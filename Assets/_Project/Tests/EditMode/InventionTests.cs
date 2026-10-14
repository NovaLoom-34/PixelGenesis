using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using PG.Content;
using PG.Core;
using PG.EditorTools;
using PG.Persistence;
using PG.Sim;
using PG.World;
using Unity.Mathematics;
using UnityEngine;

namespace PG.Tests.EditMode
{
    // Tests for Kingdom Inventions, Technology Research, Land/Water Vehicles and New Tools (DECISIONS #103).
    public sealed class InventionTests
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
        public void Setup()
        {
            _sim = new SimWorld(_db, new WorldMap(64, 64, _db), new SimRandomProvider(104));
        }

        [TearDown]
        public void Teardown()
        {
            _sim.Dispose();
            _sim = null;
        }

        int CreateKingdomWithCity(int2 pos)
        {
            int species = _db.Species.IdOf("sp.human");
            int city = _sim.Civ.CreateCity(pos, species, -1, 0, _sim.Clock.Tick, 0);
            var rng = _sim.Rng.Get(RngStream.Meta);
            int k = _sim.Meta.CreateKingdom(city, species, PG.Core.EntityId.None, 0, _sim.Clock.Tick, ref rng);
            ref var cd = ref _sim.Civ.City(city);
            cd.Kingdom = k;
            cd.Loyalty = 100;
            _sim.Civ.Version++;
            return k;
        }

        [Test]
        public void LibrariesAndHallsGenerateMonthlyResearchPoints()
        {
            int k = CreateKingdomWithCity(new int2(16, 16));
            int city = _sim.Meta.Kingdom(k).Capital;

            // Give the library (3x3 cells = 9x9 tiles from 18,16) and the city land: the blank test map is deep ocean.
            for (int y = 12; y < 34; y++)
                for (int x = 12; x < 34; x++)
                    _sim.World.SetGround(x, y, (byte)_db.Tiles.IdOf("tile.soil_low"), ChangeSource.WorldGen);

            // Add library to city
            _sim.Civ.PlaceBuilding(city, _sim.Civ.BldLibrary, new int2(18, 16), 1f, 0);
            _sim.Civ.City(city).HallTier = 2;

            // Advance one full month
            long monthStart = _sim.Clock.Tick + SimConst.TicksPerMonth;
            while (_sim.Clock.Tick < monthStart) _sim.Tick();

            Assert.That(_sim.Civ.ResearchProgress.TryGetValue(k, out int prog), Is.True);
            Assert.That(prog, Is.GreaterThan(5), "Library and Hall Tier 2 must produce substantial research points");
        }

        [Test]
        public void CompletingResearchUnlocksInventionAndPublishesEvent()
        {
            int k = CreateKingdomWithCity(new int2(16, 16));
            int wheelInv = _db.Inventions.IdOf("inv.wheel");
            _sim.Civ.CurrentResearch[k] = wheelInv;

            int cost = _db.Inventions[wheelInv].Cost;
            _sim.Civ.ResearchProgress[k] = cost - 1; // 1 point away from discovery

            bool eventFired = false;
            _sim.Events.Subscribe<InventionDiscoveredEvent>(e =>
            {
                if (e.Kingdom == k && e.Invention == wheelInv) eventFired = true;
            });

            // Advance one month to trigger InventionSystem.Tick
            long monthStart = _sim.Clock.Tick + SimConst.TicksPerMonth;
            while (_sim.Clock.Tick < monthStart) _sim.Tick();

            Assert.That(_sim.Civ.HasInvention(k, wheelInv), Is.True, "Invention must be unlocked upon reaching cost");
            Assert.That(eventFired, Is.True, "InventionDiscoveredEvent must be published");
            Assert.That(_sim.Civ.CurrentResearch[k], Is.Not.EqualTo(wheelInv), "System must automatically select the next invention");
        }

