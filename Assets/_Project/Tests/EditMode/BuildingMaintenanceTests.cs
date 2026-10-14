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
    public sealed class BuildingMaintenanceTests
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
        public void Setup() => _sim = new SimWorld(_db, new WorldMap(64, 64, _db), new SimRandomProvider(4407));

        [TearDown]
        public void Teardown() => _sim?.Dispose();

        [Test]
        public void MaintenanceIsConfiguredForUsefulBuildingsAndZeroForRuins()
        {
            Assert.That(_db.Buildings[_db.Buildings.IdOf("bld.hut")].GoldUpkeepPerYear, Is.GreaterThan(0));
            Assert.That(_db.Buildings[_db.Buildings.IdOf("bld.grand_temple")].GoldUpkeepPerYear,
                Is.GreaterThan(_db.Buildings[_db.Buildings.IdOf("bld.hut")].GoldUpkeepPerYear));
            Assert.That(_db.Buildings[_db.Buildings.IdOf("bld.ruins")].GoldUpkeepPerYear, Is.Zero);
        }

        [Test]
        public void AnnualMaintenanceOnlyCountsCompletedLivingBuildings()
        {
            int city = _sim.Civ.CreateCity(new int2(32, 32), _db.Species.IdOf("sp.human"), -1, 0, 0, 0);
            int hut = _sim.Civ.PlaceBuilding(city, _db.Buildings.IdOf("bld.hut"), new int2(5, 5), 1f, 0);
            _sim.Civ.PlaceBuilding(city, _db.Buildings.IdOf("bld.house"), new int2(15, 5), 0.5f, 0);
            int hall = _sim.Civ.PlaceBuilding(city, _db.Buildings.IdOf("bld.hall_2"), new int2(30, 5), 1f, 0);
            var destroyed = _sim.Civ.Buildings[hall]; destroyed.Flags = BuildingFlags.Dead; _sim.Civ.Buildings[hall] = destroyed;
            int temple = _sim.Civ.PlaceBuilding(city, _db.Buildings.IdOf("bld.temple"), new int2(45, 5), 1f, 0);
            var ruined = _sim.Civ.Buildings[temple]; ruined.Flags = BuildingFlags.Ruin; _sim.Civ.Buildings[temple] = ruined;

            long due = CityEconomySystem.AnnualMaintenanceDue(_sim.Civ, city);

            Assert.That(due, Is.EqualTo(_db.Buildings[_sim.Civ.Buildings[hut].Def].GoldUpkeepPerYear));
        }

        [Test]
        public void MaintenancePaymentNeverCreatesDebtAndReportsUnpaidAmount()
        {
            int city = _sim.Civ.CreateCity(new int2(32, 32), _db.Species.IdOf("sp.human"), -1, 0, 0, 0);
            _sim.Civ.City(city).Gold = 7;

            long paid = CityEconomySystem.PayAnnualMaintenance(_sim.Civ, city, 12);

            Assert.That(paid, Is.EqualTo(7));
            Assert.That(_sim.Civ.City(city).Gold, Is.Zero);
            Assert.That(_sim.Civ.CityGoldFlows[city].Expenses, Is.EqualTo(7));
            Assert.That(_sim.Civ.CityGoldFlows[city].MaintenanceDue, Is.EqualTo(12));
            Assert.That(_sim.Civ.CityGoldFlows[city].MaintenanceUnpaid, Is.EqualTo(5));
        }

        [Test]
        public void AnnualMaintenanceRunsAtTheStartOfTheLastMonthOnly()
        {
            long collectionTick = SimConst.TicksPerYear - SimConst.TicksPerMonth + 1;
            Assert.That(CityEconomySystem.IsMaintenanceCollectionTick(collectionTick), Is.True);
            Assert.That(CityEconomySystem.IsMaintenanceCollectionTick(collectionTick + SimConst.TicksPerYear), Is.True);
            Assert.That(CityEconomySystem.IsMaintenanceCollectionTick(1), Is.False);
            Assert.That(CityEconomySystem.IsMaintenanceCollectionTick(collectionTick + 1), Is.False);
        }
    }
}
