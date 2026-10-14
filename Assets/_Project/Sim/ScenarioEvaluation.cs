using System;
using PG.Content;
using PG.Core;

namespace PG.Sim
{
    public enum ScenarioStatus : byte { Sandbox, Active, Won, Lost }

    public readonly struct ScenarioConditionProgress
    {
        public readonly string TextKey;
        public readonly string Operator;
        public readonly int Current, Target;
        public readonly bool Complete;

        public ScenarioConditionProgress(string textKey, string op, int current, int target, bool complete)
        {
            TextKey = textKey;
            Operator = op;
            Current = current;
            Target = target;
            Complete = complete;
        }
    }

    public sealed class ScenarioEvaluation
    {
        public ScenarioStatus Status { get; internal set; }
        public ScenarioConditionProgress[] Goals { get; internal set; } = Array.Empty<ScenarioConditionProgress>();
        public ScenarioConditionProgress[] Failures { get; internal set; } = Array.Empty<ScenarioConditionProgress>();
    }

    // Read-only scenario measurement. It is called by UI at year boundaries and never mutates the simulation.
    public static class ScenarioMetric
    {
        static readonly string[] Supported = { "elapsedYears", "livingPopulation", "livingKingdoms", "livingCities", "alliances", "subspecies" };

        public static bool IsSupported(string metric) => Array.IndexOf(Supported, metric) >= 0;

        public static int Read(string metric, SimWorld sim, int startYear)
        {
            switch (metric)
            {
                case "elapsedYears": return Math.Max(0, sim.Clock.Year - startYear);
                case "livingPopulation": return LivingCivilizedPopulation(sim);
                case "livingKingdoms": return CountAlive(sim.Meta.Kingdoms);
                case "livingCities": return CountAlive(sim.Civ.Cities);
                case "alliances": return CountAlive(sim.Meta.Alliances);
                case "subspecies": return LivingSubspecies(sim);
                default: return 0;
            }
        }

        static int CountAlive<T>(MetaStore<T> store) where T : unmanaged
        {
            int count = 0;
            for (int i = 0; i < store.Count; i++) if (store.IsAlive(i)) count++;
            return count;
        }

        static int LivingCivilizedPopulation(SimWorld sim)
        {
            int count = 0;
            var units = sim.Units.Store;
            for (int i = 0; i < units.Alive.Length; i++)
            {
                int unit = units.Alive[i];
                if (sim.Content.Species[units.Species[unit]].IsCiv) count++;
            }
            return count;
        }

        static int LivingSubspecies(SimWorld sim)
        {
            int count = 0;
            var subspecies = sim.Units.Subspecies;
            for (int i = 0; i < subspecies.Count; i++) if (subspecies.IsAlive(i)) count++;
            return count;
        }
    }

    public static class ScenarioRules
    {
        public static bool CanUsePower(ScenarioDef scenario, string powerId)
        {
            if (scenario == null || !scenario.RestrictPowers) return true;
            var allowed = scenario.AllowedPowers;
            if (allowed == null) return false;
            for (int i = 0; i < allowed.Length; i++)
                if (string.Equals(allowed[i], powerId, StringComparison.Ordinal)) return true;
            return false;
        }

        public static ScenarioEvaluation Evaluate(SimWorld sim, ScenarioDef scenario)
        {
            if (sim == null || scenario == null || !string.Equals(sim.ScenarioId, scenario.Id, StringComparison.Ordinal))
                return new ScenarioEvaluation { Status = ScenarioStatus.Sandbox };

            var current = EvaluateCurrent(sim, scenario);
            if (sim.ScenarioOutcome == ScenarioStatus.Won || sim.ScenarioOutcome == ScenarioStatus.Lost)
                current.Status = sim.ScenarioOutcome;
            return current;
        }

        public static ScenarioEvaluation EvaluateCurrent(SimWorld sim, ScenarioDef scenario)
        {
            if (sim == null || scenario == null || !string.Equals(sim.ScenarioId, scenario.Id, StringComparison.Ordinal))
                return new ScenarioEvaluation { Status = ScenarioStatus.Sandbox };

            var failures = scenario.Failures ?? Array.Empty<ScenarioConditionDef>();
            var failureProgress = new ScenarioConditionProgress[failures.Length];
            bool lost = false;
            for (int i = 0; i < failures.Length; i++)
            {
                var condition = failures[i];
                int current = ScenarioMetric.Read(condition.Metric, sim, sim.ScenarioStartYear);
                bool complete = IsMet(condition, current);
                failureProgress[i] = new ScenarioConditionProgress(condition.Text, condition.Operator, current, condition.Target, complete);
                lost |= complete;
            }

            var goals = scenario.Goals ?? Array.Empty<ScenarioConditionDef>();
            var progress = new ScenarioConditionProgress[goals.Length];
            bool won = goals.Length > 0;
            for (int i = 0; i < goals.Length; i++)
            {
                var condition = goals[i];
                int current = ScenarioMetric.Read(condition.Metric, sim, sim.ScenarioStartYear);
                bool complete = IsMet(condition, current);
                progress[i] = new ScenarioConditionProgress(condition.Text, condition.Operator, current, condition.Target, complete);
                won &= complete;
            }
            return new ScenarioEvaluation
            {
                Status = lost ? ScenarioStatus.Lost : won ? ScenarioStatus.Won : ScenarioStatus.Active,
                Goals = progress,
                Failures = failureProgress,
            };
        }

