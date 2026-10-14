using System.IO;
using System.Collections.Generic;
using NUnit.Framework;
using PG.Boot;
using PG.Content;
using PG.Core;
using PG.EditorTools;
using PG.Persistence;
using PG.Powers;
using PG.Sim;
using PG.World;
using PG.WorldGen;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace PG.Tests.EditMode
{
    public sealed class DeterminismTests
    {
        const int SaveSlot = 899;
        ContentDB _db;

        [OneTimeSetUp]
        public void LoadContent()
        {
            DataSync.Sync();
            _db = ContentDB.LoadAll(Path.Combine(Application.streamingAssetsPath, "Data"), null);
        }

        [Test]
        public void SameSeedAndScriptedCommandsKeepWorldAndUnitsDeterministic()
        {
            Assert.That(Diagnostics.DeterminismCheck(_db,
                new WorldGenSettings { Seed = 7319, Size = MapSizePreset.Small }, 1200), Is.True);
        }

        [Test]
        public void WorldHashIncludesPersistentEcologyZoneState()
        {
            using (var map = new WorldMap(64, 64, _db))
            {
                ulong before = map.ComputeHash();
                var zone = map.Zones[0];
                zone.Moisture++;
                map.Zones[0] = zone;
                ulong moistureChanged = map.ComputeHash();
                Assert.That(moistureChanged, Is.Not.EqualTo(before));

                zone.FoodBiomass += 1f;
                map.Zones[0] = zone;
                Assert.That(map.ComputeHash(), Is.Not.EqualTo(moistureChanged));
            }
        }

        [Test]
        public void HeightRidgesAreDeterministicAndRespectHeightBounds()
        {
            const int width = 48, height = 48;
            var baseline = new NativeArray<float>(width * height, Allocator.TempJob);
            var ridged = new NativeArray<float>(width * height, Allocator.TempJob);
            var repeated = new NativeArray<float>(width * height, Allocator.TempJob);
            var blobs = new NativeArray<float4>(0, Allocator.TempJob);
            try
            {
                var job = new HeightJob
                {
                    Width = width, Height = height, Octaves = 5, Frequency = 3f,
                    Lacunarity = 2f, Gain = 0.5f, WarpAmp = 0.08f, WarpFrequency = 2f,
                    OffMain = new float2(15.2f, 31.7f), OffWarpX = new float2(91f, -24f),
                    OffWarpY = new float2(-8f, 66f), OffMask = new float2(2f, 5f),
                    MaskType = MaskKind.None, Falloff = 2f, EdgeOcean = 0.06f,
                    Blobs = blobs, Heights = baseline,
                };
                job.Schedule(width * height, 256).Complete();

                job.RidgeStrength = 0.45f;
                job.Heights = ridged;
                job.Schedule(width * height, 256).Complete();
                job.Heights = repeated;
                job.Schedule(width * height, 256).Complete();

                bool changed = false;
                for (int i = 0; i < ridged.Length; i++)
                {
                    Assert.That(ridged[i], Is.InRange(0f, 1f));
                    Assert.That(repeated[i], Is.EqualTo(ridged[i]));
                    changed |= math.abs(ridged[i] - baseline[i]) > 0.0001f;
                }
                Assert.That(changed, Is.True, "ridge strength should shape the highland field");
            }
            finally
            {
                baseline.Dispose();
                ridged.Dispose();
                repeated.Dispose();
                blobs.Dispose();
            }
        }

        [Test]
        public void HydraulicErosionCarvesHighFlowChannelsDeterministically()
        {
            const int width = 16, height = 16, count = width * height;
            var source = new NativeArray<float>(count, Allocator.TempJob);
            var eroded = new NativeArray<float>(count, Allocator.TempJob);
            var repeated = new NativeArray<float>(count, Allocator.TempJob);
            var parent = new NativeArray<int>(count, Allocator.TempJob);
            var accumulation = new NativeArray<int>(count, Allocator.TempJob);
            try
            {
                for (int i = 0; i < count; i++)
                {
                    int x = i % width, y = i / width;
                    source[i] = x == 8 ? 0.55f + y * 0.01f : 0.55f;
                    parent[i] = x == 8 && y > 0 ? i - width : -1;
                    accumulation[i] = x == 8 ? 100 : 1;
                }

                var job = new HydraulicErosionJob
                {
                    Heights = source, Parent = parent, Accumulation = accumulation,
                    ErodedHeights = eroded, Strength = 0.35f, FlowThreshold = 8,
                };
                job.Schedule(count, 64).Complete();
                job.ErodedHeights = repeated;
                job.Schedule(count, 64).Complete();

                int channel = 8 * width + 8;
                Assert.That(eroded[channel], Is.LessThan(source[channel] - 0.001f));
                Assert.That(eroded[3 * width + 3], Is.EqualTo(source[3 * width + 3]));
                for (int i = 0; i < count; i++)
                {
                    Assert.That(eroded[i], Is.InRange(0f, 1f));
                    Assert.That(repeated[i], Is.EqualTo(eroded[i]));
                }
            }
            finally
            {
                source.Dispose(); eroded.Dispose(); repeated.Dispose(); parent.Dispose(); accumulation.Dispose();
            }
        }

        [Test]
        public void CoastalHeightSmoothingOnlyBlendsShorelineDeterministically()
        {
            const int width = 5, height = 5, count = width * height;
            var ground = new NativeArray<byte>(count, Allocator.TempJob);
            var waterByType = new NativeArray<byte>(2, Allocator.TempJob);
            var coast = new NativeArray<byte>(count, Allocator.TempJob);
            var source = new NativeArray<float>(count, Allocator.TempJob);
            var smoothed = new NativeArray<float>(count, Allocator.TempJob);
            var repeated = new NativeArray<float>(count, Allocator.TempJob);
            try
            {
                waterByType[0] = 1;
                for (int i = 0; i < count; i++)
                {
                    bool water = i % width < 2;
                    ground[i] = water ? (byte)0 : (byte)1;
                    source[i] = water ? 0.2f : 0.8f;
                }

                new CoastMaskJob { Width = width, Height = height, Ground = ground, IsWaterByType = waterByType, CoastMask = coast }
                    .Schedule(count, 16).Complete();
                var job = new CoastalHeightSmoothJob
                {
                    Width = width, Height = height, Heights = source, CoastMask = coast,
                    SmoothedHeights = smoothed, Strength = 0.5f,
                };
                job.Schedule(count, 16).Complete();
                job.SmoothedHeights = repeated;
                job.Schedule(count, 16).Complete();

                int shoreWater = 2 * width + 1, shoreLand = 2 * width + 2, inland = 2 * width;
                Assert.That(smoothed[shoreWater], Is.GreaterThan(source[shoreWater]));
                Assert.That(smoothed[shoreLand], Is.LessThan(source[shoreLand]));
                Assert.That(smoothed[inland], Is.EqualTo(source[inland]));
                for (int i = 0; i < count; i++)
                {
                    Assert.That(smoothed[i], Is.InRange(0f, 1f));
                    Assert.That(repeated[i], Is.EqualTo(smoothed[i]));
                }
            }
            finally
            {
                ground.Dispose(); waterByType.Dispose(); coast.Dispose();
                source.Dispose(); smoothed.Dispose(); repeated.Dispose();
            }
        }

        [Test]
        public void RainShadowFollowsInitialWindAndKeepsDeterministicClimateRelief()
        {
            const int width = 32, height = 12, count = width * height;
            var heights = new NativeArray<float>(count, Allocator.TempJob);
            var ground = new NativeArray<byte>(count, Allocator.TempJob);
            var levels = new NativeArray<sbyte>(1, Allocator.TempJob);
            var relief = new NativeArray<float>(count, Allocator.TempJob);
            var repeated = new NativeArray<float>(count, Allocator.TempJob);
            try
            {
                for (int i = 0; i < count; i++)
                {
                    int x = i % width;
                    heights[i] = x >= 10 && x <= 13 ? 0.98f : 0.62f;
                }

                var job = new RainShadowJob
                {
                    Width = width, Height = height, WindDirection = new float2(1f, 0f),
                    HasContinuousHeights = true, HeightField = heights, Ground = ground,
                    LevelByType = levels, MoistureRelief = relief,
                };
                job.Schedule(count, 64).Complete();
                job.MoistureRelief = repeated;
                job.Schedule(count, 64).Complete();

                int windward = 5 * width + 10, lee = 5 * width + 20, unaffected = 5 * width + 4;
                Assert.That(relief[windward], Is.GreaterThan(0f));
                Assert.That(relief[lee], Is.LessThan(-0.1f));
                Assert.That(relief[unaffected], Is.EqualTo(0f));
                for (int i = 0; i < count; i++) Assert.That(repeated[i], Is.EqualTo(relief[i]));
            }
            finally
            {
                heights.Dispose(); ground.Dispose(); levels.Dispose(); relief.Dispose(); repeated.Dispose();
            }
        }

        [Test]
        public void GeneratedWorldStartsWithZoneMoistureFromClimateField()
        {
            var settings = new WorldGenSettings { Seed = 49031, Size = MapSizePreset.Tiny, Template = "wgt.continents" };
            using (var map = WorldGenerator.Generate(settings, _db))
            {
                int wetZones = 0;
                byte minMoisture = byte.MaxValue, maxMoisture = byte.MinValue;
                for (int z = 0; z < map.Zones.Length; z++)
                {
                    byte moisture = map.Zones[z].Moisture;
                    if (moisture > 0) wetZones++;
                    if (moisture < minMoisture) minMoisture = moisture;
                    if (moisture > maxMoisture) maxMoisture = moisture;
                }

                Assert.That(wetZones, Is.GreaterThan(0));
                Assert.That(maxMoisture, Is.GreaterThan(minMoisture), "the generated climate field should vary between zones");
            }
        }

        [Test]
        public void GeneratedMajorLandmassesHaveBasicResourceGroups()
        {
            var settings = new WorldGenSettings { Seed = 28517, Size = MapSizePreset.Tiny, Template = "wgt.continents" };
            using (var map = WorldGenerator.Generate(settings, _db))
            {
                int minimumTiles = math.max(128, map.TileCount / 512);
                var visited = new byte[map.TileCount];
                var component = new int[map.TileCount];
                var required = new[] { "copper", "iron", "stone", "wood" };
                int majorLandmasses = 0;

                for (int start = 0; start < map.TileCount; start++)
                {
                    if (visited[start] != 0 || !IsLandmassTile(map, start)) continue;
                    int head = 0, count = 1;
                    component[0] = start;
                    visited[start] = 1;
                    while (head < count)
                    {
                        int current = component[head++];
                        int x = current % map.Width, y = current / map.Width;
                        for (int direction = 0; direction < 4; direction++)
                        {
                            int nx = x + (direction == 0 ? 1 : direction == 1 ? -1 : 0);
                            int ny = y + (direction == 2 ? 1 : direction == 3 ? -1 : 0);
                            if ((uint)nx >= (uint)map.Width || (uint)ny >= (uint)map.Height) continue;
                            int next = ny * map.Width + nx;
                            if (visited[next] != 0 || !IsLandmassTile(map, next)) continue;
                            visited[next] = 1;
                            component[count++] = next;
                        }
                    }

                    if (count < minimumTiles) continue;
                    majorLandmasses++;
                    var found = new HashSet<string>();
                    for (int i = 0; i < count; i++)
                    {
                        ushort value = map.Feature[component[i]];
                        if (value > 0) found.Add(_db.Features[value - 1].LandmassResource);
                    }
                    for (int i = 0; i < required.Length; i++)
                        Assert.That(found.Contains(required[i]), Is.True, $"Major landmass {majorLandmasses} is missing {required[i]}.");
                }

                Assert.That(majorLandmasses, Is.GreaterThan(0), "the selected world should contain at least one major landmass");
            }
        }

        static bool IsLandmassTile(WorldMap map, int tile)
        {
            byte ground = map.Ground[tile];
            return (map.Flags[tile] & (ushort)TileFlags.Water) == 0 && map.Tables.Level[ground] >= 3;
        }

        [Test]
        public void PriorityFloodBuildsDeterministicDownhillRoutesToWater()
        {
            const int width = 24, height = 24, count = width * height;
            var ground = new NativeArray<byte>(count, Allocator.TempJob);
            var waterByType = new NativeArray<byte>(2, Allocator.TempJob);
            var source = new NativeArray<float>(count, Allocator.TempJob);
            var filled = new NativeArray<float>(count, Allocator.TempJob);
            var filledAgain = new NativeArray<float>(count, Allocator.TempJob);
            var parent = new NativeArray<int>(count, Allocator.TempJob);
            var parentAgain = new NativeArray<int>(count, Allocator.TempJob);
            var order = new NativeArray<int>(count, Allocator.TempJob);
            var orderAgain = new NativeArray<int>(count, Allocator.TempJob);
            var heapTiles = new NativeArray<int>(count, Allocator.TempJob);
            var heapHeights = new NativeArray<float>(count, Allocator.TempJob);
            var visited = new NativeArray<byte>(count, Allocator.TempJob);
            var flow = new NativeArray<int>(count, Allocator.TempJob);
            var accumulation = new NativeArray<int>(count, Allocator.TempJob);
            var reachesWater = new NativeArray<byte>(count, Allocator.TempJob);
            try
            {
                waterByType[1] = 1;
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                    {
                        int i = y * width + x;
                        ground[i] = (byte)(x == 0 ? 1 : 0);
                        source[i] = x * 0.1f + y * 0.001f;
                    }

                var flood = new PriorityFloodJob
                {
                    Width = width, Height = height, Ground = ground, IsWaterByType = waterByType,
                    SourceHeight = source, FilledHeight = filled, Parent = parent, VisitOrder = order,
                    HeapTiles = heapTiles, HeapHeights = heapHeights, Visited = visited,
                };
                flood.Run();
                new FlowDirectionJob
                {
                    Width = width, Height = height, Ground = ground, IsWaterByType = waterByType,
                    FilledHeight = filled, Parent = parent, FlowTo = flow,
                }.Schedule(count, 64).Complete();
                new FlowAccumulationJob
                {
                    Ground = ground, IsWaterByType = waterByType, FlowTo = flow, VisitOrder = order,
                    Accumulation = accumulation, ReachesWater = reachesWater,
                }.Run();

                for (int i = 0; i < count; i++)
                {
                    if (ground[i] == 1) continue;
                    int downstream = flow[i];
                    Assert.That(downstream, Is.GreaterThanOrEqualTo(0));
                    Assert.That(filled[i], Is.GreaterThanOrEqualTo(filled[downstream]));
                    Assert.That(reachesWater[i], Is.EqualTo(1));
                    int cursor = i, steps = 0;
                    while (ground[cursor] != 1 && steps++ < count) cursor = flow[cursor];
                    Assert.That(ground[cursor], Is.EqualTo(1), "every channel route must terminate at water without a cycle");
                }

                flood.FilledHeight = filledAgain;
                flood.Parent = parentAgain;
                flood.VisitOrder = orderAgain;
                flood.Run();
                for (int i = 0; i < count; i++)
                {
                    Assert.That(filledAgain[i], Is.EqualTo(filled[i]));
                    Assert.That(parentAgain[i], Is.EqualTo(parent[i]));
                    Assert.That(orderAgain[i], Is.EqualTo(order[i]));
                }
            }
            finally
            {
                ground.Dispose(); waterByType.Dispose(); source.Dispose(); filled.Dispose(); filledAgain.Dispose();
                parent.Dispose(); parentAgain.Dispose(); order.Dispose(); orderAgain.Dispose(); heapTiles.Dispose();
                heapHeights.Dispose(); visited.Dispose(); flow.Dispose(); accumulation.Dispose(); reachesWater.Dispose();
            }
        }

        [Test]
        public void GeneratedContinentsKeepFordsWalkableAndWideRiverBedsBlockedDeterministically()
        {
            var settings = new WorldGenSettings
            {
                Seed = 6851, Size = MapSizePreset.Tiny, Template = "wgt.continents", RiverDensity = 1f,
            };
            using (var first = WorldGenerator.Generate(settings, _db))
            using (var second = WorldGenerator.Generate(settings, _db))
            {
                int fords = 0;
                int wideRiverBeds = 0;
                for (int i = 0; i < first.TileCount; i++)
                {
                    if ((first.Flags[i] & (ushort)TileFlags.River) == 0) continue;
                    bool isWater = first.IsWater(i % first.Width, i / first.Width);
                    if (isWater)
                    {
                        wideRiverBeds++;
                        Assert.That(first.IsWalkable(i % first.Width, i / first.Width), Is.False,
                            "wide river beds must block movement until a road bridge is placed");
                    }
                    else
                    {
                        fords++;
                        Assert.That(first.IsWalkable(i % first.Width, i / first.Width), Is.True,
                            "shallow river fords must remain walkable");
                    }
                }
                Assert.That(fords, Is.GreaterThan(0), "generation should retain shallow, walkable river crossings");
                Assert.That(wideRiverBeds, Is.GreaterThan(0), "high-flow channels should create blocked wide river beds");
                Assert.That(first.ComputeHash(), Is.EqualTo(second.ComputeHash()),
                    "the same seed and settings must produce the same ford and wide-river layout");
            }
        }

        [Test]
        public void ZeroRiverDensitySuppressesChannels()
        {
            var settings = new WorldGenSettings
            {
                Seed = 6851, Size = MapSizePreset.Tiny, Template = "wgt.continents", RiverDensity = 0f,
            };
            using (var map = WorldGenerator.Generate(settings, _db))
            {
                for (int i = 0; i < map.TileCount; i++)
                    Assert.That((map.Flags[i] & (ushort)TileFlags.River) == 0, Is.True,
                        "zero river density should suppress channels; lake generation runs before this channel setting is applied");
            }
        }

        [Test]
        public void NewWorldTemplatesUseTheirOwnTerrainAndClimateDefaults()
        {
            Assert.That(_db.WorldGenTemplates.TryGet("wgt.river_basins", out var basins), Is.True);
            Assert.That(_db.WorldGenTemplates.TryGet("wgt.inland_sea", out var inlandSea), Is.True);
            Assert.That(_db.WorldGenTemplates.TryGet("wgt.mountain_islands", out var mountainIslands), Is.True);
            Assert.That(basins.Mask.Type, Is.EqualTo("multiBlob"));
            Assert.That(inlandSea.Mask.Type, Is.EqualTo("annulus"));
            Assert.That(mountainIslands.Noise.FrequencyMul, Is.GreaterThan(basins.Noise.FrequencyMul));

            var settings = new WorldGenSettings();
            settings.ApplyTemplateDefaults(basins);
            Assert.That(settings.RiverDensity, Is.EqualTo(basins.RiverDensityDefault.Value));
            Assert.That(settings.Moisture, Is.EqualTo(basins.MoistureDefault.Value));
            settings.ApplyTemplateDefaults(mountainIslands);
            Assert.That(settings.MountainRatio, Is.EqualTo(mountainIslands.MountainRatioDefault.Value));
        }

        [Test]
        public void WorldGenWorkingSetEstimateScalesWithTileAreaAndPreviewSize()
        {
            long medium = WorldGenerator.EstimatePeakWorkingSetBytes(MapSizePreset.Medium);
            long huge = WorldGenerator.EstimatePeakWorkingSetBytes(MapSizePreset.Huge);
            long preview = WorldGenerator.EstimatePeakWorkingSetBytes(MapSizePreset.Huge, 4);
            Assert.That(huge, Is.EqualTo(medium * 4));
            Assert.That(preview, Is.EqualTo(huge / 16));

            var invalid = new WorldGenSettings { Size = (MapSizePreset)999 };
            invalid.Clamp();
            Assert.That(invalid.Size, Is.EqualTo(MapSizePreset.Medium));
        }

        [Test]
        public void RiverBanksStartWithMoreMoistureThanUnwateredZones()
        {
            using (var dryMap = new WorldMap(64, 64, _db))
            using (var riverMap = new WorldMap(64, 64, _db))
            {
                byte soil = (byte)_db.TileByLevel[4];
                for (int i = 0; i < dryMap.TileCount; i++)
                {
                    dryMap.Ground[i] = soil;
                    riverMap.Ground[i] = soil;
                    dryMap.Flags[i] = dryMap.Tables.TypeFlags[soil];
                    riverMap.Flags[i] = riverMap.Tables.TypeFlags[soil];
                }
                int riverX = 28, riverY = 28;
                riverMap.Flags[riverMap.Index(riverX, riverY)] |= (ushort)TileFlags.River;
                dryMap.RebuildDerivedData();
                riverMap.RebuildDerivedData();

                using (var dryWorld = new SimWorld(_db, dryMap, new SimRandomProvider(451)))
                using (var riverWorld = new SimWorld(_db, riverMap, new SimRandomProvider(451)))
                {
                    int zone = dryMap.ZoneIndexOf(riverX, riverY);
                    Assert.That(riverMap.Zones[zone].Moisture, Is.GreaterThan(dryMap.Zones[zone].Moisture));
                    Assert.That(dryWorld.Civ.FootprintFree(new int2(riverX, riverY), 1, 1, -1, plaza: false), Is.True);
                    Assert.That(riverWorld.Civ.FootprintFree(new int2(riverX, riverY), 1, 1, -1, plaza: false), Is.False);

                    for (int z = 0; z < dryMap.Zones.Length; z++)
                    {
                        var dry = dryMap.Zones[z];
                        var wet = riverMap.Zones[z];
                        dry.TemperatureC = wet.TemperatureC = 20;
                        dry.Moisture = wet.Moisture = 100;
                        dryMap.Zones[z] = dry;
                        riverMap.Zones[z] = wet;
                    }
                    var moisture = new MoistureSystem();
                    moisture.Tick(dryWorld.Context);
                    moisture.Tick(riverWorld.Context);
                    Assert.That(riverMap.Zones[zone].Moisture, Is.EqualTo(dryMap.Zones[zone].Moisture + 1));
                }
            }
        }

        [Test]
        public void SaveLoadAndContinueKeepsSimulationStateDeterministic()
        {
            SaveSystem.Delete(SaveSlot);
            var settings = new WorldGenSettings { Seed = 8821, Size = MapSizePreset.Small };
            var map = WorldGenerator.Generate(settings, _db);
            try
            {
                using (var original = new SimWorld(_db, map, new SimRandomProvider(settings.Seed), null, "A5 round trip"))
                {
                    original.SpawnInitialAnimals();
                    PowerSystems.Install(original, new PowerCommandQueue(), false);
                    for (int i = 0; i < 240; i++) original.Tick();

                    ulong expectedState = SaveSystem.ComputeStateHash(original);
                    ulong expectedMap = original.World.ComputeHash();
                    ulong expectedUnits = Diagnostics.UnitHash(original);
                    ulong expectedCiv = Diagnostics.CivHash(original);
                    ulong expectedMeta = Diagnostics.MetaHash(original);
                    long expectedTick = original.Clock.Tick;
                    SaveSystem.Save(SaveSlot, original);
                    var state = SaveSystem.Load(SaveSlot, _db);
                    using (var restored = new SimWorld(_db, state.World, state.Rng, state.Clock, state.WorldName))
                    {
                        PowerSystems.Install(restored, new PowerCommandQueue(), false);
                        state.ApplyTo(restored);
                        Assert.That(restored.Clock.Tick, Is.EqualTo(expectedTick));
                        Assert.That(SaveSystem.ComputeStateHash(restored), Is.EqualTo(expectedState));
                        Assert.That(restored.World.ComputeHash(), Is.EqualTo(expectedMap));
                        Assert.That(Diagnostics.UnitHash(restored), Is.EqualTo(expectedUnits));
                        Assert.That(Diagnostics.CivHash(restored), Is.EqualTo(expectedCiv));
                        Assert.That(Diagnostics.MetaHash(restored), Is.EqualTo(expectedMeta));

                        for (int i = 0; i < 120; i++)
                        {
                            original.Tick();
                            restored.Tick();
                            Assert.That(SaveSystem.ComputeStateHash(restored), Is.EqualTo(SaveSystem.ComputeStateHash(original)), $"complete saveable state diverged after {i + 1} continuation ticks");
                            Assert.That(restored.World.ComputeHash(), Is.EqualTo(original.World.ComputeHash()), $"world diverged after {i + 1} continuation ticks");
                            Assert.That(Diagnostics.UnitHash(restored), Is.EqualTo(Diagnostics.UnitHash(original)), $"simulation diverged after {i + 1} continuation ticks");
                            Assert.That(Diagnostics.CivHash(restored), Is.EqualTo(Diagnostics.CivHash(original)), $"civilization diverged after {i + 1} continuation ticks");
                            Assert.That(Diagnostics.MetaHash(restored), Is.EqualTo(Diagnostics.MetaHash(original)), $"meta state diverged after {i + 1} continuation ticks");
                        }
                    }
                }
            }
            finally
            {
                SaveSystem.Delete(SaveSlot);
            }
        }

        [Test]
        public void SavedWorldRecipeRoundTripsAndLegacySaveClearsIt()
        {
            SaveSystem.Delete(SaveSlot);
            var oldWriter = SaveSystem.WorldRecipeSectionWriter;
            var oldImporter = SaveSystem.WorldRecipeSectionImporter;
            var settings = new WorldGenSettings
            {
                Seed = 99231,
                Size = MapSizePreset.Large,
                Template = "wgt.continents",
                LandRatio = 0.62f,
                MountainRatio = 0.17f,
                ForestDensity = 0.73f,
                SpawnAnimals = false,
                SpawnCivs = true,
                ScenarioId = "scenario.test",
                PreviewDivisor = 4,
            };
            var map = WorldGenerator.Generate(settings, _db);
            try
            {
                using (var original = new SimWorld(_db, map, new SimRandomProvider(settings.Seed), null, "Recipe round trip"))
                {
                    PowerSystems.Install(original, new PowerCommandQueue(), false);
                    byte[] imported = null;
                    SaveSystem.WorldRecipeSectionWriter = () => WorldGenSettingsCodec.Encode(settings);
                    SaveSystem.WorldRecipeSectionImporter = raw => imported = raw;
                    SaveSystem.Save(SaveSlot, original);
                    var state = SaveSystem.Load(SaveSlot, _db);
                    using (var restored = new SimWorld(_db, state.World, state.Rng, state.Clock, state.WorldName))
                    {
                        PowerSystems.Install(restored, new PowerCommandQueue(), false);
                        state.ApplyTo(restored);
                        Assert.That(WorldGenSettingsCodec.TryDecode(imported, out var loaded), Is.True);
                        Assert.That(loaded.Seed, Is.EqualTo(settings.Seed));
                        Assert.That(loaded.Size, Is.EqualTo(settings.Size));
                        Assert.That(loaded.LandRatio, Is.EqualTo(settings.LandRatio));
                        Assert.That(loaded.MountainRatio, Is.EqualTo(settings.MountainRatio));
                        Assert.That(loaded.ForestDensity, Is.EqualTo(settings.ForestDensity));
                        Assert.That(loaded.SpawnAnimals, Is.EqualTo(settings.SpawnAnimals));
                        Assert.That(loaded.SpawnCivs, Is.EqualTo(settings.SpawnCivs));
                        Assert.That(loaded.ScenarioId, Is.EqualTo(settings.ScenarioId));
                        Assert.That(loaded.PreviewDivisor, Is.EqualTo(1));
                    }

                    SaveSystem.WorldRecipeSectionWriter = null;
                    imported = new byte[] { 1 };
                    SaveSystem.Save(SaveSlot, original);
                    var legacy = SaveSystem.Load(SaveSlot, _db);
                    using (var restoredLegacy = new SimWorld(_db, legacy.World, legacy.Rng, legacy.Clock, legacy.WorldName))
                    {
                        PowerSystems.Install(restoredLegacy, new PowerCommandQueue(), false);
                        legacy.ApplyTo(restoredLegacy);
                        Assert.That(imported, Is.Null, "a save without the optional recipe section must clear stale recipe state");
                    }
                }
            }
            finally
            {
                SaveSystem.WorldRecipeSectionWriter = oldWriter;
                SaveSystem.WorldRecipeSectionImporter = oldImporter;
                SaveSystem.Delete(SaveSlot);
            }
        }

        [Test]
        public void CorruptPrimarySaveRecoversPreviousBackupAndReportsWarning()
        {
            SaveSystem.Delete(SaveSlot);
            var settings = new WorldGenSettings { Seed = 8822, Size = MapSizePreset.Small };
            var map = WorldGenerator.Generate(settings, _db);
            try
            {
                using (var sim = new SimWorld(_db, map, new SimRandomProvider(settings.Seed), null, "Backup recovery"))
                {
                    PowerSystems.Install(sim, new PowerCommandQueue(), false);
                    SaveSystem.Save(SaveSlot, sim); // first good version
                    sim.Tick();
                    SaveSystem.Save(SaveSlot, sim); // rotates the first version to .bak
                    File.WriteAllBytes(SaveSystem.PathFor(SaveSlot), new byte[] { 0x50, 0x58 });

                    var recovered = SaveSystem.Load(SaveSlot, _db);
                    try
                    {
                        Assert.That(recovered.Clock.Tick, Is.EqualTo(0));
                        Assert.That(recovered.Warnings.Exists(w => w.Contains("restored from its backup")), Is.True);
                    }
                    finally { recovered.World.Dispose(); }
                }
            }
            finally { SaveSystem.Delete(SaveSlot); }
        }

        [Test]
        public void InterruptedTemporaryWriteDoesNotReplaceTheLastCommittedSave()
        {
            SaveSystem.Delete(SaveSlot);
            var settings = new WorldGenSettings { Seed = 8823, Size = MapSizePreset.Small };
            var map = WorldGenerator.Generate(settings, _db);
            string path = SaveSystem.PathFor(SaveSlot);
            string temporaryPath = path + ".tmp";
            try
            {
                using (var sim = new SimWorld(_db, map, new SimRandomProvider(settings.Seed), null, "Committed save"))
                {
                    PowerSystems.Install(sim, new PowerCommandQueue(), false);
                    SaveSystem.Save(SaveSlot, sim);

                    // Simulate a process stopping while the next snapshot is still being written.
                    File.WriteAllBytes(temporaryPath, new byte[] { 0x50, 0x58, 0x47 });
                    var loaded = SaveSystem.Load(SaveSlot, _db);
                    try
                    {
                        Assert.That(loaded.WorldName, Is.EqualTo("Committed save"));
                        Assert.That(loaded.Clock.Tick, Is.EqualTo(0));
                        Assert.That(loaded.Warnings.Exists(w => w.Contains("restored from its backup")), Is.False);
                    }
                    finally { loaded.World.Dispose(); }
                }
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
                SaveSystem.Delete(SaveSlot);
            }
        }
    }
}
