using System.IO;
using NUnit.Framework;
using PG.Core;
using PG.Content;
using PG.EditorTools;
using PG.Persistence;
using PG.Powers;
using PG.Sim;
using PG.World;
using UnityEngine;

namespace PG.Tests.EditMode
{
    public sealed class RegionSplitTests
    {
        const int SaveSlot = 896;
        ContentDB _db;

        [OneTimeSetUp]
        public void LoadContent()
        {
            DataSync.Sync();
            _db = ContentDB.LoadAll(Path.Combine(Application.streamingAssetsPath, "Data"), null);
        }

        [Test]
        public void RoadAcrossWideRiverConnectsLandRegionsAndRemovingItSplitsThemAgain()
        {
            using (var map = new WorldMap(64, 64, _db))
            using (var regions = new RegionGraph(map))
            {
                byte land = (byte)_db.Tiles.IdOf("tile.soil_low");
                byte river = (byte)_db.Tiles.IdOf("tile.river");
                ushort landFlags = map.Tables.TypeFlags[land];
                ushort riverFlags = (ushort)(map.Tables.TypeFlags[river] | (ushort)TileFlags.River);
                for (int i = 0; i < map.TileCount; i++)
                {
                    map.Ground[i] = land;
                    map.Flags[i] = landFlags;
                }
                for (int y = 0; y < map.Height; y++)
                    for (int x = 31; x <= 33; x++)
                    {
                        int i = map.Index(x, y);
                        map.Ground[i] = river;
                        map.Flags[i] = riverFlags;
                    }

                regions.RebuildAll();
                Assert.IsFalse(regions.SameIsland(10, 32, 50, 32, MoveClass.Land));

                for (int x = 31; x <= 33; x++) map.SetFlag(x, 32, TileFlags.Road, true);
                regions.RebuildDirty(1);
                Assert.IsTrue(regions.SameIsland(10, 32, 50, 32, MoveClass.Land));

                for (int x = 31; x <= 33; x++) map.SetFlag(x, 32, TileFlags.Road, false);
                regions.RebuildDirty(1);
                Assert.IsFalse(regions.SameIsland(10, 32, 50, 32, MoveClass.Land));
            }
        }

        [Test]
        public void BridgeFlagsSurviveSaveLoadAndReconnectLandRegions()
        {
            SaveSystem.Delete(SaveSlot);
            using (var map = new WorldMap(64, 64, _db))
            {
                byte land = (byte)_db.Tiles.IdOf("tile.soil_low");
                byte river = (byte)_db.Tiles.IdOf("tile.river");
                ushort landFlags = map.Tables.TypeFlags[land];
                ushort riverFlags = (ushort)(map.Tables.TypeFlags[river] | (ushort)TileFlags.River);
                for (int i = 0; i < map.TileCount; i++)
                {
                    map.Ground[i] = land;
                    map.Flags[i] = landFlags;
                }
                for (int y = 0; y < map.Height; y++)
                    for (int x = 31; x <= 33; x++)
                    {
                        int i = map.Index(x, y);
                        map.Ground[i] = river;
                        map.Flags[i] = riverFlags;
                    }
                map.RebuildDerivedData();

                try
                {
                    using (var original = new SimWorld(_db, map, new SimRandomProvider(896), null, "River bridge save"))
                    {
                        PowerSystems.Install(original, new PowerCommandQueue(), false);
                        for (int x = 31; x <= 33; x++) original.World.SetRoad(x, 32, true);
                        SaveSystem.Save(SaveSlot, original);

                        var state = SaveSystem.Load(SaveSlot, _db);
                        using (var restored = new SimWorld(_db, state.World, state.Rng, state.Clock, state.WorldName))
                        using (var regions = new RegionGraph(restored.World))
                        {
                            PowerSystems.Install(restored, new PowerCommandQueue(), false);
                            state.ApplyTo(restored);
                            regions.RebuildAll();

                            for (int x = 31; x <= 33; x++)
                            {
                                TileFlags flags = restored.World.GetFlags(x, 32);
                                Assert.That(flags & (TileFlags.River | TileFlags.Water | TileFlags.Road | TileFlags.Walkable),
                                    Is.EqualTo(TileFlags.River | TileFlags.Water | TileFlags.Road | TileFlags.Walkable));
                            }
                            Assert.That(regions.SameIsland(10, 32, 50, 32, MoveClass.Land), Is.True);
                        }
                    }
                }
                finally
                {
                    SaveSystem.Delete(SaveSlot);
                }
            }
        }
    }
}
