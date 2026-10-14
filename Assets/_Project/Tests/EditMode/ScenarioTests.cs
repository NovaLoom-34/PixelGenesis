using System;
using System.IO;
using NUnit.Framework;
using PG.Content;
using PG.Core;
using PG.Sim;
using PG.World;
using PG.WorldGen;
using UnityEngine;

namespace PG.Tests.EditMode
{
    public sealed class ScenarioTests
    {
        ContentDB _db;
        SimWorld _sim;

        [SetUp]
        public void SetUp()
        {
            _db = ContentDB.LoadAll(Path.Combine(Application.streamingAssetsPath, "Data"), null);
            _sim = new SimWorld(_db, new WorldMap(64, 64, _db), new SimRandomProvider(81503));
        }

        [TearDown]
        public void TearDown() => _sim?.Dispose();

        [Test]
        public void CatalogContainsPlayableScenariosWithObjectives()
        {
            Assert.That(_db.Scenarios.Count, Is.GreaterThanOrEqualTo(8));
            foreach (var scenario in _db.Scenarios.All)
            {
                Assert.That(scenario.Seed, Is.GreaterThan(0UL), scenario.Id);
                Assert.That(scenario.Goals, Is.Not.Empty, scenario.Id);
                Assert.That(scenario.TemplateIndex, Is.GreaterThanOrEqualTo(0), scenario.Id);
            }
        }

        [Test]
        public void SandboxCivilizationStartCreatesClusteredAdultPairsForEachCivSpecies()
        {
            var map = WorldGenerator.Generate(new WorldGenSettings { Seed = 81723, Size = MapSizePreset.Tiny }, _db);
            using (var sim = new SimWorld(_db, map, new SimRandomProvider(81723)))
            {
                int expectedSpecies = 0;
                for (int species = 0; species < _db.Species.Count; species++)
                    if (_db.Species[species].IsCiv) expectedSpecies++;

                int spawned = CivStartSpawner.SpawnStartingPopulation(sim);
                Assert.That(spawned, Is.EqualTo(expectedSpecies * CivStartSpawner.InitialPopulationPerSpecies));
                for (int species = 0; species < _db.Species.Count; species++)
                {
                    if (!_db.Species[species].IsCiv) continue;
                    int count = 0, female = 0, male = 0;
                    for (int k = 0; k < sim.Units.Store.Alive.Length; k++)
                    {
                        int unit = sim.Units.Store.Alive[k];
                        if (sim.Units.Store.Species[unit] != species) continue;
                        count++;
                        Assert.That((AgeStage)sim.Units.Store.Age[unit], Is.EqualTo(AgeStage.Adult));
                        if (sim.Units.Store.Sex[unit] == 0) female++;
                        else if (sim.Units.Store.Sex[unit] == 1) male++;
                    }
                    Assert.That(count, Is.EqualTo(CivStartSpawner.InitialPopulationPerSpecies));
                    Assert.That(female, Is.EqualTo(male));
                }

                var repeatedMap = WorldGenerator.Generate(new WorldGenSettings { Seed = 81723, Size = MapSizePreset.Tiny }, _db);
                using (var repeated = new SimWorld(_db, repeatedMap, new SimRandomProvider(81723)))
                {
                    Assert.That(CivStartSpawner.SpawnStartingPopulation(repeated), Is.EqualTo(spawned));
                    Assert.That(repeated.Units.Store.Alive.Length, Is.EqualTo(sim.Units.Store.Alive.Length));
                    for (int i = 0; i < sim.Units.Store.Alive.Length; i++)
                    {
                        int first = sim.Units.Store.Alive[i], second = repeated.Units.Store.Alive[i];
                        Assert.That(repeated.Units.Store.Species[second], Is.EqualTo(sim.Units.Store.Species[first]));
                        Assert.That(repeated.Units.Store.Sex[second], Is.EqualTo(sim.Units.Store.Sex[first]));
                        Assert.That(repeated.Units.Store.Pos[second].x, Is.EqualTo(sim.Units.Store.Pos[first].x));
                        Assert.That(repeated.Units.Store.Pos[second].y, Is.EqualTo(sim.Units.Store.Pos[first].y));
                    }
                }
            }
        }

