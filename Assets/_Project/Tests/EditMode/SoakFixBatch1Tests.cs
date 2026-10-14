using System.IO;
using NUnit.Framework;
using PG.Content;
using PG.Core;
using PG.EditorTools;
using PG.Sim;
using PG.World;
using Unity.Mathematics;
using UnityEngine;

namespace PG.Tests.EditMode
{
    // Soak quality-gate bug fixes (DECISIONS #206-#208): hall sites, planner duplicates,
    // ambient moisture recharge and ruins as fire fuel.
    public sealed class SoakFixBatch1Tests
    {
        ContentDB _db;
        SimWorld _sim;
        SimRandom _rng;

        [OneTimeSetUp]
        public void LoadContent()
        {
            DataSync.Sync();
            _db = ContentDB.LoadAll(Path.Combine(Application.streamingAssetsPath, "Data"), null);
        }

        [SetUp]
        public void Setup()
        {
            _sim = new SimWorld(_db, new WorldMap(64, 64, _db), new SimRandomProvider(5521));
            _rng = _sim.Rng.Get(RngStream.Civ);
        }

        [TearDown]
        public void Teardown() => _sim?.Dispose();

        void MakeLand()
        {
            byte soil = (byte)_db.Tiles.IdOf("tile.soil_low");
            for (int y = 0; y < _sim.World.Height; y++)
                for (int x = 0; x < _sim.World.Width; x++)
                    _sim.World.SetGround(x, y, soil, ChangeSource.WorldGen);
            _sim.World.RebuildDerivedData();
        }

        ushort MetalVeinFeature()
        {
            var civ = _sim.Civ;
            for (ushort f = 1; f <= _db.Features.Count; f++)
                if (_sim.World.Tables.FeatureKind[f] == FeatureDef.KindOre && f != civ.BonesFeature && civ.FeatureRes[f] != civ.ResStone && civ.FeatureRes[f] >= 0)
                    return f;
            Assert.Fail("No metal vein feature in the content.");
            return 0;
        }

        [Test]
        public void MetalVeinsBlockOrdinaryFootprintsButNotHallsOrMines()
        {
            MakeLand();
            var civ = _sim.Civ;
            var origin = new int2(20, 20);
            Assert.That(civ.FootprintFree(origin, 6, 6, -1, -1, false), Is.True);
            _sim.World.SetFeature(23, 23, MetalVeinFeature(), ChangeSource.WorldGen);

            Assert.That(civ.FootprintFree(origin, 6, 6, -1, -1, false), Is.False);
            Assert.That(civ.FootprintFree(origin, 6, 6, -1, -1, false, clearOre: true), Is.True);
        }

        [Test]
        public void HallFootprintToleratesRingNeighboursButNotBuildingsInsideIt()
        {
            MakeLand();
            var civ = _sim.Civ;
            civ.PlaceBuilding(-1, civ.BldTent, new int2(20, 20), 1f, 0); // 6x6 tiles: x 20..25

            var touching = new int2(26, 20); // the tent sits in the one-tile ring
            Assert.That(civ.FootprintFree(touching, 6, 6, -1, -1, false), Is.False);
            Assert.That(civ.FootprintFree(touching, 6, 6, -1, -1, false, ringBuildingsOk: true), Is.True);

            var overlapping = new int2(25, 20); // the tent tile x=25 is inside the footprint
            Assert.That(civ.FootprintFree(overlapping, 6, 6, -1, -1, false, ringBuildingsOk: true), Is.False);
        }

        [Test]
        public void PlannerDoesNotQueueASecondFoundationOfTheSameWorkBuilding()
        {
            int city = _sim.Civ.CreateCity(new int2(32, 32), _db.Species.IdOf("sp.human"), -1, 0, 0, 0);
            _sim.Civ.ClaimZone(city, _sim.World.ZoneIndexOf(32, 32)); // open water gives the coast requirement
            _sim.Civ.RebuildCaches(_sim.Units.Store);
            ref var data = ref _sim.Civ.City(city);
            data.HallTier = 3;
            data.Population = 10;
            data.Housing = 40;
            int[] defs = new int[CityPlanner.MaxCandidates];
            float[] scores = new float[CityPlanner.MaxCandidates];
            int hut = _sim.Civ.BldFishingHut;

            int count = CityPlanner.Candidates(_sim.Civ, _sim.Units, city, defs, scores);
            Assert.That(System.Array.IndexOf(defs, hut, 0, count), Is.GreaterThanOrEqualTo(0), "Precondition: a hungry coastal city wants a fishing hut.");

            _sim.Civ.PlaceBuilding(city, hut, new int2(10, 10), 0f, 0);
            count = CityPlanner.Candidates(_sim.Civ, _sim.Units, city, defs, scores);
            Assert.That(System.Array.IndexOf(defs, hut, 0, count), Is.LessThan(0), "The unfinished hut already covers that need.");
        }

        void LandZone0(int temperature, int moisture, int dryMonths, int drought)
        {
            MakeLand();
            var zone = _sim.World.Zones[0];
            zone.TemperatureC = (short)temperature;
            zone.Moisture = (byte)moisture;
            zone.DryMonths = (byte)dryMonths;
            zone.Drought = (byte)drought;
            _sim.World.Zones[0] = zone;
        }

        [Test]
        public void TemperateZoneWithoutWaterRecoversFromDrought()
        {
            LandZone0(temperature: 15, moisture: 20, dryMonths: 6, drought: 1);
            var system = new MoistureSystem();
            for (int month = 0; month < 120; month++) system.Tick(_sim.Context);

            var zone = _sim.World.Zones[0];
            Assert.That(zone.Moisture, Is.GreaterThanOrEqualTo(104));
            Assert.That(zone.Drought, Is.EqualTo(0));
        }

        [Test]
        public void HotDesertZoneStillDriesOut()
        {
            LandZone0(temperature: 40, moisture: 120, dryMonths: 0, drought: 0);
            var system = new MoistureSystem();
            for (int month = 0; month < 120; month++) system.Tick(_sim.Context);

            var zone = _sim.World.Zones[0];
            Assert.That(zone.Moisture, Is.LessThanOrEqualTo(64));
            Assert.That(zone.Drought, Is.EqualTo(1));
        }

        [Test]
        public void RuinsStopFeedingAFireButStandingBuildingsKeepBurning()
        {
            MakeLand();
            var civ = _sim.Civ;
            int standing = civ.PlaceBuilding(-1, civ.BldTent, new int2(10, 10), 1f, 0);
            int ruined = civ.PlaceBuilding(-1, civ.BldTent, new int2(30, 30), 1f, 0);
            civ.Ruin(ruined, 0);
            Assert.That(standing, Is.Not.EqualTo(ruined));

            _sim.Nature.Ignite(11, 11, NatureState.IgniteDefault, true);
            _sim.Nature.Ignite(31, 31, NatureState.IgniteDefault, true);
            using (var fire = new FireSystem())
                for (int step = 0; step < 150; step++) fire.Tick(_sim.Context);

            var map = _sim.World;
            Assert.That(map.Fire[map.Index(31, 31)], Is.EqualTo(0), "A ruin burns out.");
            Assert.That(map.HasFlag(31, 31, TileFlags.Burning), Is.False);
            Assert.That(map.Fire[map.Index(11, 11)], Is.GreaterThan(0), "A standing building keeps feeding the fire.");
        }
    }
}
