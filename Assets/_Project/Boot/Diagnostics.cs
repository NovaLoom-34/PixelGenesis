using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using PG.Content;
using PG.Core;
using PG.Powers;
using PG.Persistence;
using PG.Sim;
using PG.World;
using PG.WorldGen;
using Unity.Mathematics;
using Debug = UnityEngine.Debug;

namespace PG.Boot
{
    // Bölüm 1.15 determinism check and EkB 3.7.3 headless run. Both work without rendering.
    public static class Diagnostics
    {
        const int HashEvery = 100;
        const int EditEvery = 5;

        // Two runs with the same seed and the same scripted brush edits must produce identical world hashes.
        public static bool DeterminismCheck(ContentDB db, WorldGenSettings settings, int ticks, int simulationDetailDivisions = 1)
        {
            var a = Run(db, settings, ticks, simulationDetailDivisions);
            var b = Run(db, settings, ticks, simulationDetailDivisions);
            bool ok = a.Count == b.Count;
            for (int i = 0; ok && i < a.Count; i++)
            {
                if (a[i] == b[i]) continue;
                Debug.LogError($"[Determinism] Mismatch at tick {(i + 1) * HashEvery}: {a[i]:X16} != {b[i]:X16}");
                ok = false;
            }
            if (ok) Debug.Log($"[Determinism] OK: {ticks} ticks, {a.Count} hashes, last {(a.Count > 0 ? a[a.Count - 1] : 0):X16}");
            return ok;
        }

        static List<ulong> Run(ContentDB db, WorldGenSettings settings, int ticks, int simulationDetailDivisions)
        {
            var hashes = new List<ulong>();
            var map = WorldGenerator.Generate(settings.Clone(), db);
            using (var sim = new SimWorld(db, map, new SimRandomProvider(settings.Seed)))
            {
                int detailLaw = sim.Nature.LawSimulationDetail;
                if (detailLaw >= 0) sim.Nature.Laws.Set(detailLaw, true, UnitThinkSystem.Divisions(simulationDetailDivisions));
                sim.SpawnInitialAnimals();
                var queue = new PowerCommandQueue();
                PowerSystems.Install(sim, queue, false);
                var brushes = BrushPowers(db);
                var script = new SimRandom(settings.Seed, 0xD37E);

                for (int t = 1; t <= ticks; t++)
                {
                    if (brushes.Count > 0 && t % EditEvery == 0) EnqueueRandomEdit(queue, brushes, map, ref script, sim.Clock.Tick);
                    sim.Tick();
                if (t % HashEvery == 0)
                    hashes.Add(SaveSystem.ComputeStateHash(sim));
                }
            }
            return hashes;
        }

        // Positions, hp, species, city and job of every live unit (Bölüm 3.12 #9) + civ state (buildings, stores, items).
        public static ulong UnitHash(SimWorld sim)
        {
            var u = sim.Units.Store;
            ulong h = 1469598103934665603UL ^ (ulong)u.Count;
            for (int k = 0; k < u.Alive.Length; k++)
            {
                int i = u.Alive[k];
                h = (h ^ (ulong)i) * 1099511628211UL;
                h = (h ^ (ulong)math.asuint(u.Pos[i].x)) * 1099511628211UL;
                h = (h ^ (ulong)math.asuint(u.Pos[i].y)) * 1099511628211UL;
                h = (h ^ (ulong)math.asuint(u.Hp[i])) * 1099511628211UL;
                h = (h ^ u.Species[i]) * 1099511628211UL;
                h = (h ^ (ulong)(u.City[i] + 1) ^ ((ulong)u.Job[i] << 32)) * 1099511628211UL;
            }
            return h ^ CivHash(sim);
        }