        [Test]
        public void ScenarioEvaluationIsReadOnlyAndSandboxRemainsInactive()
        {
            var sandbox = ScenarioRules.Evaluate(_sim, _db.Scenarios[0]);
            Assert.That(sandbox.Status, Is.EqualTo(ScenarioStatus.Sandbox));
            Assert.That(_sim.Units.Store.Count, Is.Zero);

            _sim.ScenarioId = "scenario.new_species";
            _sim.ScenarioStartYear = 0;
            var first = ScenarioRules.Evaluate(_sim, _db.Scenarios[3]);
            var second = ScenarioRules.Evaluate(_sim, _db.Scenarios[3]);
            Assert.That(first.Status, Is.EqualTo(ScenarioStatus.Active));
            Assert.That(first.Goals[0].Current, Is.EqualTo(second.Goals[0].Current));
            Assert.That(_sim.Units.Store.Count, Is.Zero);
        }

        [Test]
        public void RestrictedScenarioOnlyAllowsDeclaredPowers()
        {
            var scenario = _db.Scenarios[0];
            Assert.That(ScenarioRules.CanUsePower(scenario, "pw.fertile_rain"), Is.True);
            Assert.That(ScenarioRules.CanUsePower(scenario, "pw.meteor"), Is.False);
            Assert.That(ScenarioRules.CanUsePower(_db.Scenarios[2], "pw.meteor"), Is.True);
        }

        [Test]
        public void ScenarioFailureConditionEndsTheChallengeAtItsLimit()
        {
            var scenario = _db.Scenarios[3];
            _sim.ScenarioId = scenario.Id;
            _sim.ScenarioStartYear = 0;
            _sim.Clock.SetTick(100L * SimConst.TicksPerYear);

            var failure = ScenarioRules.EvaluateCurrent(_sim, scenario);
            Assert.That(failure.Status, Is.EqualTo(ScenarioStatus.Lost));
            _sim.ScenarioOutcome = failure.Status;
            _sim.Clock.SetTick(0);
            Assert.That(ScenarioRules.Evaluate(_sim, scenario).Status, Is.EqualTo(ScenarioStatus.Lost));
        }

        [Test]
        public void ScenarioEvaluatorRespectsGoalAndFailureBoundariesAndKeepsTerminalResult()
        {
            var scenario = new ScenarioDef
            {
                Id = "scenario.evaluator_test",
                Goals = new[] { new ScenarioConditionDef { Metric = "elapsedYears", Operator = "atLeast", Target = 3, Text = "ui.test.goal" } },
                Failures = new[] { new ScenarioConditionDef { Metric = "elapsedYears", Operator = "atLeast", Target = 5, Text = "ui.test.failure" } },
            };
            _sim.ScenarioId = scenario.Id;
            _sim.ScenarioStartYear = 10;
            _sim.Clock.SetTick(12L * SimConst.TicksPerYear);
            Assert.That(ScenarioRules.EvaluateCurrent(_sim, scenario).Status, Is.EqualTo(ScenarioStatus.Active));

            _sim.Clock.SetTick(13L * SimConst.TicksPerYear);
            var won = ScenarioRules.EvaluateCurrent(_sim, scenario);
            Assert.That(won.Status, Is.EqualTo(ScenarioStatus.Won));
            Assert.That(won.Goals[0].Complete, Is.True);
            Assert.That(won.Failures[0].Complete, Is.False);
            _sim.ScenarioOutcome = won.Status;

            _sim.Clock.SetTick(15L * SimConst.TicksPerYear);
            Assert.That(ScenarioRules.Evaluate(_sim, scenario).Status, Is.EqualTo(ScenarioStatus.Won),
                "a recorded terminal result must survive later metric changes");
            _sim.ScenarioOutcome = ScenarioStatus.Active;
            var lost = ScenarioRules.EvaluateCurrent(_sim, scenario);
            Assert.That(lost.Status, Is.EqualTo(ScenarioStatus.Lost));
            Assert.That(lost.Failures[0].Complete, Is.True);
        }

