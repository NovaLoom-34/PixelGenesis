using System;
using System.IO;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using PG.Content;
using PG.EditorTools;
using PG.Sim;
using PG.World;
using Unity.Mathematics;
using UnityEngine;

namespace PG.Tests.EditMode
{
    public sealed class FoodFlowTests
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
            _sim = new SimWorld(_db, new WorldMap(64, 64, _db), new PG.Core.SimRandomProvider(3401));
        }

        [TearDown]
        public void Teardown() => _sim?.Dispose();

        [Test]
        public void FoodFlowSeparatesAcceptedStockFromStorageOverflow()
        {
            int city = _sim.Civ.CreateCity(new int2(32, 32), _db.Species.IdOf("sp.human"), -1, 0, 0, 0);
            _sim.Civ.City(city).StorageCap = 100;

            int acceptedFirst = _sim.Civ.AddRes(city, _sim.Civ.ResFish, 90);
            int acceptedSecond = _sim.Civ.AddRes(city, _sim.Civ.ResFish, 20);

            Assert.That(acceptedFirst, Is.EqualTo(90));
            Assert.That(acceptedSecond, Is.EqualTo(10));
            Assert.That(_sim.Civ.FoodTotal(city), Is.EqualTo(100));
            Assert.That(_sim.Civ.CityFoodFlows[city].AddedToStock, Is.EqualTo(100),
                "accepted stock must exclude food that could not fit");
            Assert.That(_sim.Civ.CityFoodFlows[city].Overflow, Is.EqualTo(10));
            Assert.That(_sim.Civ.City(city).FoodProducedYear, Is.EqualTo(100));
            Assert.That(_sim.Civ.StatFoodIn, Is.EqualTo(100));
        }

        [Test]
        public void YearlySoakMetricsExposeFoodOverflowSeparately()
        {
            int city = _sim.Civ.CreateCity(new int2(32, 32), _db.Species.IdOf("sp.human"), -1, 0, 0, 0);
            _sim.Civ.City(city).StorageCap = 100;
            _sim.Civ.AddRes(city, _sim.Civ.ResFish, 90);
            _sim.Civ.AddRes(city, _sim.Civ.ResFish, 20);

            var metrics = new SoakMetrics(3401, "food-flow", 1);
            metrics.Capture(_sim, 1, 0, 0, 0, 0, 0);
            string path = Path.Combine(Path.GetTempPath(), "pg-food-flow-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                metrics.Write(path);
                JObject root = JObject.Parse(File.ReadAllText(path));
                var flow = root["Samples"][0]["CitiesById"][0]["FoodFlow"];
                Assert.That((int)flow["AddedToStock"], Is.EqualTo(100));
                Assert.That((int)flow["Overflow"], Is.EqualTo(10));
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }
    }
}
