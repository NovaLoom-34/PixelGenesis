using System;
using System.Collections.Generic;
using PG.Content;
using PG.Core;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace PG.World
{
    public sealed class WorldMap : IDisposable
    {
        public const int ChunkSize = 64, ChunkShift = 6;
        public const int ZoneSize = 8, ZoneShift = 3;

        // --- Tile storage (SoA), index = y * Width + x ---
        public NativeArray<byte> Ground;    // TileTypeId
        public NativeArray<byte> Biome;     // BiomeDef.MapValue, 0 = none
        public NativeArray<ushort> Flags;   // TileFlags
        public NativeArray<byte> Variant;   // color variant 0-3
        public NativeArray<byte> Fire;      // fire intensity 0-255 (Bölüm 2)
        public NativeArray<ushort> Feature; // FeatureDef.MapValue, 0 = none (Bölüm 2)
        public NativeArray<byte> FeatureState; // features: bits 0-1 stage, bits 2-7 resource; empty tiles store forage initialization/reserve
        public NativeArray<int> Building;   // building index, -1 = none (Bölüm 5)

        public NativeArray<ZoneData> Zones;
        public NativeArray<ChunkData> Chunks;

        // Tiles that turned into an active type (lava, goo): (index, previous ground). Drained by the nature systems.
        public NativeList<int2> ActivatedTiles;
        public const int MaxActivatedTiles = 1 << 16;

        public readonly TileTables Tables;
        public readonly ContentDB Content;

        int _batchDepth;
        ChangeSource _batchSource;
        readonly DirtyMask[] _batchMask;
        readonly List<int> _batchChunks = new List<int>(64);

        public WorldMap(int width, int height, ContentDB content, EventBus events = null)
        {
            if (width <= 0 || height <= 0 || width % ChunkSize != 0 || height % ChunkSize != 0)
                throw new ArgumentException($"Map size must be a positive multiple of {ChunkSize}: {width}x{height}");

            Width = width;
            Height = height;
            Content = content;
            Events = events;
            Tables = new TileTables(content);

            int n = width * height;
            Ground = new NativeArray<byte>(n, Allocator.Persistent);
            Biome = new NativeArray<byte>(n, Allocator.Persistent);
            Flags = new NativeArray<ushort>(n, Allocator.Persistent);
            Variant = new NativeArray<byte>(n, Allocator.Persistent);
            Fire = new NativeArray<byte>(n, Allocator.Persistent);
            Feature = new NativeArray<ushort>(n, Allocator.Persistent);
            FeatureState = new NativeArray<byte>(n, Allocator.Persistent);
            ActivatedTiles = new NativeList<int2>(256, Allocator.Persistent);
            Building = new NativeArray<int>(n, Allocator.Persistent);
            for (int i = 0; i < n; i++) Building[i] = -1;

            Zones = new NativeArray<ZoneData>(ZonesX * ZonesY, Allocator.Persistent);
            for (int i = 0; i < Zones.Length; i++) Zones[i] = new ZoneData { OwnerCity = -1 };
            Chunks = new NativeArray<ChunkData>(ChunksX * ChunksY, Allocator.Persistent);
            _batchMask = new DirtyMask[Chunks.Length];
            // A blank map must already carry derived data (type flags, zone land/water counts) the way a loaded one does.
            RebuildDerivedData();
        }

        public int Width { get; }
        public int Height { get; }
        public int ChunksX => Width >> ChunkShift;
        public int ChunksY => Height >> ChunkShift;
        public int ZonesX => Width >> ZoneShift;
        public int ZonesY => Height >> ZoneShift;
        public int TileCount => Width * Height;
        public int ChunkCount => Chunks.Length;
        public EventBus Events { get; set; }

        // --- Read ---
        public bool InBounds(int x, int y) => (uint)x < (uint)Width && (uint)y < (uint)Height;
        public int Index(int x, int y) => y * Width + x;
        public byte GetGround(int x, int y) => Ground[Index(x, y)];
        public byte GetBiome(int x, int y) => Biome[Index(x, y)];
        public TileFlags GetFlags(int x, int y) => (TileFlags)Flags[Index(x, y)];
        public bool HasFlag(int x, int y, TileFlags flag) => (Flags[Index(x, y)] & (ushort)flag) != 0;
        public bool IsWalkable(int x, int y) => HasFlag(x, y, TileFlags.Walkable);
        public bool IsWater(int x, int y) => HasFlag(x, y, TileFlags.Water);
        public int GetLevel(int x, int y) => Tables.Level[Ground[Index(x, y)]];
        public int ZoneIndexOf(int x, int y) => (y >> ZoneShift) * ZonesX + (x >> ZoneShift);
        public int ChunkIndexOf(int x, int y) => (y >> ChunkShift) * ChunksX + (x >> ChunkShift);
        public TileTypeDef GroundDef(int x, int y) => Content.Tiles[Ground[Index(x, y)]];

        // --- Write (single gate: every change goes through here) ---
        public void SetGround(int x, int y, byte type, ChangeSource src)
        {
            int i = Index(x, y);
            byte old = Ground[i];
            if (old == type) return;

            Ground[i] = type;
            ushort flags = Flags[i];
            ushort oldType = (ushort)(flags & (ushort)TileFlags.TypeMask);
            ushort newType = Tables.TypeFlags[type];
            flags = (ushort)((flags & ~(ushort)TileFlags.TypeMask) | newType);
            bool riverCleared = src != ChangeSource.WorldGen && src != ChangeSource.Load &&
                                (flags & (ushort)TileFlags.River) != 0;
            if (src != ChangeSource.WorldGen && src != ChangeSource.Load) flags &= unchecked((ushort)~(ushort)TileFlags.River);

            var mask = DirtyMask.Render | DirtyMask.Stats | DirtyMask.Save;
            if (!Tables.BiomeFits(type, Biome[i]))
            {
                if (Feature[i] == 0) RemoveGroundFood(i);
                Biome[i] = 0;
            }
            if (!Tables.FeatureFits(type, Feature[i]))
            {
                ClearFeatureCounted(x, y, i); // land plants drown, coral dries out
                mask |= DirtyMask.Features;
            }

            bool wasWater = (oldType & (ushort)TileFlags.Water) != 0;
            bool isWater = (newType & (ushort)TileFlags.Water) != 0;
            if (isWater)
            {
                Fire[i] = 0;
                flags &= unchecked((ushort)~(ushort)(TileFlags.Burning | TileFlags.Road | TileFlags.SnowCover));
            }
            if (Tables.MeltTo[type] != TileTables.NoStep) flags |= (ushort)TileFlags.Frozen;
            else flags &= unchecked((ushort)~(ushort)TileFlags.Frozen);
            if ((flags & (ushort)TileFlags.Reserved) != 0) flags &= unchecked((ushort)~(ushort)TileFlags.Walkable); // building footprint stays blocked
            Flags[i] = flags;

            if (Tables.Active[type] != 0 && ActivatedTiles.Length < MaxActivatedTiles) ActivatedTiles.Add(new int2(i, old));

            if (wasWater != isWater)
            {
                int z = ZoneIndexOf(x, y);
                var zone = Zones[z];
                if (isWater) { zone.WaterTiles++; zone.LandTiles--; }
                else { zone.LandTiles++; zone.WaterTiles--; }
                Zones[z] = zone;
            }

            if (((oldType ^ newType) & (ushort)TileFlags.MoveMask) != 0) mask |= DirtyMask.Regions;
            Touch(x, y, mask);
            if (riverCleared)
            {
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = x + dx, ny = y + dy;
                    if (InBounds(nx, ny)) Touch(nx, ny, DirtyMask.Render);
                }
            }

            if (_batchDepth == 0 && Publishes(src)) Events?.Publish(new TileChangedEvent(x, y, old, type));
        }

        public void SetBiome(int x, int y, byte biome, ChangeSource src)
        {
            int i = Index(x, y);
            if (Biome[i] == biome) return;
            if (!Tables.BiomeFits(Ground[i], biome)) return;
            if (Feature[i] == 0)
            {
                int beforeResource = (FeatureState[i] & 1) != 0 ? GetFeatureResource(i) : 0;
                float before = GroundFoodBiomass(Biome[i], beforeResource);
                int afterResource = Tables.BiomeFoodValue[biome] > 0f ? 63 : 0;
                float after = GroundFoodBiomass(biome, afterResource);
                int z = ZoneIndexOf(x, y);
                var zone = Zones[z];
                zone.FoodBiomass = math.max(0f, zone.FoodBiomass - before + after);
                Zones[z] = zone;
                FeatureState[i] = biome == 0 ? (byte)0 : (byte)((afterResource << 2) | 1);
            }
            Biome[i] = biome;
            Touch(x, y, DirtyMask.Render | DirtyMask.Stats | DirtyMask.Save);
        }

        // New features start as saplings (stage 0) with their full resource; worldgen passes a grown stage.
        public void SetFeature(int x, int y, ushort feature, ChangeSource src) => SetFeature(x, y, feature, 0, src);

        public void SetFeature(int x, int y, ushort feature, int stage, ChangeSource src)
        {
            int i = Index(x, y);
            if (Feature[i] == feature) return;
            if (!Tables.FeatureFits(Ground[i], feature)) return;
            ClearFeatureCounted(x, y, i);
            if (feature != 0)
            {
                RemoveGroundFood(i);
                Feature[i] = feature;
                FeatureState[i] = (byte)((Tables.FeatureResource[feature] << 2) | (stage & 3));
                CountFeature(ZoneIndexOf(x, y), feature, 1, GetFeatureResource(i));
            }
            Touch(x, y, DirtyMask.Features | DirtyMask.Stats | DirtyMask.Save);
        }

        // Bölüm 5.4 footprint: building index on the tile; blocking tiles get Reserved and lose Walkable (the door stays walkable).
        public void SetBuilding(int x, int y, int building, bool blocking)
        {
            int i = Index(x, y);
            Building[i] = building;
            ushort flags = Flags[i];
            ushort old = flags;
            if (building >= 0 && blocking) flags = (ushort)((flags | (ushort)TileFlags.Reserved) & ~(ushort)TileFlags.Walkable);
            else flags = (ushort)((flags & ~(ushort)TileFlags.Reserved & ~(ushort)TileFlags.Walkable) | (Tables.TypeFlags[Ground[i]] & (ushort)TileFlags.Walkable));
            if (building >= 0) flags &= unchecked((ushort)~(ushort)TileFlags.Road);
            Flags[i] = flags;
            var mask = DirtyMask.Render | DirtyMask.Save | DirtyMask.Overlay | DirtyMask.Stats;
            if (((old ^ flags) & (ushort)TileFlags.MoveMask) != 0) mask |= DirtyMask.Regions;
            Touch(x, y, mask);
        }

        public void SetRoad(int x, int y, bool on)
        {
            SetFlag(x, y, TileFlags.Road, on);
        }

        public void SetZoneOwner(int zone, int city)
        {
            var z = Zones[zone];
            if (z.OwnerCity == city) return;
            z.OwnerCity = city;
            Zones[zone] = z;
            int x = (zone % ZonesX) << ZoneShift, y = (zone / ZonesX) << ZoneShift;
            MarkChunkDirty(ChunkIndexOf(x, y), DirtyMask.Overlay | DirtyMask.Save);
        }

        public void SetProtectedSpecies(int zone, ushort speciesIndexPlusOne)
        {
            if ((uint)zone >= (uint)Zones.Length) return;
            var data = Zones[zone];
            if (data.ProtectedSpecies == speciesIndexPlusOne) return;
            data.ProtectedSpecies = speciesIndexPlusOne;
            Zones[zone] = data;
            int x = (zone % ZonesX) << ZoneShift, y = (zone / ZonesX) << ZoneShift;
            MarkChunkDirty(ChunkIndexOf(x, y), DirtyMask.Overlay | DirtyMask.Save);
        }

        public bool IsSpeciesProtectedAt(int species, int x, int y)
        {
            if (species < 0 || !InBounds(x, y)) return false;
            return Zones[ZoneIndexOf(x, y)].ProtectedSpecies == species + 1;
        }

        public int GetFeatureStage(int i) => FeatureState[i] & 3;
        public int GetFeatureResource(int i) => FeatureState[i] >> 2;

        public void SetFeatureStage(int x, int y, int stage)
        {
            int i = Index(x, y);
            byte s = (byte)((FeatureState[i] & ~3) | (stage & 3));
            if (s == FeatureState[i]) return;
            FeatureState[i] = s;
            Touch(x, y, DirtyMask.Features | DirtyMask.Save);
        }

        public void SetFeatureResource(int x, int y, int resource)
        {
            int i = Index(x, y);
            int before = GetFeatureResource(i);
            int after = math.clamp(resource, 0, 63);
            if (before == after) return;
            ushort feature = Feature[i];
            int z = ZoneIndexOf(x, y);
            var zone = Zones[z];
            if (feature != 0)
            {
                zone.FoodBiomass = math.max(0f, zone.FoodBiomass - FeatureFoodBiomass(feature, before) + FeatureFoodBiomass(feature, after));
            }
            else
            {
                byte biome = Biome[i];
                int oldResource = (FeatureState[i] & 1) != 0 ? before : 0;
                zone.FoodBiomass = math.max(0f, zone.FoodBiomass - GroundFoodBiomass(biome, oldResource) + GroundFoodBiomass(biome, after));
            }
            Zones[z] = zone;
            FeatureState[i] = (byte)((FeatureState[i] & 3) | (after << 2) | (feature == 0 ? 1 : 0));
            MarkChunkDirty(ChunkIndexOf(x, y), DirtyMask.Save);
        }

        // Fire intensity: render is refreshed only when the visible palette step changes.
        public void SetFire(int x, int y, byte value)
        {
            int i = Index(x, y);
            byte old = Fire[i];
            if (old == value) return;
            Fire[i] = value;
            if ((old >> 6) != (value >> 6)) Touch(x, y, DirtyMask.Render);
        }

        void ClearFeatureCounted(int x, int y, int i)
        {
            ushort f = Feature[i];
            if (f == 0) return;
            CountFeature(ZoneIndexOf(x, y), f, -1, GetFeatureResource(i));
            Feature[i] = 0;
            byte biome = Biome[i];
            int reserve = biome != 0 && Tables.BiomeFoodValue[biome] > 0f ? 63 : 0;
            FeatureState[i] = biome == 0 ? (byte)0 : (byte)((reserve << 2) | 1);
            if (reserve > 0)
            {
                int z = ZoneIndexOf(x, y);
                var zone = Zones[z];
                zone.FoodBiomass += GroundFoodBiomass(biome, reserve);
                Zones[z] = zone;
            }
        }

        void CountFeature(int z, ushort feature, int delta, int resource)
        {
            byte kind = Tables.FeatureKind[feature];
            var zone = Zones[z];
            if (kind == FeatureDef.KindTree) zone.TreeCount = (byte)math.clamp(zone.TreeCount + delta, 0, 255);
            else if (kind == FeatureDef.KindPlant) zone.PlantCount = (byte)math.clamp(zone.PlantCount + delta, 0, 255);
            zone.FoodBiomass = math.max(0f, zone.FoodBiomass + delta * FeatureFoodBiomass(feature, resource));
            Zones[z] = zone;
        }

        float GroundFoodBiomass(byte biome, int resource) => biome == 0
            ? 0f : Tables.BiomeFoodValue[biome] * (resource / 63f);

        void RemoveGroundFood(int i)
        {
            byte biome = Biome[i];
            if (biome == 0) { FeatureState[i] = 0; return; }
            int resource = (FeatureState[i] & 1) != 0 ? GetFeatureResource(i) : 0;
            int z = ZoneIndexOf(i % Width, i / Width);
            var zone = Zones[z];
            zone.FoodBiomass = math.max(0f, zone.FoodBiomass - GroundFoodBiomass(biome, resource));
            Zones[z] = zone;
            FeatureState[i] = 0;
        }

        float FeatureFoodBiomass(ushort feature, int resource)
        {
            int maximum = Tables.FeatureResource[feature];
            return maximum <= 0 ? 0f : Tables.FeatureFoodValue[feature] * (resource / (float)maximum);
        }

        public void SetFlag(int x, int y, TileFlags flag, bool value)
        {
            int i = Index(x, y);
            ushort before = Flags[i];
            ushort after = value ? (ushort)(before | (ushort)flag) : (ushort)(before & ~(ushort)flag);
            bool roadOnRiver = (flag & TileFlags.Road) != 0 && (before & (ushort)TileFlags.River) != 0 &&
                               (Tables.TypeFlags[Ground[i]] & (ushort)TileFlags.Water) != 0;
            if (roadOnRiver)
            {
                if (value) after |= (ushort)TileFlags.Walkable;
                else after &= unchecked((ushort)~(ushort)TileFlags.Walkable);
            }
            if (before == after) return;
            Flags[i] = after;
            var mask = DirtyMask.Render | DirtyMask.Save;
            if (((before ^ after) & (ushort)TileFlags.MoveMask) != 0) mask |= DirtyMask.Regions;
            Touch(x, y, mask);
        }

        public void RaiseLevel(int x, int y, ChangeSource src)
        {
            byte to = Tables.RaiseTo[Ground[Index(x, y)]];
            if (to != TileTables.NoStep) SetGround(x, y, to, src);
        }

        public void LowerLevel(int x, int y, ChangeSource src)
        {
            byte to = Tables.LowerTo[Ground[Index(x, y)]];
            if (to != TileTables.NoStep) SetGround(x, y, to, src);
        }

        // --- Batch write (brushes): one ChunkDirtyEvent per chunk instead of one event per tile ---
        public TileEditBatch BeginBatch(ChangeSource src)
        {
            if (_batchDepth == 0) _batchSource = src;
            _batchDepth++;
            return new TileEditBatch(this, src);
        }

        internal void EndBatch()
        {
            if (_batchDepth == 0) return;
            if (--_batchDepth > 0) return;

            bool publish = Publishes(_batchSource);
            for (int k = 0; k < _batchChunks.Count; k++)
            {
                int c = _batchChunks[k];
                if (publish) Events?.Publish(new ChunkDirtyEvent(c, _batchMask[c]));
                _batchMask[c] = DirtyMask.None;
            }
            _batchChunks.Clear();
        }

        // --- Dirty bookkeeping ---
        public void MarkChunkDirty(int chunk, DirtyMask mask)
        {
            var c = Chunks[chunk];
            c.Dirty |= mask;
            Chunks[chunk] = c;
        }

        public void ClearDirty(int chunk, DirtyMask mask)
        {
            var c = Chunks[chunk];
            if ((c.Dirty & mask) == 0) return;
            c.Dirty &= ~mask;
            Chunks[chunk] = c;
        }

        public void MarkAllDirty(DirtyMask mask)
        {
            for (int c = 0; c < Chunks.Length; c++) MarkChunkDirty(c, mask);
        }

        public int CountDirty(DirtyMask mask)
        {
            int n = 0;
            for (int c = 0; c < Chunks.Length; c++)
                if ((Chunks[c].Dirty & mask) != 0) n++;
            return n;
        }

        // After bulk writes (worldgen, load): re-derive type flags and zone stats, dirty everything.
        public void RebuildDerivedData()
        {
            for (int z = 0; z < Zones.Length; z++)
            {
                var zone = Zones[z];
                zone.LandTiles = 0;
                zone.WaterTiles = 0;
                zone.TreeCount = 0;
                zone.PlantCount = 0;
                zone.FoodBiomass = 0;
                Zones[z] = zone;
            }

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    int i = Index(x, y);
                    byte g = Ground[i];
                    ushort newType = Tables.TypeFlags[g];
                    ushort flags = (ushort)((Flags[i] & ~(ushort)TileFlags.TypeMask) | newType);
                    if ((newType & (ushort)TileFlags.Water) != 0 &&
                        (flags & ((ushort)TileFlags.River | (ushort)TileFlags.Road)) ==
                        ((ushort)TileFlags.River | (ushort)TileFlags.Road))
                        flags |= (ushort)TileFlags.Walkable; // persisted river bridge override
                    if ((flags & (ushort)TileFlags.Reserved) != 0) flags &= unchecked((ushort)~(ushort)TileFlags.Walkable); // building footprints stay blocked
                    Flags[i] = flags;
                    if (!Tables.BiomeFits(g, Biome[i])) Biome[i] = 0;
                    if (!Tables.FeatureFits(g, Feature[i])) { Feature[i] = 0; FeatureState[i] = 0; }

                    int z = ZoneIndexOf(x, y);
                    var zone = Zones[z];
                    if ((newType & (ushort)TileFlags.Water) != 0) zone.WaterTiles++;
                    else zone.LandTiles++;
                    if (Feature[i] == 0)
                    {
                        if (Biome[i] == 0) FeatureState[i] = 0;
                        else if ((FeatureState[i] & 1) == 0)
                        {
                            int reserve = Tables.BiomeFoodValue[Biome[i]] > 0f ? 63 : 0;
                            FeatureState[i] = (byte)((reserve << 2) | 1);
                        }
                        zone.FoodBiomass += GroundFoodBiomass(Biome[i], GetFeatureResource(i));
                    }
                    Zones[z] = zone;
                    if (Feature[i] != 0) CountFeature(z, Feature[i], 1, GetFeatureResource(i));
                }
            }
            ActivatedTiles.Clear();
            MarkAllDirty(DirtyMask.All);
        }

        void Touch(int x, int y, DirtyMask mask)
        {
            int c = ChunkIndexOf(x, y);
            MarkChunkDirty(c, mask);
            if (_batchDepth > 0)
            {
                if (_batchMask[c] == DirtyMask.None) _batchChunks.Add(c);
                _batchMask[c] |= mask;
            }
        }

        static bool Publishes(ChangeSource src) => src != ChangeSource.WorldGen && src != ChangeSource.Load;

        // xxHash64 over the simulation arrays (determinism check, Bölüm 1.15).
        public ulong ComputeHash()
        {
            ulong h = 1469598103934665603UL;
            h = (h ^ (uint)Width) * 1099511628211UL;
            h = (h ^ (uint)Height) * 1099511628211UL;
            h = (h ^ Hash(Ground)) * 1099511628211UL;
            h = (h ^ Hash(Biome)) * 1099511628211UL;
            h = (h ^ Hash(Flags)) * 1099511628211UL;
            h = (h ^ Hash(Variant)) * 1099511628211UL;
            h = (h ^ Hash(Fire)) * 1099511628211UL;
            h = (h ^ Hash(Feature)) * 1099511628211UL;
            h = (h ^ Hash(FeatureState)) * 1099511628211UL;
            h = (h ^ Hash(Building)) * 1099511628211UL;
            h = (h ^ Hash(ActivatedTiles.AsArray())) * 1099511628211UL;
            for (int z = 0; z < Zones.Length; z++)
            {
                var zone = Zones[z];
                h = (h ^ (uint)zone.OwnerCity) * 1099511628211UL;
                h = (h ^ (ushort)zone.TemperatureC ^ ((ulong)(ushort)zone.BaseTemperatureC << 16)) * 1099511628211UL;
                h = (h ^ zone.LandTiles ^ ((ulong)zone.WaterTiles << 16)) * 1099511628211UL;
                h = (h ^ zone.DominantBiome ^ ((ulong)zone.TreeCount << 8) ^ ((ulong)zone.PlantCount << 16)) * 1099511628211UL;
                h = (h ^ zone.Moisture ^ ((ulong)zone.DryMonths << 8) ^ ((ulong)zone.Drought << 16)) * 1099511628211UL;
                // FoodBiomass is updated incrementally but rebuilt on load, so the last float bits can differ; hash at 1/256 precision.
                h = (h ^ (ulong)(long)math.round(zone.FoodBiomass * 256f) ^ ((ulong)zone.ProtectedSpecies << 32)) * 1099511628211UL;
            }
            return h;
        }

        static unsafe ulong Hash<T>(NativeArray<T> array) where T : struct
        {
            var v = xxHash3.Hash64(NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(array), (long)array.Length * UnsafeUtility.SizeOf<T>());
            return ((ulong)v.y << 32) | v.x;
        }

        public void Dispose()
        {
            if (Ground.IsCreated) Ground.Dispose();
            if (Biome.IsCreated) Biome.Dispose();
            if (Flags.IsCreated) Flags.Dispose();
            if (Variant.IsCreated) Variant.Dispose();
            if (Fire.IsCreated) Fire.Dispose();
            if (Feature.IsCreated) Feature.Dispose();
            if (FeatureState.IsCreated) FeatureState.Dispose();
            if (ActivatedTiles.IsCreated) ActivatedTiles.Dispose();
            if (Building.IsCreated) Building.Dispose();
            if (Zones.IsCreated) Zones.Dispose();
            if (Chunks.IsCreated) Chunks.Dispose();
            Tables.Dispose();
        }
    }
}