        [Test]
        public void EveryCatalogScenarioUsesSupportedMetricsAndValidComparisons()
        {
            foreach (var scenario in _db.Scenarios.All)
            {
                Assert.That(scenario.Goals, Is.Not.Empty, scenario.Id);
                foreach (var condition in scenario.Goals)
                {
                    Assert.That(ScenarioMetric.IsSupported(condition.Metric), Is.True, scenario.Id + ": " + condition.Metric);
                    Assert.That(condition.Operator == "atLeast" || condition.Operator == "atMost", Is.True, scenario.Id);
                    Assert.That(condition.Target, Is.GreaterThanOrEqualTo(0), scenario.Id);
                }
                foreach (var condition in scenario.Failures)
                {
                    Assert.That(ScenarioMetric.IsSupported(condition.Metric), Is.True, scenario.Id + ": " + condition.Metric);
                    Assert.That(condition.Operator == "atLeast" || condition.Operator == "atMost", Is.True, scenario.Id);
                    Assert.That(condition.Target, Is.GreaterThanOrEqualTo(0), scenario.Id);
                }
            }
        }

        [Test]
        public void EveryCatalogScenarioEvaluatesAsWonWhenGoalMetricsAreSatisfied()
        {
            foreach (var scenario in _db.Scenarios.All)
            {
                using (var sim = NewScenarioWorld(scenario))
                {
                    KeepFailureConditionsFalse(sim, scenario);
                    foreach (var goal in scenario.Goals)
                        SetMetricAtLeast(sim, scenario, goal.Metric, goal.Target);

                    var result = ScenarioRules.EvaluateCurrent(sim, scenario);
                    Assert.That(result.Status, Is.EqualTo(ScenarioStatus.Won), scenario.Id);
                    for (int i = 0; i < result.Goals.Length; i++)
                        Assert.That(result.Goals[i].Complete, Is.True, scenario.Id + ": goal " + i);
                    for (int i = 0; i < result.Failures.Length; i++)
                        Assert.That(result.Failures[i].Complete, Is.False, scenario.Id + ": failure " + i);
                }
            }
        }

        [Test]
        public void EveryCatalogScenarioEvaluatesAsLostWhenFailureMetricIsSatisfied()
        {
            foreach (var scenario in _db.Scenarios.All)
            {
                Assert.That(scenario.Failures, Is.Not.Empty, scenario.Id);
                foreach (var failure in scenario.Failures)
                {
                    using (var sim = NewScenarioWorld(scenario))
                    {
                        sim.ScenarioId = scenario.Id;
                        sim.ScenarioStartYear = 0;
                        sim.ScenarioOutcome = ScenarioStatus.Active;
                        SetFailureCondition(sim, scenario, failure.Metric, failure.Operator, failure.Target);

                        var result = ScenarioRules.EvaluateCurrent(sim, scenario);
                        Assert.That(result.Status, Is.EqualTo(ScenarioStatus.Lost), scenario.Id + ": " + failure.Metric);
                        Assert.That(result.Failures, Has.Some.Matches<ScenarioConditionProgress>(p => p.Complete), scenario.Id);
                    }
                }
            }
        }

        SimWorld NewScenarioWorld(ScenarioDef scenario)
        {
            var sim = new SimWorld(_db, new WorldMap(64, 64, _db), new SimRandomProvider(scenario.Seed));
            sim.ScenarioId = scenario.Id;
            sim.ScenarioStartYear = 0;
            sim.ScenarioOutcome = ScenarioStatus.Active;
            return sim;
        }