        [Test]
        public void CartsAndCarsSpawnAndTravelOnLand()
        {
            int k = CreateKingdomWithCity(new int2(16, 16));
            int city = _sim.Meta.Kingdom(k).Capital;
            var rng = _sim.Rng.Get(RngStream.Civ);

            int initialVehicles = _sim.Civ.Vehicles.Length;
            VehicleSystem.SpawnCart(_sim.Context, _sim.Civ, city, k, new float2(16.5f, 16.5f), new int2(32, 16), ref rng);
            VehicleSystem.SpawnCar(_sim.Context, _sim.Civ, city, k, new float2(16.5f, 16.5f), new int2(32, 16));

            Assert.That(_sim.Civ.Vehicles.Length, Is.EqualTo(initialVehicles + 2));
            Assert.That(_sim.Civ.Vehicles[initialVehicles].Type, Is.EqualTo(VehicleType.Cart));
            Assert.That(_sim.Civ.Vehicles[initialVehicles + 1].Type, Is.EqualTo(VehicleType.Car));
            Assert.That(_sim.Civ.Vehicles[initialVehicles + 1].Speed, Is.GreaterThan(_sim.Civ.Vehicles[initialVehicles].Speed), "Car must travel faster than cart");
        }

        [Test]
        public void CraftingMusketRequiresGunpowderInvention()
        {
            int k = CreateKingdomWithCity(new int2(16, 16));
            int musket = _db.EquipmentTypes.IdOf("eq.musket");

            Assert.That(_sim.Civ.HasInvention(k, "inv.gunpowder"), Is.False);

            // Unlock gunpowder
            _sim.Civ.UnlockInvention(k, _db.Inventions.IdOf("inv.gunpowder"));
            Assert.That(_sim.Civ.HasInvention(k, "inv.gunpowder"), Is.True);
        }

        [Test]
        public void InventionsAndVehiclesRoundTripThroughSaveSection()
        {
            int k = CreateKingdomWithCity(new int2(16, 16));
            int city = _sim.Meta.Kingdom(k).Capital;
            var rng = _sim.Rng.Get(RngStream.Civ);

            int wheel = _db.Inventions.IdOf("inv.wheel");
            int sails = _db.Inventions.IdOf("inv.sails");
            _sim.Civ.UnlockInvention(k, wheel);
            _sim.Civ.UnlockInvention(k, sails);
            _sim.Civ.CurrentResearch[k] = _db.Inventions.IdOf("inv.cart");
            _sim.Civ.ResearchProgress[k] = 85;

            VehicleSystem.SpawnCart(_sim.Context, _sim.Civ, city, k, new float2(16.5f, 16.5f), new int2(32, 16), ref rng);

            // Save to Section 17 bytes
            byte[] bytes = InventionSave.Write(_sim);
            Assert.That(bytes, Is.Not.Null.And.Not.Empty);

            // Create target sim and restore
            using var targetSim = new SimWorld(_db, new WorldMap(64, 64, _db), new SimRandomProvider(99));
            int targetCity = targetSim.Civ.CreateCity(new int2(16, 16), _db.Species.IdOf("sp.human"), -1, 0, targetSim.Clock.Tick, 0);
            var targetRng = targetSim.Rng.Get(RngStream.Meta);
            int targetKingdom = targetSim.Meta.CreateKingdom(targetCity, _db.Species.IdOf("sp.human"), PG.Core.EntityId.None, 0, targetSim.Clock.Tick, ref targetRng);
            targetSim.Civ.City(targetCity).Kingdom = targetKingdom;
            targetSim.Civ.City(targetCity).Loyalty = 100;
            var warnings = new List<string>();
            InventionSave.Apply(bytes, targetSim, warnings);

            Assert.That(warnings.Count, Is.EqualTo(0));
            Assert.That(targetKingdom, Is.EqualTo(k), "The target save fixture must map the same kingdom index.");
            Assert.That(targetSim.Civ.HasInvention(targetKingdom, wheel), Is.True);
            Assert.That(targetSim.Civ.HasInvention(targetKingdom, sails), Is.True);
            Assert.That(targetSim.Civ.CurrentResearch[targetKingdom], Is.EqualTo(_db.Inventions.IdOf("inv.cart")));
            Assert.That(targetSim.Civ.ResearchProgress[targetKingdom], Is.EqualTo(85));
            Assert.That(targetSim.Civ.Vehicles.Length, Is.EqualTo(1));
            Assert.That(targetSim.Civ.Vehicles[0].Type, Is.EqualTo(VehicleType.Cart));
        }
    }
}
