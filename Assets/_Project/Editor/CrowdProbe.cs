using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using PG.Content;
using PG.Core;
using PG.Sim;
using PG.World;
using PG.WorldGen;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using EntityId = PG.Core.EntityId;

namespace PG.EditorTools
{
    // Headless crowding measurement: how many units overlap visually, and who they are. Run: Tools/run_unity.ps1 -Mode CrowdProbe
    public static class CrowdProbe
    {
        const float StackDistance = 0.4f; // closer than this the sprites sit on top of each other

        [MenuItem("PixelGenesis/Crowd Probe")]
        public static void Run()
        {
            int code = 0;
            try
            {
                DataSync.Sync();
                var db = ContentDB.LoadAll(Path.Combine(Application.streamingAssetsPath, "Data"), null);
                var settings = new WorldGenSettings { Seed = 2024, Size = MapSizePreset.Small };
                var map = WorldGenerator.Generate(settings, db);
                using (var sim = new SimWorld(db, map, new SimRandomProvider(settings.Seed)))
                {
                    if (Environment.GetEnvironmentVariable("PG_CROWD") == "battle") Battle(sim, db);
                    else Wild(sim, db);
                }
            }
            catch (Exception e) { Debug.LogException(e); code = 1; }
            Debug.Log("[Crowd] DONE");
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        // PG_CROWD=city: one big human settlement, no animals; default: wild world with a small village.
        static void Wild(SimWorld sim, ContentDB db)
        {
            {
                {
                    bool city = Environment.GetEnvironmentVariable("PG_CROWD") == "city";
                    if (!city) sim.SpawnInitialAnimals();
                    CivSoak.SpawnSettlers(sim, db.Species.IdOf("sp.human"), city ? 40 : 10, 9);
                    if (!city) CivSoak.SpawnSettlers(sim, db.Species.IdOf("sp.orc"), 10, 11, new int2(0, 0), 0f, out _);
                    var checkpoints = city ? new HashSet<int> { 0, 5, 10, 20 } : new HashSet<int> { 0, 2, 8, 20, 40 };
                    for (int year = 0; year <= (city ? 20 : 40); year++)
                    {
                        if (checkpoints.Contains(year)) Report(sim, year);
                        for (int t = 0; t < SimConst.TicksPerYear; t++) sim.Tick();
                    }
                }
            }
        }

        // A god-power style drop of 40 humans against 40 orcs, watched tick by tick.
        static void Battle(SimWorld sim, ContentDB db)
        {
            var map = sim.World;
            var rng = SimRandom.Derive(2024, 99, 0);
            CivSoak.SpawnSettlers(sim, db.Species.IdOf("sp.human"), 1, 9, default, 0f, out int2 site);
            for (int side = 0; side < 2; side++)
            {
                int species = db.Species.IdOf(side == 0 ? "sp.human" : "sp.orc");
                float life = db.Species[species].BaseStats[(int)StatId.Lifespan];
                SpawnSpread.Begin(); // the same placement the spawn powers use
                for (int n = 0; n < 40; n++)
                    sim.Units.Store.Spawn(new UnitSpawnRequest { Species = species, AgeYears = life * .3f, Subspecies = -1, Sex = n % 2,
                        Mother = EntityId.None, Father = EntityId.None,
                        Pos = SpawnSpread.Next(sim.Units, Mobility.Land, new float2(site.x + 8 * side + .5f, site.y + .5f), 40, 3f, ref rng) }, sim.Clock.Tick, ref rng);
                }
            foreach (int ticks in new[] { 0, 40, 100, 200, 400 })
            {
                while (sim.Clock.Tick < ticks) sim.Tick();
                Report(sim, ticks);
            }
        }

        static void Report(SimWorld sim, int year)
        {
            var u = sim.Units.Store;
            var db = sim.Content;
            int n = u.Alive.Length;
            var stacked = new bool[n];
            var neighbours = new int[n];
            for (int a = 0; a < n; a++)
                for (int b = a + 1; b < n; b++)
                {
                    int i = u.Alive[a], j = u.Alive[b];
                    if (math.distancesq(u.Pos[i], u.Pos[j]) >= StackDistance * StackDistance) continue;
                    stacked[a] = stacked[b] = true; neighbours[a]++; neighbours[b]++;
                }
            int stackedCount = 0, worst = 0;
            var perSpecies = new Dictionary<string, int[]>(); // [total, stacked]
            var perTask = new Dictionary<string, int>();
            for (int a = 0; a < n; a++)
            {
                int i = u.Alive[a];
                string sp = db.Species[u.Species[i]].Id;
                if (!perSpecies.TryGetValue(sp, out var c)) perSpecies[sp] = c = new int[2];
                c[0]++;
                if (!stacked[a]) continue;
                stackedCount++; c[1]++; worst = Math.Max(worst, neighbours[a]);
                string task = ((UnitTask)u.Task[i]).ToString();
                perTask.TryGetValue(task, out int v); perTask[task] = v + 1;
            }
            // Worst 1x1 tiles.
            var tiles = new Dictionary<int, List<int>>();
            for (int a = 0; a < n; a++)
            {
                int i = u.Alive[a];
                int2 t = u.Tile(i);
                int key = t.y * sim.World.Width + t.x;
                if (!tiles.TryGetValue(key, out var list)) tiles[key] = list = new List<int>();
                list.Add(i);
            }
            var top = new List<KeyValuePair<int, List<int>>>(tiles);
            top.Sort((x, y) => y.Value.Count.CompareTo(x.Value.Count));
            var sb = new StringBuilder();
            sb.AppendLine($"[Crowd] year {year}: {n} units, {stackedCount} ({(n == 0 ? 0 : 100f * stackedCount / n):0}%) overlap another within {StackDistance} tile, worst has {worst} neighbours");
            foreach (var kv in perSpecies) if (kv.Value[1] > 0 || kv.Value[0] > 30) sb.AppendLine($"[Crowd]   {kv.Key}: {kv.Value[0]} total, {kv.Value[1]} stacked");
            sb.Append("[Crowd]   stacked by task:"); foreach (var kv in perTask) sb.Append($" {kv.Key}={kv.Value}"); sb.AppendLine();
            for (int k = 0; k < Math.Min(5, top.Count); k++)
            {
                var list = top[k].Value;
                if (list.Count < 3) break;
                var mix = new Dictionary<string, int>();
                foreach (int i in list) { string key = db.Species[u.Species[i]].Id + "/" + (UnitTask)u.Task[i] + (u.City[i] >= 0 ? "/city" : ""); mix.TryGetValue(key, out int v); mix[key] = v + 1; }
                var text = new StringBuilder();
                foreach (var kv in mix) text.Append(kv.Key).Append('x').Append(kv.Value).Append(' ');
                sb.AppendLine($"[Crowd]   tile ({top[k].Key % sim.World.Width},{top[k].Key / sim.World.Width}): {list.Count} units: {text}");
            }
            Debug.Log(sb.ToString());
        }
    }
}
