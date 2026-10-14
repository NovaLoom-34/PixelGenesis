using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using PG.Content;
using PG.Core;
using PG.EditorTools;
using PG.Sim;
using PG.World;
using Unity.Mathematics;
using UnityEngine;
using EntityId = PG.Core.EntityId;

namespace PG.Tests.EditMode
{
    public sealed class SoakMetricsTests
    {
        ContentDB _db;
        SimWorld _sim;

        [OneTimeSetUp]
        public void LoadContent()
        {
            DataSync.Sync();
            _db = ContentDB.LoadAll(Path.Combine(Application.streamingAssetsPath, "Data"), null);
        }

        [TearDown]
        public void Cleanup() => _sim?.Dispose();

        [Test]
        public void InitialPopulationCensusRecordsOnlyTheSpeciesPresentBeforeTheSoak()
        {
            _sim = new SimWorld(_db, new WorldMap(64, 64, _db), new SimRandomProvider(42));
            Spawn("sp.parrot", new float2(12.5f, 12.5f), 0);
            Spawn("sp.parrot", new float2(13.5f, 12.5f), 1);
            Spawn("sp.human", new float2(14.5f, 12.5f), 0);
            var metrics = new SoakMetrics(42, "wild", 1);
            metrics.CaptureInitialPopulation(_sim);

            string path = Path.Combine(Application.temporaryCachePath, "soak-initial-census-" + System.Guid.NewGuid().ToString("N") + ".json");
            try
            {
                metrics.Write(path);
                JObject json = JObject.Parse(File.ReadAllText(path));
                JArray initial = (JArray)json["InitialPopulationBySpecies"];
                Assert.That(initial, Is.Not.Null);
                Assert.That(initial.Count, Is.EqualTo(2));
                Assert.That((string)initial[0]["Species"], Is.EqualTo("sp.human"));
                Assert.That((int)initial[0]["Population"], Is.EqualTo(1));
                Assert.That((bool)initial[0]["IsCiv"], Is.True);
                Assert.That((string)initial[1]["Species"], Is.EqualTo("sp.parrot"));
                Assert.That((int)initial[1]["Population"], Is.EqualTo(2));
                Assert.That((bool)initial[1]["IsCiv"], Is.False);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Test]
        public void VegetationCoverageCountsLivingTreeAndPlantTilesButNotDeadTrees()
        {
            _sim = new SimWorld(_db, new WorldMap(64, 64, _db), new SimRandomProvider(314));
            _sim.World.Feature[0] = _db.Features[_db.Features.IdOf("feat.tree_oak")].MapValue;
            _sim.World.Feature[1] = _db.Features[_db.Features.IdOf("feat.berry_bush")].MapValue;
            _sim.World.Feature[2] = _db.Features[_db.Features.IdOf("feat.tree_dead")].MapValue;
            var metrics = new SoakMetrics(314, "wild-community", 1);
            metrics.CaptureInitialEnvironment(_sim);
            metrics.Capture(_sim, 1, 0, 0, 0, 0, 0);

            string path = Path.Combine(Application.temporaryCachePath, "soak-vegetation-" + System.Guid.NewGuid().ToString("N") + ".json");
            try
            {
                metrics.Write(path);
                JObject json = JObject.Parse(File.ReadAllText(path));
                Assert.That((int)json["InitialVegetationTiles"], Is.EqualTo(2));
                Assert.That((float)json["InitialVegetationCoveragePercent"], Is.EqualTo(100f * 2f / 4096f).Within(1e-5f));
                Assert.That((int)json["Samples"][0]["VegetationTiles"], Is.EqualTo(2));
                Assert.That((float)json["Samples"][0]["VegetationCoveragePercent"], Is.EqualTo(100f * 2f / 4096f).Within(1e-5f));
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Test]
        public void MetricsReportPredatorAndPreyPopulationsFromContentFoodWeb()
        {
            _sim = new SimWorld(_db, new WorldMap(64, 64, _db), new SimRandomProvider(2718));
            Spawn("sp.wolf", new float2(12.5f, 12.5f), 0);
            Spawn("sp.rabbit", new float2(13.5f, 12.5f), 1);
            var metrics = new SoakMetrics(2718, "wild-community", 1);
            metrics.Capture(_sim, 1, 0, 0, 0, 0, 0);

            string path = Path.Combine(Application.temporaryCachePath, "soak-food-web-" + System.Guid.NewGuid().ToString("N") + ".json");
            try
            {
                metrics.Write(path);
                JObject json = JObject.Parse(File.ReadAllText(path));
                JObject sample = (JObject)json["Samples"][0];
                Assert.That((int)sample["PredatorPopulation"], Is.EqualTo(1));
                Assert.That((int)sample["PreyPopulation"], Is.EqualTo(2), "rabbit is herbivore prey; wolves are prey of lions and others in content");
                JArray species = (JArray)sample["PopulationBySpecies"];
                Assert.That((bool)species.Single(x => (string)x["Species"] == "sp.wolf")["IsPredator"], Is.True);
                Assert.That((bool)species.Single(x => (string)x["Species"] == "sp.rabbit")["IsPrey"], Is.True);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Test]
        public void UnitPerformanceReportSerializesTheFiveThousandUnitGate()
        {
            var report = new UnitPerformanceReport
            {
                TargetPopulation = 5000,
                PopulationAfterWarmup = 5000,
                MinimumMeasuredPopulation = 4980,
                MaximumMeasuredPopulation = 5010,
                WarmupTicks = 200,
                MeasuredTicks = 600,
                AverageUnitSystemsMillisecondsPerTick = 0.42,
                WorstUnitSystemsMillisecondsPerTick = 0.49,
                LimitMillisecondsPerTick = 0.5,
                Passed = true,
                Processor = "test processor",
            };
            JObject json = JObject.Parse(UnityEngine.JsonUtility.ToJson(report));
            Assert.That((int)json["TargetPopulation"], Is.EqualTo(5000));
            Assert.That((int)json["MeasuredTicks"], Is.EqualTo(600));
            Assert.That((double)json["AverageUnitSystemsMillisecondsPerTick"], Is.EqualTo(0.42).Within(1e-8));
            Assert.That((double)json["LimitMillisecondsPerTick"], Is.EqualTo(0.5).Within(1e-8));
            Assert.That((bool)json["Passed"], Is.True);
        }

        [Test]
        public void MetricsJsonKeepsTheFieldsConsumedByComparisonAndGateTools()
        {
            var report = new SoakMetricsReport
            {
                Seed = "42",
                Scenario = "multi",
                RequestedYears = 200,
                Samples = new System.Collections.Generic.List<SoakYearMetrics>
                {
                    new SoakYearMetrics
                    {
                        Year = 1, Population = 40, WildPopulation = 18, PredatorPopulation = 6, PreyPopulation = 9, MaxWildSpeciesPopulation = 18,
                        FamineEvents = 2, ClimateLinkedFamineEvents = 1, LivingKingdoms = 4,
                        LongestActiveWarYears = 3, LargestKingdomPopulationShare = 0.65f,
                        TradeDeliveries = 4, TradeAmount = 120, SeaTradeDeliveries = 1,
                        AcceptedTreaties = 2, VassalagesFormed = 1, Rebellions = 1,
                        KingdomPopulations = new System.Collections.Generic.List<SoakKingdomPopulationMetrics>
                        {
                            new SoakKingdomPopulationMetrics { KingdomId = 0, Population = 26, WarsParticipated = 2, PopulationShare = 0.65f, Alive = true },
                            new SoakKingdomPopulationMetrics { KingdomId = 1, Population = 0, WarsParticipated = 1, PopulationShare = 0f, Alive = false, Fell = true },
                        },
                        PopulationBySpecies = new System.Collections.Generic.List<SpeciesPopulationMetrics>
                        {
                            new SpeciesPopulationMetrics { Species = "sp.dwarf", IsCiv = true, Population = 10, Female = 5, Male = 5, Adults = 8, Juveniles = 2 },
                            new SpeciesPopulationMetrics { Species = "sp.elf", IsCiv = true, Population = 10, Female = 5, Male = 5, Adults = 8, Juveniles = 2 },
                            new SpeciesPopulationMetrics
                            {
                                Species = "sp.human", IsCiv = true, Population = 10, Female = 5, Male = 5, Adults = 8, Juveniles = 2,
                                Births = 3, Deaths = 2,
                                DeathsByCause = new System.Collections.Generic.List<SpeciesDeathCauseMetrics>
                                {
                                    new SpeciesDeathCauseMetrics { Cause = "Starved", Count = 2 },
                                },
                            },
                            new SpeciesPopulationMetrics { Species = "sp.orc", IsCiv = true, Population = 10, Female = 5, Male = 5, Adults = 8, Juveniles = 2 },
                            new SpeciesPopulationMetrics { Species = "sp.parrot", Population = 18, Female = 9, Male = 9, Adults = 12, Juveniles = 6, IsPrey = true },
                        },
                    },
                },
                Summary = new SoakSummaryMetrics
                {
                    FoundedCities = 4, MaxHallTier = 2, MaxHumanPopulation = 10, ClimateLinkedFamineEvents = 1,
                    MaxLivingKingdoms = 4, FamineEvents = 1, DeclaredWars = 2,
                    MaxContinuousWarPairYears = 3, MaxLargestKingdomPopulationShare = 0.65f,
                    AcceptedTreaties = 2, VassalagesFormed = 1, Rebellions = 1,
                    TradeDeliveries = 4, SeaTradeDeliveries = 1, TradeAmount = 120,
                    PeaceEvents = 1, CraftedItems = 3, CivMillisecondsPerTick = 0.4, ClimateMillisecondsPerTick = 0.2,
                    ClimateMillisecondsPerMonth = 0.25,
                    MetaMillisecondsPerTick = 0.1,
                    TotalMillisecondsPerTick = 0.8,
                },
            };

            JObject json = JObject.Parse(UnityEngine.JsonUtility.ToJson(report));
            Assert.That((string)json["Seed"], Is.EqualTo("42"));
            Assert.That((string)json["Scenario"], Is.EqualTo("multi"));
            Assert.That((int)json["RequestedYears"], Is.EqualTo(200));
            Assert.That(((JArray)json["Samples"][0]["PopulationBySpecies"]).Count, Is.EqualTo(5));
            Assert.That((int)json["Samples"][0]["LivingKingdoms"], Is.EqualTo(4));
            Assert.That((int)json["Samples"][0]["WildPopulation"], Is.EqualTo(18));
            Assert.That((int)json["Samples"][0]["PredatorPopulation"], Is.EqualTo(6));
            Assert.That((int)json["Samples"][0]["PreyPopulation"], Is.EqualTo(9));
            Assert.That((int)json["Samples"][0]["MaxWildSpeciesPopulation"], Is.EqualTo(18));
            Assert.That((int)json["Samples"][0]["ClimateLinkedFamineEvents"], Is.EqualTo(1));
            Assert.That((int)json["Samples"][0]["LongestActiveWarYears"], Is.EqualTo(3));
            Assert.That((float)json["Samples"][0]["LargestKingdomPopulationShare"], Is.EqualTo(0.65f));
            Assert.That((int)json["Samples"][0]["TradeDeliveries"], Is.EqualTo(4));
            Assert.That((int)json["Samples"][0]["KingdomPopulations"][0]["WarsParticipated"], Is.EqualTo(2));
            Assert.That((bool)json["Samples"][0]["KingdomPopulations"][1]["Fell"], Is.True);
            Assert.That((bool)json["Samples"][0]["PopulationBySpecies"][0]["IsCiv"], Is.True);
            Assert.That((bool)json["Samples"][0]["PopulationBySpecies"][4]["IsPrey"], Is.True);
            Assert.That((int)json["Samples"][0]["PopulationBySpecies"][4]["Female"], Is.EqualTo(9));
            Assert.That((int)json["Samples"][0]["PopulationBySpecies"][4]["Male"], Is.EqualTo(9));
            Assert.That((int)json["Samples"][0]["PopulationBySpecies"][4]["Adults"], Is.EqualTo(12));
            Assert.That((int)json["Samples"][0]["PopulationBySpecies"][4]["Juveniles"], Is.EqualTo(6));
            Assert.That((int)json["Samples"][0]["PopulationBySpecies"][2]["Births"], Is.EqualTo(3));
            Assert.That((int)json["Samples"][0]["PopulationBySpecies"][2]["Deaths"], Is.EqualTo(2));
            Assert.That((string)json["Samples"][0]["PopulationBySpecies"][2]["DeathsByCause"][0]["Cause"], Is.EqualTo("Starved"));
            Assert.That((int)json["Samples"][0]["PopulationBySpecies"][2]["DeathsByCause"][0]["Count"], Is.EqualTo(2));

            var summary = (JObject)json["Summary"];
            foreach (string field in new[]
            {
                "FoundedCities", "MaxHallTier", "MaxHumanPopulation", "MaxLivingKingdoms",
                "FamineEvents", "ClimateLinkedFamineEvents", "DeclaredWars", "PeaceEvents", "CraftedItems",
                "CivMillisecondsPerTick", "ClimateMillisecondsPerTick", "ClimateMillisecondsPerMonth", "TotalMillisecondsPerTick",
                "MaxContinuousWarPairYears", "MaxLargestKingdomPopulationShare", "AcceptedTreaties", "VassalagesFormed", "Rebellions",
                "TradeDeliveries", "SeaTradeDeliveries", "TradeAmount", "MetaMillisecondsPerTick",
            })
                Assert.That(summary[field], Is.Not.Null, $"metrics_compare.py requires Summary.{field}");
            Assert.That((double)summary["ClimateMillisecondsPerTick"], Is.EqualTo(0.2).Within(1e-8));
            Assert.That((double)summary["ClimateMillisecondsPerMonth"], Is.EqualTo(0.25).Within(1e-8));
            Assert.That((double)summary["MetaMillisecondsPerTick"], Is.EqualTo(0.1).Within(1e-8));
            Assert.That((int)summary["TradeAmount"], Is.EqualTo(120));
        }

        [Test]
        public void MetricsJsonRoundTripsYearlySpeciesAndCityDetails()
        {
            var original = new SoakMetricsReport
            {
                Seed = "2024", Scenario = "single", RequestedYears = 1,
                Samples = new System.Collections.Generic.List<SoakYearMetrics>
                {
                    new SoakYearMetrics
                    {
                        Year = 1, Population = 12, TotalGold = 50,
                        MarketPriceSampleCount = 12, AverageMarketPriceDeviation = 0.18f,
                        GoldFlow = new SoakGoldFlowMetrics
                        {
                            Income = 30, Expenses = 5, TransfersIn = 4, TransfersOut = 4, RouteFees = 1, NetChange = 24,
                            MaintenanceDue = 6, MaintenancePaid = 5, MaintenanceUnpaid = 1,
                        },
                        PopulationBySpecies = new System.Collections.Generic.List<SpeciesPopulationMetrics>
                        {
                            new SpeciesPopulationMetrics { Species = "sp.human", Population = 12 },
                        },
                        CitiesById = new System.Collections.Generic.List<SoakCityMetrics>
                        {
                            new SoakCityMetrics
                            {
                                CityId = 0, Population = 12, Housing = 16,
                            NextHallAvailable = true, NextHallNeed = "bld.hall_2", NextHallAffordable = false, NextHallHasSite = true,
                            NextHallMissingResources = new System.Collections.Generic.List<SoakCountMetric>
                            {
                                new SoakCountMetric { Id = "res.stone", Count = 12 },
                            },
                                Gold = 50,
                                GoldFlow = new SoakGoldFlowMetrics
                                {
                                    Income = 30, Expenses = 5, TransfersIn = 4, TransfersOut = 4, RouteFees = 1, NetChange = 24,
                                    MaintenanceDue = 6, MaintenancePaid = 5, MaintenanceUnpaid = 1,
                                },
                                FoodFlow = new SoakFoodFlowMetrics { AddedToStock = 20, EatenFromStock = 8 },
                            },
                        },
                    },
                },
            };

            var restored = UnityEngine.JsonUtility.FromJson<SoakMetricsReport>(UnityEngine.JsonUtility.ToJson(original));
            Assert.That(restored.Seed, Is.EqualTo(original.Seed));
            Assert.That(restored.Samples[0].PopulationBySpecies[0].Species, Is.EqualTo("sp.human"));
            Assert.That(restored.Samples[0].MarketPriceSampleCount, Is.EqualTo(12));
            Assert.That(restored.Samples[0].AverageMarketPriceDeviation, Is.EqualTo(0.18f).Within(1e-5f));
            Assert.That(restored.Samples[0].TotalGold, Is.EqualTo(50));
            Assert.That(restored.Samples[0].GoldFlow.Income, Is.EqualTo(30));
            Assert.That(restored.Samples[0].GoldFlow.NetChange, Is.EqualTo(24));
            Assert.That(restored.Samples[0].GoldFlow.MaintenanceDue, Is.EqualTo(6));
            Assert.That(restored.Samples[0].GoldFlow.MaintenanceUnpaid, Is.EqualTo(1));
            Assert.That(restored.Samples[0].CitiesById[0].GoldFlow.TransfersIn, Is.EqualTo(4));
            Assert.That(restored.Samples[0].CitiesById[0].FoodFlow.AddedToStock, Is.EqualTo(20));
            Assert.That(restored.Samples[0].CitiesById[0].FoodFlow.EatenFromStock, Is.EqualTo(8));
            Assert.That(restored.Samples[0].CitiesById[0].NextHallAvailable, Is.True);
            Assert.That(restored.Samples[0].CitiesById[0].NextHallNeed, Is.EqualTo("bld.hall_2"));
            Assert.That(restored.Samples[0].CitiesById[0].NextHallAffordable, Is.False);
            Assert.That(restored.Samples[0].CitiesById[0].NextHallHasSite, Is.True);
            Assert.That(restored.Samples[0].CitiesById[0].NextHallMissingResources[0].Id, Is.EqualTo("res.stone"));
            Assert.That(restored.Samples[0].CitiesById[0].NextHallMissingResources[0].Count, Is.EqualTo(12));
        }

        void Spawn(string speciesId, float2 position, byte sex)
        {
            int species = _db.Species.IdOf(speciesId);
            var rng = _sim.Rng.Get(RngStream.UnitAI);
            _sim.Units.Spawn(new UnitSpawnRequest
            {
                Species = species,
                AgeYears = 3f,
                Sex = sex,
                Pos = position,
                Subspecies = -1,
                Mother = EntityId.None,
                Father = EntityId.None,
            }, _sim.Clock.Tick, ref rng);
        }
    }
}