        // Hashes the complete saveable meta section, including diplomacy, history sidecars,
        // biographies, world summaries, names and stores. Used only by diagnostics/tests.
        public static ulong MetaHash(SimWorld sim)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                sim.Meta.Write(writer);
                writer.Flush();
                var bytes = stream.GetBuffer();
                ulong hash = 14695981039346656037UL;
                for (int i = 0; i < (int)stream.Length; i++)
                    hash = (hash ^ bytes[i]) * 1099511628211UL;
                return hash;
            }
        }

        public static ulong CivHash(SimWorld sim)
        {
            ulong h = 1469598103934665603UL;
            var civ = sim.Civ;
            h = (h ^ (ulong)civ.CityCount) * 1099511628211UL;
            for (int b = 0; b < civ.Buildings.Length; b++)
            {
                var d = civ.Buildings[b];
                h = (h ^ d.Def ^ ((ulong)d.Flags << 16) ^ ((ulong)(uint)d.Origin.x << 24)) * 1099511628211UL;
                h = (h ^ (ulong)math.asuint(d.BuildProgress) ^ ((ulong)math.asuint(d.Hp) << 32)) * 1099511628211UL;
            }
            for (int k = 0; k < civ.Inventory.Length; k++) h = (h ^ (ulong)(uint)civ.Inventory[k]) * 1099511628211UL;
            h = (h ^ (ulong)civ.Items.Length) * 1099511628211UL;
            for (int c = 0; c < civ.CityCount; c++) h = (h ^ (ulong)(uint)civ.City(c).Kingdom ^ ((ulong)(uint)civ.City(c).Loyalty << 32)) * 1099511628211UL;
            var meta = sim.Meta;
            for (int k = 0; k < meta.Kingdoms.Count; k++)
            {
                var d = meta.Kingdoms.Items[k];
                h = (h ^ (ulong)(uint)d.King.Index ^ ((ulong)(uint)d.Capital << 32)) * 1099511628211UL;
                for (int o = 0; o < meta.Kingdoms.Count; o++) h = (h ^ (ulong)(ushort)meta.Opinion(k, o)) * 1099511628211UL;
            }
            h = (h ^ (ulong)meta.Wars.Count ^ ((ulong)meta.Armies.Length << 20) ^ ((ulong)meta.Names.Count << 40)) * 1099511628211UL;
            var pw = sim.Powers;
            for (int k = 0; k < pw.Drops.Length; k++) h = (h ^ (ulong)math.asuint(pw.Drops[k].Z) ^ ((ulong)math.asuint(pw.Drops[k].Pos.x) << 32)) * 1099511628211UL;
            for (int k = 0; k < pw.Objects.Length; k++) h = (h ^ (ulong)math.asuint(pw.Objects[k].Pos.x) ^ ((ulong)(uint)pw.Objects[k].Age << 32)) * 1099511628211UL;
            h = (h ^ (ulong)pw.TempTiles.Length ^ ((ulong)(uint)pw.LifeCells << 24)) * 1099511628211UL;
            h = (h ^ (ulong)meta.History.Length ^ ((ulong)meta.Cultures.Count << 20) ^ ((ulong)meta.Religions.Count << 30) ^ ((ulong)meta.Plots.Count << 40) ^ ((ulong)meta.Books.Length << 50)) * 1099511628211UL;
            var us = sim.Units.Store;
            for (int k = 0; k < us.Alive.Length; k++) { int i = us.Alive[k]; h = (h ^ (ulong)(uint)(us.Culture[i] * 31 + us.Religion[i] * 7 + us.Clan[i] * 3 + us.Language[i])) * 1099511628211UL; }
            for (int k = 0; k < us.Alive.Length; k++) { int i = us.Alive[k]; h = (h ^ (ulong)(uint)(us.Subspecies[i] * 131 + us.FormerSpecies[i] * 17) ^ (ulong)us.TransformTick[i]) * 1099511628211UL; }
            var subs = sim.Units.Subspecies;
            for (int s = 0; s < subs.Count; s++)
            {
                var d = subs.Meta.Items[s];
                h = (h ^ (ulong)(uint)(d.Phenotype * 7 + d.MonolithStage * 3 + d.Mutations + d.Isolation.Length * 11)) * 1099511628211UL;
                for (int g = 0; g < d.Genome.Slots.Length; g++) h = (h ^ d.Genome.Slots[g]) * 1099511628211UL;
            }
            return h;
        }

        static List<int> BrushPowers(ContentDB db)
        {
            var list = new List<int>();
            for (int i = 0; i < db.Powers.Count; i++)
                if (PowerOps.IsSupported(db.Powers[i], db)) list.Add(i);
            return list;
        }

        static void EnqueueRandomEdit(PowerCommandQueue queue, List<int> brushes, WorldMap map, ref SimRandom rng, long tick)
        {
            var from = new int2(rng.Range(0, map.Width), rng.Range(0, map.Height));
            var to = math.clamp(from + new int2(rng.Range(-20, 21), rng.Range(-20, 21)), 0, new int2(map.Width - 1, map.Height - 1));
            queue.Enqueue(new PowerCommand
            {
                PowerId = (ushort)brushes[rng.Range(0, brushes.Count)],
                From = from,
                To = to,
                Brush = new BrushSpec(rng.Chance(0.5f) ? BrushShape.Circle : BrushShape.Square, Brushes.Radii[rng.Range(0, 6)]),
                Tick = tick,
            });
        }

        static int CountFeatures(WorldMap map)
        {
            int n = 0;
            for (int i = 0; i < map.TileCount; i++)
                if (map.Feature[i] != 0) n++;
            return n;
        }

        // Runs `years` of simulation without rendering and prints a short report.
        public static void Headless(ContentDB db, WorldGenSettings settings, int years)
        {
            var sw = Stopwatch.StartNew();
            var map = WorldGenerator.Generate(settings.Clone(), db);
            double genMs = sw.Elapsed.TotalMilliseconds;
            using (var sim = new SimWorld(db, map, new SimRandomProvider(settings.Seed)))
            {
                if (settings.SpawnAnimals) sim.SpawnInitialAnimals();
                sw.Restart();
                long ticks = (long)years * SimConst.TicksPerYear;
                for (long t = 0; t < ticks; t++) sim.Tick();
                double runMs = sw.Elapsed.TotalMilliseconds;
                Debug.Log($"[Headless] seed {settings.Seed} map {map.Width}x{map.Height} gen {genMs:0} ms | {years} years = {ticks} ticks in {runMs:0} ms " +
                          $"({ticks / System.Math.Max(0.001, runMs / 1000.0):0} ticks/s) | population {sim.Population} | regions {sim.Regions.RegionCount} " +
                          $"islands {sim.Regions.IslandCount} | managed memory {System.GC.GetTotalMemory(false) / (1024 * 1024)} MB");
                var n = sim.Nature;
                Debug.Log($"[Headless] nature: era {n.CurrentEra.Id} (since year {n.Era.StartedYear}) | burning {n.Burning.Length} | lava {n.Lava.Length} " +
                          $"| clouds {n.Clouds.Length} | seeds {n.Seeds.Length} | features {CountFeatures(map)}");
            }
        }
    }
}