        static bool IsMet(ScenarioConditionDef condition, int current) =>
            condition.Operator == "atMost" ? current <= condition.Target : current >= condition.Target;
    }

    public static class ScenarioSpawner
    {
        const ulong SpawnStream = 0x5343454E4152494FUL;

        public static int SpawnStartingPopulation(SimWorld sim, ScenarioDef scenario)
        {
            if (sim == null || scenario == null || scenario.StartingPopulation <= 0 || scenario.SpeciesIndex < 0) return 0;
            var rng = SimRandom.Derive(sim.Seed, unchecked((int)SpawnStream), scenario.Index);
            var species = sim.Content.Species[scenario.SpeciesIndex];
            int made = 0;
            int total = sim.World.TileCount;
            int start = rng.Range(0, total);
            for (int n = 0; n < scenario.StartingPopulation && sim.Units.Store.Count < UnitWorld.HardUnitCap; n++)
            {
                int tile = FindStandable(sim, species.Habitat, start, n);
                if (tile < 0) break;
                int x = tile % sim.World.Width, y = tile / sim.World.Width;
                sim.Units.Spawn(new UnitSpawnRequest
                {
                    Species = scenario.SpeciesIndex,
                    Pos = new Unity.Mathematics.float2(x + 0.5f, y + 0.5f),
                    AgeYears = 18f,
                    Sex = (byte)(n & 1),
                    Mother = EntityId.None,
                    Father = EntityId.None,
                    Subspecies = -1,
                }, sim.Clock.Tick, ref rng, true);
                made++;
            }
            return made;
        }

        static int FindStandable(SimWorld sim, Habitat habitat, int start, int attempt)
        {
            int total = sim.World.TileCount;
            int stride = total > 1 ? total - 1 : 1;
            for (int step = 0; step < total; step++)
            {
                int tile = (int)(((long)start + (long)(attempt * 97 + step) * stride / total) % total);
                int x = tile % sim.World.Width, y = tile / sim.World.Width;
                var mobility = habitat == Habitat.Air ? Mobility.Fly : habitat == Habitat.Water ? Mobility.Water : Mobility.Land;
                if (sim.Units.Paths.CanStand(x, y, mobility) && (mobility != Mobility.Fly || !sim.World.IsWater(x, y))) return tile;
            }
            return -1;
        }
    }

    public static class CivStartSpawner
    {
        public const int InitialPopulationPerSpecies = 8;
        const int ClusterRadius = 12;
        const int MaxCenterAttempts = 128;
        const int SpawnStream = 0x43495653;

        public static int SpawnStartingPopulation(SimWorld sim)
        {
            if (sim == null) return 0;
            int totalSpawned = 0;
            int[] cluster = new int[InitialPopulationPerSpecies];
            for (int species = 0; species < sim.Content.Species.Count; species++)
            {
                if (!sim.Content.Species[species].IsCiv || sim.Units.Store.Count + InitialPopulationPerSpecies > UnitWorld.HardUnitCap) continue;
                var definition = sim.Content.Species[species];
                var rng = SimRandom.Derive(sim.Seed, SpawnStream, species);
                if (!TryFindCluster(sim, definition.Habitat, ref rng, cluster)) continue;

                int made = 0;
                for (int i = 0; i < cluster.Length; i++)
                {
                    int tile = cluster[i];
                    int x = tile % sim.World.Width, y = tile / sim.World.Width;
                    sim.Units.Spawn(new UnitSpawnRequest
                    {
                        Species = species,
                        Pos = new Unity.Mathematics.float2(x + 0.5f, y + 0.5f),
                        AgeYears = definition.BaseStats[(int)StatId.Lifespan] * 0.5f, // mid-life: adult for every civ species
                        Sex = i & 1,
                        Mother = EntityId.None,
                        Father = EntityId.None,
                        Subspecies = -1,
                    }, sim.Clock.Tick, ref rng, true);
                    made++;
                }
                if (made == cluster.Length) totalSpawned += made;
            }
            return totalSpawned;
        }

        static bool TryFindCluster(SimWorld sim, Habitat habitat, ref SimRandom rng, int[] tiles)
        {
            int total = sim.World.TileCount;
            if (total == 0) return false;
            var mobility = habitat == Habitat.Air ? Mobility.Fly : habitat == Habitat.Water ? Mobility.Water : Mobility.Land;
            int start = rng.Range(0, total);
            int attempts = Math.Min(MaxCenterAttempts, total);
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                int center = (int)(((long)start + (long)attempt * 97) % total);
                int cx = center % sim.World.Width, cy = center / sim.World.Width;
                if (!CanStand(sim, mobility, habitat, cx, cy)) continue;

                int found = 0;
                for (int dy = -ClusterRadius; dy <= ClusterRadius && found < tiles.Length; dy++)
                    for (int dx = -ClusterRadius; dx <= ClusterRadius && found < tiles.Length; dx++)
                    {
                        if (dx * dx + dy * dy > ClusterRadius * ClusterRadius) continue;
                        int x = cx + dx, y = cy + dy;
                        if (!sim.World.InBounds(x, y) || !CanStand(sim, mobility, habitat, x, y)) continue;
                        tiles[found++] = sim.World.Index(x, y);
                    }
                if (found == tiles.Length) return true;
            }
            return false;
        }

        static bool CanStand(SimWorld sim, Mobility mobility, Habitat habitat, int x, int y) =>
            sim.Units.Paths.CanStand(x, y, mobility) && (habitat != Habitat.Air || !sim.World.IsWater(x, y));
    }
}
