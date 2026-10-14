using System.IO;
using NUnit.Framework;
using PG.Content;
using PG.EditorTools;
using PG.Sim;
using PG.World;
using Unity.Mathematics;
using UnityEngine;

namespace PG.Tests.EditMode
{
    public sealed class CityGrowthTests
    {
        ContentDB _db;
        SimWorld _sim;

        [OneTimeSetUp]
        public void LoadContent()
        {
            DataSync.Sync();
            _db = ContentDB.LoadAll(Path.Combine(Application.streamingAssetsPath, "Data"), null);
        }

        [SetUp]
        public void Setup() => _sim = new SimWorld(_db, new WorldMap(64, 64, _db), new PG.Core.SimRandomProvider(3402));

        [TearDown]
        public void Teardown() => _sim?.Dispose();

        [Test]
        public void PlannerKeepsHousingCandidateWhileSmallReserveIsMissing()
        {
            int city = _sim.Civ.CreateCity(new int2(32, 32), _db.Species.IdOf("sp.human"), -1, 0, 0, 0);
            ref var data = ref _sim.Civ.City(city);
            data.HallTier = 3; // isolate house demand from hall upgrades
            data.Population = 10;
            data.Housing = 10;

            int[] defs = new int[CityPlanner.MaxCandidates];
            float[] scores = new float[CityPlanner.MaxCandidates];
            int count = CityPlanner.Candidates(_sim.Civ, _sim.Units, city, defs, scores);

            Assert.That(Contains(defs, count, _sim.Civ.BldTent), Is.True,
                "The planner should start housing before a small village runs out of beds.");

            data.Housing = data.Population + 4;
            count = CityPlanner.Candidates(_sim.Civ, _sim.Units, city, defs, scores);
            Assert.That(Contains(defs, count, _sim.Civ.BldTent), Is.False,
                "The minimum four-bed reserve should stop being urgent once it is satisfied.");
        }

        [Test]
        public void CrowdedCityCanClaimAnAdjacentFreeZone()
        {
            byte soil = (byte)_db.Tiles.IdOf("tile.soil_low");
            for (int y = 0; y < _sim.World.Height; y++)
                for (int x = 0; x < _sim.World.Width; x++)
                    _sim.World.SetGround(x, y, soil, ChangeSource.WorldGen);
            _sim.World.RebuildDerivedData();

            int city = _sim.Civ.CreateCity(new int2(32, 32), _db.Species.IdOf("sp.human"), -1, 0, 0, 0);
            _sim.Civ.ClaimZone(city, _sim.World.ZoneIndexOf(32, 32));
            _sim.Civ.RebuildCaches(_sim.Units.Store);
            int before = _sim.Civ.CityZones[city].Count;

            bool grew = SettlementSystem.GrowZone(_sim.Civ, _sim.Units, city);

            Assert.That(grew, Is.True);
            Assert.That(_sim.Civ.CityZones[city].Count, Is.EqualTo(before + 1));
        }

        static bool Contains(int[] values, int count, int value)
        {
            for (int i = 0; i < count; i++) if (values[i] == value) return true;
            return false;
        }
    }
}