        void KeepFailureConditionsFalse(SimWorld sim, ScenarioDef scenario)
        {
            foreach (var failure in scenario.Failures)
            {
                if (failure.Operator == "atMost") SetMetricAtLeast(sim, scenario, failure.Metric, failure.Target + 1);
                else if (failure.Metric == "elapsedYears") SetMetricAtLeast(sim, scenario, failure.Metric, 0);
            }
        }

        void SetFailureCondition(SimWorld sim, ScenarioDef scenario, string metric, string op, int target)
        {
            if (op == "atMost")
            {
                // A fresh scenario world already reads zero for each catalog atMost failure (currently no people).
                return;
            }
            SetMetricAtLeast(sim, scenario, metric, target);
        }

        void SetMetricAtLeast(SimWorld sim, ScenarioDef scenario, string metric, int target)
        {
            int current = ScenarioMetric.Read(metric, sim, sim.ScenarioStartYear);
            int missing = Math.Max(0, target - current);
            if (missing == 0) return;
            switch (metric)
            {
                case "elapsedYears":
                    sim.Clock.SetTick((long)(sim.ScenarioStartYear + target) * SimConst.TicksPerYear);
                    return;
                case "livingPopulation":
                    SpawnCivilizedPopulation(sim, scenario, missing);
                    return;
                case "livingKingdoms":
                    for (int i = 0; i < missing; i++) sim.Meta.Kingdoms.Create(default(KingdomData), 0, sim.Clock.Tick);
                    return;
                case "livingCities":
                    for (int i = 0; i < missing; i++)
                        sim.Civ.CreateCity(new Unity.Mathematics.int2(32, 32), scenario.SpeciesIndex, -1, 0, sim.Clock.Tick, 0);
                    return;
                case "alliances":
                    for (int i = 0; i < missing; i++) sim.Meta.Alliances.Create(default(AllianceData), 0, sim.Clock.Tick);
                    return;
                case "subspecies":
                    EnsureSubspecies(sim, scenario, target);
                    return;
                default:
                    Assert.Fail("Scenario metric has no test fixture: " + metric);
                    return;
            }
        }

        static void SpawnCivilizedPopulation(SimWorld sim, ScenarioDef scenario, int count)
        {
            Assert.That(scenario.SpeciesIndex, Is.GreaterThanOrEqualTo(0), scenario.Id + " needs a civilized starting species");
            var rng = SimRandom.Derive(sim.Seed, unchecked((int)0x5343454E4152494FUL), scenario.Index);
            for (int i = 0; i < count; i++)
            {
                sim.Units.Spawn(new UnitSpawnRequest
                {
                    Species = scenario.SpeciesIndex,
                    Pos = new Unity.Mathematics.float2(32.5f, 32.5f),
                    AgeYears = 18f,
                    Sex = (byte)(i & 1),
                    Mother = PG.Core.EntityId.None,
                    Father = PG.Core.EntityId.None,
                    Subspecies = -1,
                }, sim.Clock.Tick, ref rng, true);
            }
        }

        static void EnsureSubspecies(SimWorld sim, ScenarioDef scenario, int target)
        {
            Assert.That(scenario.SpeciesIndex, Is.GreaterThanOrEqualTo(0), scenario.Id + " needs a starting species");
            var rng = SimRandom.Derive(sim.Seed, 0x53554253, scenario.Index);
            int root = -1;
            for (int i = 0; i < sim.Units.Subspecies.Count; i++)
                if (sim.Units.Subspecies.IsAlive(i)) { root = i; break; }
            if (root < 0)
                root = sim.Units.Subspecies.CreateRoot(scenario.SpeciesIndex, -1, -1, sim.Clock.Tick, 0, false, ref rng);
            while (ScenarioMetric.Read("subspecies", sim, sim.ScenarioStartYear) < target)
                sim.Units.Subspecies.Branch(root, -1, -1, sim.Clock.Tick, 0, false, ref rng);
        }
    }
}
