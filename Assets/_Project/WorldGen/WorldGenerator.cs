using System;
using PG.Content;
using PG.Core;
using PG.World;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace PG.WorldGen
{
    public static class WorldGenerator
    {
        const int Buckets = 1024;
        const int BatchSize = 1024;
        const float CoastRangeChamfer = 3f * 20f; // coast influence: 20 tiles
        const int MinLandSpeck = 3;

        public static WorldMap Generate(WorldGenSettings s, ContentDB db, IProgress<float> progress = null)
        {
            s.Clamp();
            if (s.Seed == 0) s.Seed = RandomSeed();
            if (db.MaxLevel < 8) throw new InvalidOperationException("WorldGen needs tile levels 0..8 (deep ocean .. summit)");

            var template = db.WorldGenTemplates.TryGet(s.Template, out var t) ? t : db.WorldGenTemplates[0];
            int size = SizeFor(s);
            var map = new WorldMap(size, size, db);
            try
            {
                using (var gen = new Run(map, db, s, template, progress)) gen.Execute();
            }
            catch
            {
                map.Dispose();
                throw;
            }
            return map;
        }

        public static int SizeFor(WorldGenSettings s)
        {
            int size = MapSizes.TilesFor(s.Size) / Math.Max(1, s.PreviewDivisor);
            return Math.Max(WorldMap.ChunkSize, (size + WorldMap.ChunkSize - 1) / WorldMap.ChunkSize * WorldMap.ChunkSize);
        }

        // Conservative, source-derived estimate for map plus full-worldgen tile arrays; excludes Unity/runtime/render assets.
        public static long EstimatePeakWorkingSetBytes(MapSizePreset preset, int previewDivisor = 1)
        {
            if ((uint)preset >= (uint)MapSizes.Count) preset = MapSizePreset.Medium;
            int divisor = Math.Max(1, previewDivisor);
            int size = MapSizes.TilesFor(preset) / divisor;
            size = Math.Max(WorldMap.ChunkSize, (size + WorldMap.ChunkSize - 1) / WorldMap.ChunkSize * WorldMap.ChunkSize);
            return (long)size * size * 64;
        }

        // Outside the simulation: picking a fresh seed for a new world is a player choice, not sim randomness.
        public static ulong RandomSeed()
        {
            ulong v = (ulong)DateTime.UtcNow.Ticks ^ ((ulong)Environment.TickCount << 32);
            v ^= v >> 33;
            v *= 0xFF51AFD7ED558CCDUL;
            v ^= v >> 33;
            return v == 0 ? 1UL : v;
        }

        sealed class Run : IDisposable
        {
            readonly WorldMap _map;
            readonly ContentDB _db;
            readonly WorldGenSettings _s;
            readonly WorldGenTemplateDef _template;
            readonly IProgress<float> _progress;
            readonly int _w, _h, _n;
            SimRandom _rng;

            NativeArray<byte> _levels;
            NativeArray<byte> _isWater;
            NativeArray<byte> _temp;
            NativeArray<byte> _initialMoisture;
            NativeArray<float> _temperature;
            NativeArray<float> _heightField;

            public Run(WorldMap map, ContentDB db, WorldGenSettings s, WorldGenTemplateDef template, IProgress<float> progress)
            {
                _map = map;
                _db = db;
                _s = s;
                _template = template;
                _progress = progress;
                _w = map.Width;
                _h = map.Height;
                _n = map.TileCount;
                _rng = new SimRandom(s.Seed, (ulong)RngStream.WorldGen + 1);

                _levels = new NativeArray<byte>(9, Allocator.TempJob);
                for (int l = 0; l <= 8; l++) _levels[l] = (byte)db.TileByLevel[l];
                _isWater = new NativeArray<byte>(db.Tiles.Count, Allocator.TempJob);
                for (int i = 0; i < db.Tiles.Count; i++) _isWater[i] = (byte)(db.Tiles[i].Water ? 1 : 0);
                _temp = new NativeArray<byte>(_n, Allocator.TempJob);
                _initialMoisture = new NativeArray<byte>(_n, Allocator.TempJob);
                _temperature = new NativeArray<float>(_n, Allocator.TempJob);
            }

            byte Deep => _levels[0];
            byte Ocean => _levels[1];
            byte Shallow => _levels[2];
            byte Sand => _levels[3];
            byte SoilLow => _levels[4];
            byte RiverTile => (byte)_db.Tiles.IdOrDefault("tile.river");

            public void Execute()
            {
                Report(0f);
                string mask = _template.Mask?.Type ?? "none";
                switch (mask)
                {
                    case "empty":
                        Fill((byte)_template.FlatTileId);
                        ClimateAndBiomes(-1, false);
                        Report(0.6f);
                        break;
                    case "full":
                        FlatWithEdge((byte)_template.FlatTileId);
                        Coast();
                        Report(0.4f);
                        ClimateAndBiomes(_template.FlatBiomeId, true);
                        Report(0.6f);
                        break;
                    case "image":
                        ImageImporter.Apply(_s.ImagePath, _map, _db);
                        ClimateAndBiomes(-1, false);
                        Report(0.6f);
                        break;
                    default:
                        Heights();
                        Report(0.35f);
                        Cleanup();
                        Report(0.5f);
                        Coast();
                        ClimateAndBiomes(-1, true);
                        Report(0.7f);
                        Patches();
                        Hydrology();
                        Report(0.76f);
                        break;
                }

                Variants();
                Report(0.8f);
                Ores();
                Vegetation();
                EnsureMajorLandmassResources();
                Report(0.9f);
                _map.RebuildDerivedData();
                ZoneTemperaturesAndMoisture();
                Report(1f);
            }

            // --- Step 1-3: height field, sea level percentile, leveled tiles ---
            void Heights()
            {
                _heightField = new NativeArray<float>(_n, Allocator.TempJob);
                var hist = new NativeArray<int>(Buckets, Allocator.TempJob);
                var thresholds = new NativeArray<float>(8, Allocator.TempJob);
                var blobs = BuildBlobs();
                try
                {
                    var tmpl = _template;
                    new HeightJob
                    {
                        Width = _w,
                        Height = _h,
                        Octaves = math.max(1, tmpl.Noise.Octaves),
                        Frequency = 3f * tmpl.Noise.FrequencyMul,
                        Lacunarity = tmpl.Noise.Lacunarity,
                        Gain = tmpl.Noise.Gain,
                        RidgeStrength = tmpl.Noise.RidgeStrength,
                        WarpAmp = tmpl.Warp.Amplitude * _s.Roughness * 2f,
                        WarpFrequency = 3f * tmpl.Warp.FrequencyMul,
                        OffMain = RandomOffset(),
                        OffWarpX = RandomOffset(),
                        OffWarpY = RandomOffset(),
                        OffMask = RandomOffset(),
                        MaskType = MaskTypeOf(tmpl.Mask.Type),
                        Falloff = math.max(0.1f, tmpl.Mask.FalloffPower),
                        AnnulusRadius = tmpl.Mask.Radius != null && tmpl.Mask.Radius.Length > 0 ? _rng.Range(tmpl.Mask.Radius[0], tmpl.Mask.Radius[tmpl.Mask.Radius.Length - 1]) : 0.3f,
                        AnnulusWidth = math.max(0.01f, tmpl.Mask.Width),
                        EdgeOcean = tmpl.EdgeOcean,
                        Blobs = blobs,
                        Heights = _heightField,
                    }.Schedule(_n, BatchSize).Complete();

                    ApplyHeightErosion(tmpl.Noise.ErosionStrength);

                    new HistogramJob { Heights = _heightField, Histogram = hist }.Run();
                    CalculateHeightThresholds(hist, thresholds);
                    new ClassifyJob { Heights = _heightField, Thresholds = thresholds, Levels = _levels, Ground = _map.Ground }
                        .Schedule(_n, BatchSize).Complete();

                    ApplyCoastalSmoothing(tmpl.Noise.ErosionStrength);

                    new HistogramJob { Heights = _heightField, Histogram = hist }.Run();
                    CalculateHeightThresholds(hist, thresholds);

                    new ClassifyJob { Heights = _heightField, Thresholds = thresholds, Levels = _levels, Ground = _map.Ground }
                        .Schedule(_n, BatchSize).Complete();

                }
                finally
                {
                    hist.Dispose();
                    thresholds.Dispose();
                    blobs.Dispose();
                }
            }

            void ApplyHeightErosion(float strength)
            {
                if (strength <= 0f) return;

                var outlets = new NativeArray<byte>(_n, Allocator.TempJob);
                var waterByType = new NativeArray<byte>(2, Allocator.TempJob);
                var filled = new NativeArray<float>(_n, Allocator.TempJob);
                var parent = new NativeArray<int>(_n, Allocator.TempJob);
                var order = new NativeArray<int>(_n, Allocator.TempJob);
                var heapTiles = new NativeArray<int>(_n, Allocator.TempJob);
                var heapHeights = new NativeArray<float>(_n, Allocator.TempJob);
                var visited = new NativeArray<byte>(_n, Allocator.TempJob);
                var flowTo = new NativeArray<int>(_n, Allocator.TempJob);
                var accumulation = new NativeArray<int>(_n, Allocator.TempJob);
                var reachesWater = new NativeArray<byte>(_n, Allocator.TempJob);
                var eroded = new NativeArray<float>(_n, Allocator.TempJob);
                try
                {
                    waterByType[1] = 1;
                    for (int y = 0; y < _h; y++)
                    for (int x = 0; x < _w; x++)
                        if (x == 0 || y == 0 || x == _w - 1 || y == _h - 1)
                            outlets[y * _w + x] = 1;

                    new PriorityFloodJob
                    {
                        Width = _w, Height = _h, Ground = outlets, IsWaterByType = waterByType,
                        SourceHeight = _heightField, FilledHeight = filled, Parent = parent, VisitOrder = order,
                        HeapTiles = heapTiles, HeapHeights = heapHeights, Visited = visited,
                    }.Run();
                    new ParentFlowJob { Parent = parent, FlowTo = flowTo }.Schedule(_n, BatchSize).Complete();
                    new FlowAccumulationJob
                    {
                        Ground = outlets, IsWaterByType = waterByType, FlowTo = flowTo, VisitOrder = order,
                        Accumulation = accumulation, ReachesWater = reachesWater,
                    }.Run();

                    new HydraulicErosionJob
                    {
                        Heights = _heightField,
                        Parent = parent,
                        Accumulation = accumulation,
                        ErodedHeights = eroded,
                        Strength = math.saturate(strength),
                        FlowThreshold = math.max(8, _n / 1600),
                    }.Schedule(_n, BatchSize).Complete();
                    _heightField.CopyFrom(eroded);
                }
                finally
                {
                    outlets.Dispose(); waterByType.Dispose(); filled.Dispose(); parent.Dispose(); order.Dispose();
                    heapTiles.Dispose(); heapHeights.Dispose(); visited.Dispose(); flowTo.Dispose(); accumulation.Dispose();
                    reachesWater.Dispose(); eroded.Dispose();
                }
            }

            void CalculateHeightThresholds(NativeArray<int> histogram, NativeArray<float> thresholds)
            {
                float water = 1f - _s.LandRatio, land = _s.LandRatio;
                float m = _s.MountainRatio, hr = _s.HillRatio;
                float[] fractions =
                {
                    0.55f * water,
                    0.85f * water,
                    water,
                    water + 0.06f * land,
                    water + (1f - m - hr - 0.25f) * land,
                    water + (1f - m - hr) * land,
                    water + (1f - m) * land,
                    water + (1f - m * 0.15f) * land,
                };
                float prev = 0f;
                for (int k = 0; k < fractions.Length; k++)
                {
                    prev = math.max(prev, fractions[k]);
                    thresholds[k] = Quantile(histogram, _n, prev);
                }
            }

            void ApplyCoastalSmoothing(float strength)
            {
                if (strength <= 0f) return;
                var coast = new NativeArray<byte>(_n, Allocator.TempJob);
                var smoothed = new NativeArray<float>(_n, Allocator.TempJob);
                try
                {
                    new CoastMaskJob
                    {
                        Width = _w, Height = _h, Ground = _map.Ground,
                        IsWaterByType = _isWater, CoastMask = coast,
                    }.Schedule(_n, BatchSize).Complete();
                    new CoastalHeightSmoothJob
                    {
                        Width = _w, Height = _h, Heights = _heightField, CoastMask = coast,
                        SmoothedHeights = smoothed, Strength = strength,
                    }.Schedule(_n, BatchSize).Complete();
                    _heightField.CopyFrom(smoothed);
                }
                finally
                {
                    coast.Dispose();
                    smoothed.Dispose();
                }
            }

            // Priority-flood depression filling creates interior lake basins; reverse drainage
            // accumulation then identifies narrow, walkable channels that actually reach water.
            void Hydrology()
            {
                if (!_heightField.IsCreated) return;
                var filled = new NativeArray<float>(_n, Allocator.TempJob);
                var parent = new NativeArray<int>(_n, Allocator.TempJob);
                var order = new NativeArray<int>(_n, Allocator.TempJob);
                var heapTiles = new NativeArray<int>(_n, Allocator.TempJob);
                var heapHeights = new NativeArray<float>(_n, Allocator.TempJob);
                var visited = new NativeArray<byte>(_n, Allocator.TempJob);
                var flowTo = new NativeArray<int>(_n, Allocator.TempJob);
                var accumulation = new NativeArray<int>(_n, Allocator.TempJob);
                var reachesWater = new NativeArray<byte>(_n, Allocator.TempJob);
                var wideRiver = new NativeArray<byte>(_n, Allocator.TempJob);
                try
                {
                    new PriorityFloodJob
                    {
                        Width = _w, Height = _h, Ground = _map.Ground, IsWaterByType = _isWater,
                        SourceHeight = _heightField, FilledHeight = filled, Parent = parent, VisitOrder = order,
                        HeapTiles = heapTiles, HeapHeights = heapHeights, Visited = visited,
                    }.Run();

                    float lakeDepth = 0.018f;
                    int maxLakeTiles = math.max(12, _n / 100);
                    int lakeCandidates = CountLakeCandidates(filled, lakeDepth);
                    while (lakeCandidates > maxLakeTiles && lakeDepth < 1f)
                    {
                        lakeDepth *= 1.5f;
                        lakeCandidates = CountLakeCandidates(filled, lakeDepth);
                    }
                    if (lakeCandidates > 0)
                    {
                        for (int i = 0; i < _n; i++)
                        {
                            if (_isWater[_map.Ground[i]] != 0 || filled[i] - _heightField[i] <= lakeDepth) continue;
                            byte old = _map.Ground[i];
                            if (_map.Tables.Level[old] >= 8) continue;
                            _map.Ground[i] = Shallow;
                            _map.Flags[i] = (ushort)((_map.Flags[i] & ~(ushort)TileFlags.TypeMask) | _map.Tables.TypeFlags[Shallow]);
                        }
                    }

                    new FlowDirectionJob
                    {
                        Width = _w, Height = _h, Ground = _map.Ground, IsWaterByType = _isWater,
                        FilledHeight = filled, Parent = parent, FlowTo = flowTo,
                    }.Schedule(_n, BatchSize).Complete();
                    new FlowAccumulationJob
                    {
                        Ground = _map.Ground, IsWaterByType = _isWater, FlowTo = flowTo, VisitOrder = order,
                        Accumulation = accumulation, ReachesWater = reachesWater,
                    }.Run();

                    int baseRiverThreshold = math.max(24, _n / 1600);
                    int riverThreshold = math.max(1, (int)math.round(baseRiverThreshold * math.lerp(2.1f, 0.35f, _s.RiverDensity)));
                    int wideRiverThreshold = math.max(baseRiverThreshold * 2, riverThreshold * 4);
                    for (int i = 0; _s.RiverDensity > 0f && i < _n; i++)
                    {
                        byte ground = _map.Ground[i];
                        if (_isWater[ground] != 0 || reachesWater[i] == 0 || accumulation[i] < riverThreshold) continue;
                        if ((_map.Tables.TypeFlags[ground] & (ushort)TileFlags.Walkable) == 0) continue;
                        _map.Flags[i] |= (ushort)TileFlags.River;
                        if (accumulation[i] >= wideRiverThreshold) wideRiver[i] = 1;
                    }

                    // Widen only strong downstream flow. The cross section follows the local flow
                    // normal, leaving ordinary channels shallow and walkable as fords.
                    byte riverTile = RiverTile;
                    for (int i = 0; i < _n; i++)
                    {
                        if (wideRiver[i] == 0) continue;
                        int downstream = flowTo[i];
                        if ((uint)downstream >= (uint)_n) continue;
                        int dx = downstream % _w - i % _w;
                        int dy = downstream / _w - i / _w;
                        int nx = dy == 0 ? 0 : dy > 0 ? -1 : 1;
                        int ny = dx == 0 ? 0 : dx > 0 ? 1 : -1;
                        MakeWideRiverTile(i, riverTile);
                        for (int side = -1; side <= 1; side += 2)
                        {
                            int x = i % _w + nx * side, y = i / _w + ny * side;
                            if ((uint)x >= (uint)_w || (uint)y >= (uint)_h) continue;
                            int bank = y * _w + x;
                            byte bankGround = _map.Ground[bank];
                            int level = _map.Tables.Level[bankGround];
                            if (_isWater[bankGround] != 0 || level < 3 || level > 6 ||
                                (_map.Tables.TypeFlags[bankGround] & (ushort)TileFlags.Walkable) == 0) continue;
                            MakeWideRiverTile(bank, riverTile);
                        }
                    }
                }
                finally
                {
                    filled.Dispose(); parent.Dispose(); order.Dispose(); heapTiles.Dispose(); heapHeights.Dispose();
                    visited.Dispose(); flowTo.Dispose(); accumulation.Dispose(); reachesWater.Dispose(); wideRiver.Dispose();
                }
            }

            void MakeWideRiverTile(int i, byte riverTile)
            {
                _map.Ground[i] = riverTile;
                _map.Biome[i] = 0;
                _map.Feature[i] = 0;
                _map.FeatureState[i] = 0;
                ushort flags = (ushort)((_map.Flags[i] & ~(ushort)TileFlags.TypeMask) | _map.Tables.TypeFlags[riverTile]);
                flags |= (ushort)TileFlags.River;
                flags &= unchecked((ushort)~(ushort)(TileFlags.Walkable | TileFlags.Buildable | TileFlags.Burning | TileFlags.Road | TileFlags.SnowCover));
                _map.Flags[i] = flags;
            }

            int CountLakeCandidates(NativeArray<float> filled, float depth)
            {
                int count = 0;
                for (int i = 0; i < _n; i++)
                    if (_isWater[_map.Ground[i]] == 0 && _map.Tables.Level[_map.Ground[i]] < 8 && filled[i] - _heightField[i] > depth)
                        count++;
                return count;
            }

            NativeArray<float4> BuildBlobs()
            {
                var mask = _template.Mask;
                int kind = MaskTypeOf(mask.Type);
                float r0 = mask.Radius != null && mask.Radius.Length > 0 ? mask.Radius[0] : 0.4f;
                float r1 = mask.Radius != null && mask.Radius.Length > 1 ? mask.Radius[1] : r0;

                if (kind == MaskKind.Radial)
                {
                    var one = new NativeArray<float4>(1, Allocator.TempJob);
                    one[0] = new float4(0.5f, 0.5f, _rng.Range(r0, r1), 0f);
                    return one;
                }
                if (kind != MaskKind.MultiBlob) return new NativeArray<float4>(0, Allocator.TempJob);

                int c0 = mask.Count != null && mask.Count.Length > 0 ? mask.Count[0] : 2;
                int c1 = mask.Count != null && mask.Count.Length > 1 ? mask.Count[1] : c0;
                int count = math.max(1, _rng.Range(c0, c1 + 1));
                var blobs = new NativeArray<float4>(count, Allocator.TempJob);
                for (int b = 0; b < count; b++)
                {
                    float2 p = default;
                    for (int attempt = 0; attempt < 30; attempt++)
                    {
                        p = new float2(_rng.Range(0.2f, 0.8f), _rng.Range(0.2f, 0.8f));
                        bool ok = true;
                        for (int k = 0; k < b && ok; k++) ok = math.distance(p, blobs[k].xy) >= 0.3f;
                        if (ok) break;
                    }
                    blobs[b] = new float4(p, _rng.Range(r0, r1), 0f);
                }
                return blobs;
            }

            static int MaskTypeOf(string type)
            {
                switch (type)
                {
                    case "radial": return MaskKind.Radial;
                    case "multiBlob": return MaskKind.MultiBlob;
                    case "annulus": return MaskKind.Annulus;
                    case "inverse": return MaskKind.Inverse;
                    default: return MaskKind.None;
                }
            }

            // Global quantile with linear interpolation inside the bucket. frac >= 1 -> above every height.
            static float Quantile(NativeArray<int> hist, int total, float frac)
            {
                if (frac >= 1f) return 2f;
                long target = (long)(frac * total);
                long cum = 0;
                for (int b = 0; b < hist.Length; b++)
                {
                    int c = hist[b];
                    if (cum + c > target)
                    {
                        float within = c > 0 ? (target - cum) / (float)c : 0f;
                        return (b + within) / hist.Length;
                    }
                    cum += c;
                }
                return 2f;
            }

            float2 RandomOffset() => new float2(_rng.Range(-1000f, 1000f), _rng.Range(-1000f, 1000f));

            // --- Step 4: cellular cleanup and speck removal ---
            void Cleanup()
            {
                for (int pass = 0; pass < 2; pass++)
                {
                    new SmoothJob { Width = _w, Height = _h, OutsideTile = Deep, Src = _map.Ground, Dst = _temp }
                        .Schedule(_n, BatchSize).Complete();
                    _map.Ground.CopyFrom(_temp);
                }

                var visited = new NativeArray<byte>(_n, Allocator.TempJob);
                var stack = new NativeArray<int>(_n, Allocator.TempJob);
                try
                {
                    new RemoveSpecksJob
                    {
                        Width = _w,
                        Height = _h,
                        MinSize = MinLandSpeck,
                        ReplaceWith = Shallow,
                        Ground = _map.Ground,
                        IsWaterByType = _isWater,
                        Visited = visited,
                        Stack = stack,
                    }.Run();
                }
                finally
                {
                    visited.Dispose();
                    stack.Dispose();
                }
            }

            // --- Step 5: coast ---
            void Coast()
            {
                new CoastJob
                {
                    Width = _w,
                    Height = _h,
                    Deep = Deep,
                    Ocean = Ocean,
                    Shallow = Shallow,
                    Sand = Sand,
                    SoilLow = SoilLow,
                    IsWaterByType = _isWater,
                    Src = _map.Ground,
                    Dst = _temp,
                }.Schedule(_n, BatchSize).Complete();
                _map.Ground.CopyFrom(_temp);
            }

            // --- Steps 6-7: climate and biome table (forcedBiome >= 0 paints that biome everywhere instead) ---
            void ClimateAndBiomes(int forcedBiome, bool writeBiomes)
            {
                var table = _db.BiomeTables[0];
                var grid = new NativeArray<int>(math.max(1, table.GridIds.Length), Allocator.TempJob);
                var capable = new NativeArray<byte>(_db.Tiles.Count, Allocator.TempJob);
                var levels = new NativeArray<sbyte>(_db.Tiles.Count, Allocator.TempJob);
                var dist = new NativeArray<int>(_n, Allocator.TempJob);
                var moistureRelief = new NativeArray<float>(_n, Allocator.TempJob);
                // Jobs need a constructed container even without continuous heights; a zero-length array is never read then.
                var heightsForJobs = _heightField.IsCreated ? _heightField : new NativeArray<float>(0, Allocator.TempJob);
                try
                {
                    int rows = forcedBiome >= 0 ? 1 : table.Rows;
                    int cols = forcedBiome >= 0 ? 1 : table.Cols;
                    if (forcedBiome >= 0) grid[0] = forcedBiome + 1;
                    else
                        for (int k = 0; k < table.GridIds.Length; k++) grid[k] = table.GridIds[k] + 1;

                    for (int i = 0; i < _db.Tiles.Count; i++)
                    {
                        var tile = _db.Tiles[i];
                        capable[i] = (byte)(tile.CanHaveBiome && !tile.Water ? 1 : 0);
                        levels[i] = (sbyte)tile.Level;
                    }

                    new ChamferJob { Width = _w, Height = _h, Ground = _map.Ground, IsWaterByType = _isWater, Dist = dist }.Run();

                    new RainShadowJob
                    {
                        Width = _w, Height = _h, WindDirection = InitialWindDirection(),
                        HasContinuousHeights = _heightField.IsCreated,
                        HeightField = heightsForJobs, Ground = _map.Ground, LevelByType = levels,
                        MoistureRelief = moistureRelief,
                    }.Schedule(_n, BatchSize).Complete();

                    new ClimateBiomeJob
                    {
                        Width = _w,
                        Height = _h,
                        MoistureShift = _s.Moisture - 0.5f,
                        TemperatureShift = _s.Temperature - 0.5f,
                        CoastRange = CoastRangeChamfer,
                        OffMoisture = RandomOffset(),
                        OffTemperature = RandomOffset(),
                        HasContinuousHeights = _heightField.IsCreated,
                        WriteBiomes = writeBiomes,
                        GridRows = rows,
                        GridCols = cols,
                        GridBiome = grid,
                        Ground = _map.Ground,
                        Dist = dist,
                        LevelByType = levels,
                        HeightField = heightsForJobs,
                        MoistureRelief = moistureRelief,
                        BiomeCapableLand = capable,
                        Biome = _map.Biome,
                        Temperature = _temperature,
                        Moisture = _initialMoisture,
                    }.Schedule(_n, BatchSize).Complete();
                }
                finally
                {
                    grid.Dispose();
                    capable.Dispose();
                    levels.Dispose();
                    dist.Dispose();
                    moistureRelief.Dispose();
                    if (!_heightField.IsCreated) heightsForJobs.Dispose();
                }
            }

            float2 InitialWindDirection()
            {
                // Match NatureState's derived stream so its first cloud movement follows the climate map's wind.
                var rng = SimRandom.Derive(_s.Seed, 0x0E7A, 0);
                float angle = rng.Range(0f, 2f * math.PI);
                return new float2(math.cos(angle), math.sin(angle));
            }

            // Special biomes grown as flood-filled patches: count = round(BiomeVariety * chunks * patchesPerChunk).
            void Patches()
            {
                var table = _db.BiomeTables[0];
                if (table.PatchIds.Length == 0) return;
                int count = (int)math.round(_s.BiomeVariety * _map.ChunkCount * table.PatchesPerChunk);
                if (count <= 0) return;

                var stamp = new int[_n];
                var queue = new int[table.PatchSize[1] * 4 + 8];
                var biome = _map.Biome;

                for (int p = 1; p <= count; p++)
                {
                    int start = -1;
                    for (int attempt = 0; attempt < 50 && start < 0; attempt++)
                    {
                        int i = _rng.Range(0, _n);
                        if (biome[i] != 0) start = i;
                    }
                    if (start < 0) continue;

                    byte patchBiome = (byte)(table.PatchIds[_rng.Range(0, table.PatchIds.Length)] + 1);
                    int target = _rng.Range(table.PatchSize[0], table.PatchSize[1] + 1);
                    int frontier = 0, painted = 0;
                    queue[frontier++] = start;
                    stamp[start] = p;
                    // Growing from a random frontier tile (not FIFO) keeps patches blobby instead of diamond-shaped.
                    while (frontier > 0 && painted < target)
                    {
                        int pick = _rng.Range(0, frontier);
                        int i = queue[pick];
                        queue[pick] = queue[--frontier];
                        biome[i] = patchBiome;
                        painted++;
                        int x = i % _w, y = i / _w;
                        for (int dir = 0; dir < 4 && frontier < queue.Length; dir++)
                        {
                            int nx = x + (dir == 0 ? 1 : dir == 1 ? -1 : 0);
                            int ny = y + (dir == 2 ? 1 : dir == 3 ? -1 : 0);
                            if (nx < 0 || ny < 0 || nx >= _w || ny >= _h) continue;
                            int j = ny * _w + nx;
                            if (stamp[j] == p || biome[j] == 0) continue;
                            stamp[j] = p;
                            queue[frontier++] = j;
                        }
                    }
                }
            }

            // --- Step 8: variants ---
            void Variants()
            {
                new VariantJob { Width = _w, Seed = (uint)_s.Seed ^ (uint)(_s.Seed >> 32), Offset = RandomOffset(), Variant = _map.Variant }
                    .Schedule(_n, BatchSize).Complete();
            }

            // --- Step 9: ore veins (Poisson disk on hills/mountains) ---
            void Ores()
            {
                int featureCount = _db.Features.Count;
                var weights = new float[featureCount];
                var tileWeights = new float[featureCount];
                bool any = false;
                for (int i = 0; i < featureCount; i++)
                {
                    weights[i] = _db.Features[i].VeinWeight;
                    any |= weights[i] > 0f;
                }
                if (!any) return;

                byte hills = _levels[6], mountain = _levels[7];
                float minDist = math.lerp(18f, 6f, _s.OreDensity);
                float cell = minDist / math.SQRT2;
                int gw = (int)math.ceil(_w / cell), gh = (int)math.ceil(_h / cell);
                var grid = new int[gw * gh];
                for (int i = 0; i < grid.Length; i++) grid[i] = -1;
                int attempts = (int)(_n / (minDist * minDist) * 3f);
                var ground = _map.Ground;
                var feature = _map.Feature;

                for (int a = 0; a < attempts; a++)
                {
                    int x = _rng.Range(0, _w), y = _rng.Range(0, _h);
                    int seedIndex = y * _w + x;
                    byte g = ground[seedIndex];
                    if ((_map.Flags[seedIndex] & (ushort)TileFlags.River) != 0) continue;
                    if (feature[seedIndex] != 0) continue;
                    if (g != hills && g != mountain) continue;

                    int gx = (int)(x / cell), gy = (int)(y / cell);
                    bool free = true;
                    for (int dy = -2; dy <= 2 && free; dy++)
                    {
                        for (int dx = -2; dx <= 2 && free; dx++)
                        {
                            int cx = gx + dx, cy = gy + dy;
                            if (cx < 0 || cy < 0 || cx >= gw || cy >= gh) continue;
                            int other = grid[cy * gw + cx];
                            if (other < 0) continue;
                            int ox = other % _w, oy = other / _w;
                            if ((ox - x) * (ox - x) + (oy - y) * (oy - y) < minDist * minDist) free = false;
                        }
                    }
                    if (!free) continue;
                    grid[gy * gw + gx] = y * _w + x;

                    int level = _map.Tables.Level[g];
                    byte biome = _map.Biome[seedIndex];
                    for (int f = 0; f < featureCount; f++)
                    {
                        var definition = _db.Features[f];
                        tileWeights[f] = weights[f] * definition.VeinWeightAtLevel(level) *
                                         (definition.AllowsVeinInBiome(biome) ? 1f : 0f);
                    }
                    int pick = _rng.WeightedIndex(tileWeights);
                    if (pick < 0) continue;
                    var vein = _db.Features[pick];
                    ushort value = _db.Features[pick].MapValue;
                    int length = _rng.Range(3, 10);
                    int vx = x, vy = y;
                    for (int step = 0; step < length; step++)
                    {
                        int i = vy * _w + vx;
                        if (CanPlaceRandomVeinTile(vein, i))
                        {
                            feature[i] = value;
                            _map.FeatureState[i] = (byte)((_map.Tables.FeatureResource[value] << 2) | 2);
                        }
                        int dir = _rng.Range(0, 4);
                        vx = math.clamp(vx + (dir == 0 ? 1 : dir == 1 ? -1 : 0), 0, _w - 1);
                        vy = math.clamp(vy + (dir == 2 ? 1 : dir == 3 ? -1 : 0), 0, _h - 1);
                    }
                }
            }

            void EnsureMajorLandmassResources()
            {
                int minimumTiles = math.max(128, _n / 512);
                var visited = new byte[_n];
                var component = new int[_n];
                int landmassOrdinal = 0;

                for (int start = 0; start < _n; start++)
                {
                    if (visited[start] != 0 || !IsLandmassTile(start)) continue;
                    int head = 0, count = 1;
                    component[0] = start;
                    visited[start] = 1;
                    while (head < count)
                    {
                        int current = component[head++];
                        int x = current % _w, y = current / _w;
                        for (int direction = 0; direction < 4; direction++)
                        {
                            int nx = x + (direction == 0 ? 1 : direction == 1 ? -1 : 0);
                            int ny = y + (direction == 2 ? 1 : direction == 3 ? -1 : 0);
                            if ((uint)nx >= (uint)_w || (uint)ny >= (uint)_h) continue;
                            int next = ny * _w + nx;
                            if (visited[next] != 0 || !IsLandmassTile(next)) continue;
                            visited[next] = 1;
                            component[count++] = next;
                        }
                    }

                    if (count < minimumTiles) continue;
                    landmassOrdinal++;
                    for (int f = 0; f < _db.Features.Count; f++)
                    {
                        var definition = _db.Features[f];
                        string group = definition.LandmassResource;
                        if (string.IsNullOrEmpty(group) || HasEarlierResourceGroup(f, group)) continue;
                        if (LandmassHasResource(component, count, group)) continue;
                        if (FindLandmassResource(component, count, group, landmassOrdinal,
                                out int seed, out int featureIndex, out bool nativeTree))
                            PlaceLandmassResourceCluster(seed, featureIndex, nativeTree);
                    }
                }
            }

            bool IsLandmassTile(int tile)
            {
                byte ground = _map.Ground[tile];
                return _isWater[ground] == 0 && _map.Tables.Level[ground] >= 3;
            }

            bool HasEarlierResourceGroup(int featureIndex, string group)
            {
                for (int i = 0; i < featureIndex; i++)
                    if (string.Equals(_db.Features[i].LandmassResource, group, StringComparison.Ordinal)) return true;
                return false;
            }

            bool LandmassHasResource(int[] component, int count, string group)
            {
                for (int p = 0; p < count; p++)
                {
                    ushort value = _map.Feature[component[p]];
                    if (value == 0) continue;
                    var feature = _db.Features[value - 1];
                    if (string.Equals(feature.LandmassResource, group, StringComparison.Ordinal)) return true;
                }
                return false;
            }

            bool FindLandmassResource(int[] component, int count, string group, int landmassOrdinal,
                out int seed, out int selectedFeatureIndex, out bool nativeTree)
            {
                seed = -1;
                selectedFeatureIndex = -1;
                nativeTree = false;
                float bestQuality = -1f;
                uint bestHash = uint.MaxValue;
                for (int p = 0; p < count; p++)
                {
                    int tile = component[p];
                    if (_map.Feature[tile] != 0 || (_map.Flags[tile] & (ushort)TileFlags.River) != 0) continue;
                    byte ground = _map.Ground[tile];
                    int level = _map.Tables.Level[ground];
                    byte biomeMapValue = _map.Biome[tile];
                    for (int f = 0; f < _db.Features.Count; f++)
                    {
                        var definition = _db.Features[f];
                        if (!string.Equals(definition.LandmassResource, group, StringComparison.Ordinal)) continue;
                        bool tree = definition.KindCode == FeatureDef.KindTree;
                        float reliefWeight = tree ? 1f : definition.VeinWeightAtLevel(level);
                        if (reliefWeight <= 0f || (tree ? definition.TotalYield <= 0 : definition.VeinWeight <= 0f) ||
                            !definition.AllowsVeinInBiome(biomeMapValue)) continue;

                        bool native = tree && BiomeContainsTree(biomeMapValue, definition.MapValue);
                        float quality = (native ? 2f : 1f) + reliefWeight * (tree ? 1f : definition.VeinWeight);
                        uint hash = NoiseUtil.Hash(tile % _w, tile / _w,
                            (uint)_s.Seed ^ (uint)(definition.Index * 0x9E3779B9) ^ (uint)landmassOrdinal);
                        if (quality < bestQuality || (quality == bestQuality && hash >= bestHash)) continue;
                        bestQuality = quality;
                        bestHash = hash;
                        seed = tile;
                        selectedFeatureIndex = f;
                        nativeTree = native;
                    }
                }
                return seed >= 0;
            }

            bool BiomeContainsTree(byte biomeMapValue, ushort featureValue)
            {
                if (biomeMapValue == 0) return false;
                var trees = _db.Biomes[biomeMapValue - 1].TreeValues;
                for (int i = 0; i < trees.Length; i++) if (trees[i] == featureValue) return true;
                return false;
            }

            bool CanPlaceRandomVeinTile(FeatureDef vein, int tile)
            {
                byte ground = _map.Ground[tile];
                int level = _map.Tables.Level[ground];
                return _map.Feature[tile] == 0 && (_map.Flags[tile] & (ushort)TileFlags.River) == 0 &&
                       vein.VeinWeightAtLevel(level) > 0f && vein.AllowsVeinInBiome(_map.Biome[tile]);
            }

            void PlaceLandmassResourceCluster(int seed, int featureIndex, bool nativeTree)
            {
                if ((uint)featureIndex >= (uint)_db.Features.Count) return;
                var first = _db.Features[featureIndex];
                ushort value = first.MapValue;
                int[] frontier = new int[12];
                int head = 0, tail = 1, placed = 0;
                frontier[0] = seed;
                PlaceLandmassTile(seed, value);
                placed++;

                while (head < tail && placed < 4)
                {
                    int current = frontier[head++];
                    int x = current % _w, y = current / _w;
                    for (int direction = 0; direction < 4 && placed < 4 && tail < frontier.Length; direction++)
                    {
                        int nx = x + (direction == 0 ? 1 : direction == 1 ? -1 : 0);
                        int ny = y + (direction == 2 ? 1 : direction == 3 ? -1 : 0);
                        if ((uint)nx >= (uint)_w || (uint)ny >= (uint)_h) continue;
                        int next = ny * _w + nx;
                        if (!CanPlaceLandmassTile(first, next, nativeTree)) continue;
                        PlaceLandmassTile(next, value);
                        frontier[tail++] = next;
                        placed++;
                    }
                }
            }

            bool CanPlaceLandmassTile(FeatureDef feature, int tile, bool nativeTree)
            {
                if (_map.Feature[tile] != 0 || (_map.Flags[tile] & (ushort)TileFlags.River) != 0 || !IsLandmassTile(tile)) return false;
                byte biome = _map.Biome[tile];
                int level = _map.Tables.Level[_map.Ground[tile]];
                if (feature.KindCode == FeatureDef.KindTree)
                    return feature.TotalYield > 0 && (!nativeTree || BiomeContainsTree(biome, feature.MapValue));
                return feature.VeinWeight > 0f && feature.VeinWeightAtLevel(level) > 0f && feature.AllowsVeinInBiome(biome);
            }

            void PlaceLandmassTile(int tile, ushort feature)
            {
                _map.Feature[tile] = feature;
                _map.FeatureState[tile] = (byte)((_map.Tables.FeatureResource[feature] << 2) | 2);
            }

            // --- Step 10: initial trees and plants from each biome's lists, grown stages, zone caps (Bölüm 2.4) ---
            void Vegetation()
            {
                const int maxTrees = FeatureDensity.MaxTreesPerZone, maxPlants = FeatureDensity.MaxPlantsPerZone;
                float treeChance = _s.ForestDensity * 0.12f, plantChance = 0.04f + _s.ForestDensity * 0.04f;
                var trees = new byte[_map.Zones.Length];
                var plants = new byte[_map.Zones.Length];
                var tables = _map.Tables;
                for (int y = 0; y < _h; y++)
                {
                    for (int x = 0; x < _w; x++)
                    {
                        int i = y * _w + x;
                        byte b = _map.Biome[i];
                        if (b == 0 || _map.Feature[i] != 0 || (_map.Flags[i] & (ushort)TileFlags.River) != 0) continue;
                        var biome = _db.Biomes[b - 1];
                        if (biome.EffectKind == BiomeEffect.NoPlants) continue;
                        int z = _map.ZoneIndexOf(x, y);
                        ushort pick = 0;
                        if (biome.TreeValues.Length > 0 && trees[z] < maxTrees && _rng.Chance(treeChance))
                        {
                            pick = biome.TreeValues[_rng.Range(0, biome.TreeValues.Length)];
                            trees[z]++;
                        }
                        else if (biome.PlantValues.Length > 0 && plants[z] < maxPlants && _rng.Chance(plantChance))
                        {
                            pick = biome.PlantValues[_rng.Range(0, biome.PlantValues.Length)];
                            plants[z]++;
                        }
                        if (pick == 0 || !tables.FeatureFits(_map.Ground[i], pick)) continue;
                        _map.Feature[i] = pick;
                        _map.FeatureState[i] = (byte)((tables.FeatureResource[pick] << 2) | _rng.Range(1, 4));
                    }
                }
            }

            void Fill(byte tile)
            {
                for (int i = 0; i < _n; i++)
                {
                    _map.Ground[i] = tile;
                    _map.Biome[i] = 0;
                }
            }

            // "full" mask: flat land with an ocean band of EdgeOcean at the map border.
            void FlatWithEdge(byte tile)
            {
                float band = _template.EdgeOcean * math.min(_w, _h);
                for (int y = 0; y < _h; y++)
                {
                    for (int x = 0; x < _w; x++)
                    {
                        float edge = math.min(math.min(x, y), math.min(_w - 1 - x, _h - 1 - y));
                        _map.Ground[y * _w + x] = edge < band ? Deep : tile;
                    }
                }
            }

            void ZoneTemperaturesAndMoisture()
            {
                int zw = _map.ZonesX, zh = _map.ZonesY;
                for (int zy = 0; zy < zh; zy++)
                {
                    for (int zx = 0; zx < zw; zx++)
                    {
                        float sum = 0f;
                        int moistureSum = 0;
                        int x0 = zx << WorldMap.ZoneShift, y0 = zy << WorldMap.ZoneShift;
                        for (int y = y0; y < y0 + WorldMap.ZoneSize; y++)
                            for (int x = x0; x < x0 + WorldMap.ZoneSize; x++)
                            {
                                int tile = y * _w + x;
                                sum += _temperature[tile];
                                moistureSum += _initialMoisture[tile];
                            }
                        float t = sum / (WorldMap.ZoneSize * WorldMap.ZoneSize);
                        int z = zy * zw + zx;
                        var zone = _map.Zones[z];
                        zone.BaseTemperatureC = (short)math.round(math.lerp(-20f, 40f, t));
                        zone.TemperatureC = zone.BaseTemperatureC; // refined monthly by TemperatureSystem
                        zone.Moisture = (byte)math.round(moistureSum / (float)(WorldMap.ZoneSize * WorldMap.ZoneSize));
                        _map.Zones[z] = zone;
                    }
                }
            }

            void Report(float value) => _progress?.Report(value);

            public void Dispose()
            {
                if (_levels.IsCreated) _levels.Dispose();
                if (_isWater.IsCreated) _isWater.Dispose();
                if (_temp.IsCreated) _temp.Dispose();
                if (_initialMoisture.IsCreated) _initialMoisture.Dispose();
                if (_temperature.IsCreated) _temperature.Dispose();
                if (_heightField.IsCreated) _heightField.Dispose();
            }
        }
    }
}
