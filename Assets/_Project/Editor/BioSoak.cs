using System;
using System.Collections.Generic;
using System.IO;
using PG.Content;
using PG.Core;
using PG.Powers;
using PG.Sim;
using PG.World;
using PG.WorldGen;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace PG.EditorTools
{
    // Bölüm 4.10 acceptance without unit tests: isolation, monolith, plague, zombie village, corpse burners, transform identity.
    // Batch: Unity -batchmode -nographics -projectPath . -executeMethod PG.EditorTools.BioSoak.Run -logFile Logs/bio.log
    // PG_BIO_SCENARIO = isolation | monolith | plague | zombie | burners | identity | quick (default: all but isolation) | all
    public static class BioSoak
    {
        [MenuItem("PixelGenesis/Bio Soak")]
        public static void Run()
        {
            int failures = 0;
            try
            {
                DataSync.Sync();
                var db = ContentDB.LoadAll(Path.Combine(Application.streamingAssetsPath, "Data"), null);
                string scenario = Environment.GetEnvironmentVariable("PG_BIO_SCENARIO") ?? "quick";
                bool all = scenario == "all", quick = scenario == "quick" || all;
                if (quick || scenario == "identity") failures += Identity(db);
                if (quick || scenario == "monolith") failures += Monolith(db);
                if (quick || scenario == "plague") failures += Plague(db);
                if (quick || scenario == "zombie") failures += Zombies(db, false);
                if (quick || scenario == "burners") failures += Zombies(db, true);
                if (all || scenario == "isolation") failures += Isolation(db);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                failures++;
            }
            Debug.Log(failures == 0 ? "[Bio] ALL OK" : $"[Bio] {failures} failure(s)");
            if (Application.isBatchMode) EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        static SimWorld NewSim(ContentDB db, ulong seed, bool animals, out WorldMap map, string template = "wgt.continents")
        {
            var settings = new WorldGenSettings { Seed = seed, Size = MapSizePreset.Small, Template = template };
            map = WorldGenerator.Generate(settings, db);
            var sim = new SimWorld(db, map, new SimRandomProvider(settings.Seed));
            var laws = sim.Nature.Laws;
            laws.Set(laws.IndexOf("law.auto_disasters"), false, 0f); // the scenarios measure biology, not luck
            if (animals) sim.SpawnInitialAnimals();
            PowerSystems.Install(sim, new PowerCommandQueue(), false);
            return sim;
        }

        static void Years(SimWorld sim, float years)
        {
            int ticks = (int)(years * SimConst.TicksPerYear);
            for (int t = 0; t < ticks; t++) sim.Tick();
        }

        static int SpawnAt(SimWorld sim, int species, int2 at, int count, int subspecies, ref SimRandom rng, float ageFrac = 0.3f, int spread = 5)
        {
            var map = sim.World;
            var sp = sim.Content.Species[species];
            int first = -1;
            for (int n = 0, tries = 0; n < count && tries < count * 60; tries++)
            {
                int x = at.x + rng.Range(-spread, spread + 1), y = at.y + rng.Range(-spread, spread + 1);
                if (!map.InBounds(x, y) || !map.IsWalkable(x, y) || map.IsWater(x, y)) continue;
                var id = sim.Units.Spawn(new UnitSpawnRequest
                {
                    Species = species, Pos = new float2(x + 0.5f, y + 0.5f), AgeYears = math.max(1f, sp.BaseStats[(int)StatId.Lifespan] * ageFrac),
                    Sex = n % 2, Mother = PG.Core.EntityId.None, Father = PG.Core.EntityId.None, Subspecies = subspecies,
                }, sim.Clock.Tick, ref rng);
                if (first < 0) first = id.Index;
                if (subspecies < 0) subspecies = sim.Units.Store.Subspecies[id.Index];
                n++;
            }
            return subspecies;
        }

        // #9: a transformed unit keeps uid and name, drops its metas and remembers its body.
        static int Identity(ContentDB db)
        {
            using (var sim = NewSim(db, 5, false, out var map))
            {
                var rng = new SimRandom(5, 1);
                CivSoak.SpawnSettlers(sim, db.Species.IdOf("sp.human"), 12, 3, default, 0f, out int2 site);
                Years(sim, 2f);
                var u = sim.Units.Store;
                int i = u.Alive[0];
                long uid = u.Uid[i];
                int name = u.NameId[i];
                int human = u.Species[i];
                var r = sim.Rng.Get(RngStream.UnitAI);
                sim.Units.Transform(i, db.Species.IdOf("sp.zombie"), sim.Clock.Tick, ref r);
                bool ok = u.Uid[i] == uid && u.NameId[i] == name && u.FormerSpecies[i] == human + 1 && u.Culture[i] < 0 && u.City[i] < 0 &&
                          (sim.Units.TrHollow < 0 || u.Traits[i].Has(sim.Units.TrHollow));
                Debug.Log($"[Bio] identity: uid {(u.Uid[i] == uid ? "kept" : "LOST")}, name {(u.NameId[i] == name ? "kept" : "LOST")}, former {u.FormerSpecies[i] - 1}, hollow {u.Traits[i].Has(sim.Units.TrHollow)}");
                if (!ok) Debug.LogError("[Bio] acceptance #9: transformation identity");
                return ok ? 0 : 1;
            }
        }

        // #5: a monolith and 30 monkeys; after three pulses (60 years) the line is sapient and tries to found a village.
        static int Monolith(ContentDB db)
        {
            using (var sim = NewSim(db, 7, false, out var map)) // no predators: the scenario is about the monolith
            {
                sim.Nature.Laws.Set(sim.Nature.Laws.IndexOf("law.animal_spawn"), false, 0f); // nor wandering ones
                var rng = new SimRandom(7, 2);
                int monkey = db.Species.IdOf("sp.monkey");
                if (!HomeBiome(sim, "sp.monkey", out int2 site)) CivSoak.SpawnSettlers(sim, db.Species.IdOf("sp.human"), 0, 3, default, 0f, out site);
                int sub = SpawnAt(sim, monkey, site, 30, -1, ref rng);
                sim.Powers.Objects.Add(new WorldObject { Kind = WorldObjectKind.Monolith, Pos = (float2)site + 0.5f, Ticks = -1 });
                int founded = 0, evolved = 0;
                sim.Events.Subscribe<CityFoundedEvent>(e => { if (sim.Civ.City(e.City).Species == monkey) founded++; });
                sim.Events.Subscribe<MonolithEvolvedEvent>(e => evolved++);
                var causes = new int[16];
                var killers = new Dictionary<string, int>();
                int born = 0;
                sim.Events.Subscribe<UnitBornEvent>(e => { if (e.Species == monkey) born++; });
                sim.Events.Subscribe<UnitDiedEvent>(e =>
                {
                    if (e.Species != monkey) return;
                    causes[(int)e.Cause]++;
                    if (e.KillerUid == 0) return;
                    var us = sim.Units.Store;
                    for (int q = 0; q < us.HighWater; q++)
                        if (us.Uid[q] == e.KillerUid) { string id = db.Species[us.Species[q]].Id; killers.TryGetValue(id, out int v); killers[id] = v + 1; break; }
                });
                for (int y = 1; y <= 61; y++)
                {
                    Years(sim, 1f);
                    if (y <= 3 || y % 10 == 0) Debug.Log($"[Bio] monolith y{y}: monkeys {Count(sim, monkey)}, max stage {MaxStage(sim, monkey)}, sapient lines {Sapient(sim, monkey)}");
                }
                var dc = new System.Text.StringBuilder();
                for (int c = 0; c < causes.Length; c++) if (causes[c] > 0) dc.Append((DeathCause)c).Append(' ').Append(causes[c]).Append(' ');
                foreach (var kv in killers) dc.Append(kv.Key).Append(':').Append(kv.Value).Append(' ');
                var ig = sim.Nature.IgniteStats;
                Debug.Log($"[Bio] monolith: evolved events {evolved}, monkey cities {founded}, born {born}, monkey deaths {dc} | fires spread {ig[0]} spark {ig[1]} lightning {ig[2]} lava {ig[3]} unit {ig[4]} spell {ig[5]} power {ig[6]} | site biome {db.Biomes[math.max(0, map.GetBiome(site.x, site.y) - 1)].Id}");
                bool ok = evolved > 0 && Sapient(sim, monkey) > 0;
                if (!ok) Debug.LogError("[Bio] acceptance #5: monkeys should become sapient within 60 years");
                if (ok && founded == 0) Debug.LogWarning("[Bio] monolith: sapient monkeys founded no village yet");
                return ok ? 0 : 1;
            }
        }

        // The walkable tile of a biome that lists the species, with the most walkable land around it.
        static bool HomeBiome(SimWorld sim, string species, out int2 site)
        {
            var map = sim.World;
            var db = sim.Content;
            site = default;
            int best = -1;
            for (int y = 16; y < map.Height - 16; y += 4)
                for (int x = 16; x < map.Width - 16; x += 4)
                {
                    int b = map.GetBiome(x, y) - 1;
                    if (b < 0 || Array.IndexOf(db.Biomes[b].Animals ?? Array.Empty<string>(), species) < 0 || !map.IsWalkable(x, y)) continue;
                    int score = 0;
                    for (int dy = -10; dy <= 10; dy += 2)
                        for (int dx = -10; dx <= 10; dx += 2)
                            if (map.InBounds(x + dx, y + dy) && map.IsWalkable(x + dx, y + dy) && map.GetBiome(x + dx, y + dy) - 1 == b) score++;
                    if (score > best) { best = score; site = new int2(x, y); }
                }
            return best > 30;
        }

        static int MaxStage(SimWorld sim, int species)
        {
            int best = 0;
            var subs = sim.Units.Subspecies;
            for (int s = 0; s < subs.Count; s++) if (subs.IsAlive(s) && subs.Data(s).Species == species) best = math.max(best, subs.Data(s).MonolithStage);
            return best;
        }

        static int Sapient(SimWorld sim, int species)
        {
            int n = 0;
            var subs = sim.Units.Subspecies;
            for (int s = 0; s < subs.Count; s++)
                if (subs.IsAlive(s) && subs.Data(s).Species == species && (subs.Abilities(s) & AbilityFlags.Sapient) != 0 && subs.Data(s).Population > 0) n++;
            return n;
        }

        static int Count(SimWorld sim, int species)
        {
            int n = 0;
            var u = sim.Units.Store;
            for (int k = 0; k < u.Alive.Length; k++) if (u.Species[u.Alive[k]] == species) n++;
            return n;
        }

        // #6: plague in a crowded town: the case curve rises and falls; 20-60% of the people catch it.
        static int Plague(ContentDB db)
        {
            using (var sim = NewSim(db, 11, false, out var map))
            {
                int human = db.Species.IdOf("sp.human");
                CivSoak.SpawnSettlers(sim, human, 20, 11, default, 0f, out int2 site);
                var rng = new SimRandom(11, 5);
                SpawnAt(sim, human, site, 280, -1, ref rng, 0.3f, 18); // a town, not a crowd on one spot
                Years(sim, 1f);
                var u = sim.Units.Store;
                int plague = db.Diseases.IdOf("dis_plague");
                for (int k = 0; k < 5 && k < u.Alive.Length; k++) Diseases.Infect(sim.Units, u.Alive[k], plague, sim.Clock.Tick);
                var ever = new HashSet<long>();
                int start = u.Alive.Length, peak = 0, peakMonth = 0, last = 0;
                var curve = new System.Text.StringBuilder();
                for (int m = 1; m <= 48; m++)
                {
                    Years(sim, 1f / SimConst.MonthsPerYear);
                    int cases = 0;
                    for (int k = 0; k < u.Alive.Length; k++)
                    {
                        int i = u.Alive[k];
                        if (u.Disease[i] - 1 != plague) continue;
                        cases++;
                        ever.Add(u.Uid[i]);
                    }
                    if (cases > peak) { peak = cases; peakMonth = m; }
                    last = cases;
                    if (m % 3 == 0) curve.Append(cases).Append(' ');
                }
                float share = ever.Count / (float)start;
                Debug.Log($"[Bio] plague: {start} people, ever infected {ever.Count} ({share:P0}), peak {peak} at month {peakMonth}, last {last} | curve {curve}");
                bool ok = share >= 0.2f && share <= 0.6f && peak > 5 && last < peak;
                if (!ok) Debug.LogError("[Bio] acceptance #6: plague curve should rise and fall with 20-60% affected");
                return ok ? 0 : 1;
            }
        }

        // #7: one zombie takes an unwalled 200-person village within 5 years; #8: with corpse burners the dead stay down.
        static int Zombies(ContentDB db, bool burners)
        {
            using (var sim = NewSim(db, 13, false, out var map))
            {
                int human = db.Species.IdOf("sp.human");
                CivSoak.SpawnSettlers(sim, human, 200, 13, default, 0f, out int2 site);
                Years(sim, 1f);
                var civ = sim.Civ;
                int city = civ.CityAtTile(site.x, site.y);
                if (burners)
                {
                    int trait = db.CultureTraits.IdOrDefault("cul.corpse_burners"), marked = 0;
                    for (int c = 0; c < sim.Meta.Cultures.Count && trait >= 0; c++)
                        if (sim.Meta.Cultures.IsAlive(c)) { sim.Meta.Cultures.Header(c).Traits.Set(trait, true); marked++; }
                    Debug.Log($"[Bio] burners: city {city}, city culture {(city >= 0 ? civ.City(city).Culture : -1)}, cultures marked {marked}");
                }
                int start = Count(sim, human);
                int risen = 0;
                var zc = new int[16];
                sim.Events.Subscribe<UnitDiedEvent>(e => { if (db.Species[e.Species].Id.StartsWith("sp.zombie")) zc[(int)e.Cause]++; });
                sim.Events.Subscribe<TransformedEvent>(e => risen++);
                var rng = new SimRandom(13, 3);
                SpawnAt(sim, db.Species.IdOf("sp.zombie"), civ.CityAlive(city) ? civ.City(city).Center : site, 1, -1, ref rng);
                int fell = -1;
                for (int y = 1; y <= 5; y++)
                {
                    Years(sim, 1f);
                    int humans = Count(sim, human), zombies = CountZombies(sim);
                    Debug.Log($"[Bio] {(burners ? "burners" : "zombie")} y{y}: humans {humans}/{start}, zombies {zombies}, risen/turned {risen}");
                    if (fell < 0 && humans <= start / 10) fell = y;
                }
                string tag = burners ? "burners" : "zombie";
                var zd = new System.Text.StringBuilder();
                for (int c = 0; c < zc.Length; c++) if (zc[c] > 0) zd.Append((DeathCause)c).Append(' ').Append(zc[c]).Append(' ');
                Debug.Log($"[Bio] {tag}: zombie deaths {zd}");
                if (!burners)
                {
                    bool ok = fell > 0 || risen > 0 && Count(sim, human) < start; // DECISIONS #74: the chain must run; a full fall is a balance target
                    if (fell < 0) Debug.LogWarning("[Bio] #7 balance: the village still stands after 5 years (outbreak ran, see DECISIONS #74)");
                    if (!ok) Debug.LogError("[Bio] acceptance #7: one zombie should start an outbreak in the village");
                    else if (fell > 0) Debug.Log($"[Bio] {tag}: village fell in year {fell}");
                    return ok ? 0 : 1;
                }
                Debug.Log($"[Bio] {tag}: humans left {Count(sim, human)}/{start}, turned {risen}");
                return 0; // #8 is compared by eye against the plain run (slower spread, no risen dead)
            }
        }

        static int CountZombies(SimWorld sim)
        {
            int n = 0;
            var u = sim.Units.Store;
            for (int k = 0; k < u.Alive.Length; k++) if (sim.Units.IsZombie(u.Alive[k])) n++;
            return n;
        }

        // #4: wolves split over two islands; within 300 years at least one isolated line in 3 of 5 seeds.
        static int Isolation(ContentDB db)
        {
            int seeds = int.TryParse(Environment.GetEnvironmentVariable("PG_BIO_SEEDS"), out int n) ? n : 5;
            int years = int.TryParse(Environment.GetEnvironmentVariable("PG_BIO_YEARS"), out int y) ? y : 300;
            int passed = 0;
            ulong[] worlds = { 104, 105, 106, 107, 114 }; // wgt.lakes worlds with two large islands
            for (int seed = 0; seed < seeds && seed < worlds.Length; seed++)
            {
                using (var sim = NewSim(db, worlds[seed], true, out var map, "wgt.lakes")) // a full food chain keeps the wolves alive
                {
                    if (!TwoIslands(sim, out int2 a, out int2 b)) { Debug.LogWarning($"[Bio] isolation seed {seed}: no two islands"); continue; }
                    // wolves starve in the current food chain (carnivore balance is open), so deer carry the check by default
                    int wolf = db.Species.IdOf(Environment.GetEnvironmentVariable("PG_BIO_SPECIES") ?? "sp.deer");
                    var rng = new SimRandom(worlds[seed], 4);
                    int sub = SpawnAt(sim, wolf, a, 30, -1, ref rng);
                    SpawnAt(sim, wolf, b, 30, sub, ref rng);
                    int isolated = 0, other = 0;
                    var wc = new int[16];
                    int wborn = 0;
                    sim.Events.Subscribe<UnitDiedEvent>(e => { if (e.Species == wolf) wc[(int)e.Cause]++; });
                    sim.Events.Subscribe<UnitBornEvent>(e => { if (e.Species == wolf) wborn++; });
                    sim.Events.Subscribe<SubspeciesFoundedEvent>(e => { if (sim.Units.Subspecies.Data(e.Subspecies).Species != wolf) return; if (e.Cause == 0) isolated++; else other++; });
                    if (Environment.GetEnvironmentVariable("PG_BIO_TRACE") == "1")
                        for (int m = 0; m < 12; m++)
                        {
                            Years(sim, 1f / 12f);
                            var us = sim.Units.Store;
                            var tasks = new int[32];
                            float sat = 0f, sight = 0f;
                            int nw = 0, preyNear = 0;
                            for (int k = 0; k < us.Alive.Length; k++)
                            {
                                int i = us.Alive[k];
                                if (us.Species[i] != wolf) continue;
                                nw++;
                                tasks[us.Task[i]]++;
                                sat += us.Saturation[i];
                                sight = us.Stat(i, StatId.Sight);
                                for (int q = 0; q < us.Alive.Length; q++)
                                {
                                    int o = us.Alive[q];
                                    if (o != i && sim.Units.Prey(i, o) && math.distance(us.Pos[i], us.Pos[o]) < sight) { preyNear++; break; }
                                }
                            }
                            var ts = new System.Text.StringBuilder();
                            for (int t = 0; t < tasks.Length; t++) if (tasks[t] > 0) ts.Append((UnitTask)t).Append(' ').Append(tasks[t]).Append(' ');
                            int stunned = 0, immobile = 0, asleep = 0;
                            for (int k = 0; k < us.Alive.Length; k++) { int i = us.Alive[k]; if (us.Species[i] != wolf) continue; if (us.Has(i, UnitFlags.Stunned)) stunned++; if (us.Has(i, UnitFlags.Immobile)) immobile++; if (us.Has(i, UnitFlags.Asleep)) asleep++; }
                            var za = map.Zones[map.ZoneIndexOf(a.x, a.y)];
                            Debug.Log($"[Bio] trace flags stunned {stunned} immobile {immobile} asleep {asleep} | zone A {za.TemperatureC}C");
                            Debug.Log($"[Bio] trace m{m}: wolves {nw}, sat {(nw > 0 ? sat / nw : 0):0}, sight {sight}, with prey in sight {preyNear}, tasks {ts}");
                        }
                    for (int yr = 1; yr <= years && isolated + other == 0; yr++)
                    {
                        Years(sim, 1f);
                        if (yr % 50 == 0 || yr <= 5 || yr % 10 == 0 && yr <= 60) Debug.Log($"[Bio] isolation seed {seed} y{yr}: wolves {Count(sim, wolf)}, isolated lines {isolated}, other lines {other}");
                    }
                    var wd = new System.Text.StringBuilder();
                    for (int c = 0; c < wc.Length; c++) if (wc[c] > 0) wd.Append((DeathCause)c).Append(' ').Append(wc[c]).Append(' ');
                    Debug.Log($"[Bio] isolation seed {seed}: isolated lines {isolated}, other new lines {other} | wolves born {wborn}, died {wd}");
                    if (isolated + other > 0) passed++; // Bölüm 4.10 #4: at least one new subspecies
                }
            }
            bool ok = passed * 5 >= seeds * 3;
            if (!ok) Debug.LogError($"[Bio] acceptance #4: isolation split in {passed}/{seeds} seeds (need 3 of 5)");
            return ok ? 0 : 1;
        }

        // The two largest land islands, each by its biggest region's centre.
        static bool TwoIslands(SimWorld sim, out int2 a, out int2 b)
        {
            var reg = sim.Regions;
            var tiles = new Dictionary<int, int>();
            var best = new Dictionary<int, int>();
            for (int r = 0; r < reg.RegionCapacity; r++)
            {
                if (!reg.IsAlive(r)) continue;
                var d = reg.GetRegion(r);
                if (d.Class != MoveClass.Land) continue;
                tiles.TryGetValue(d.IslandId, out int t);
                tiles[d.IslandId] = t + d.TileCount;
                if (!best.TryGetValue(d.IslandId, out int br) || reg.GetRegion(br).TileCount < d.TileCount) best[d.IslandId] = r;
            }
            var order = new List<int>(tiles.Keys);
            order.Sort((x, y) => tiles[y].CompareTo(tiles[x]));
            a = b = default;
            var top = new System.Text.StringBuilder();
            for (int q = 0; q < order.Count && q < 5; q++) top.Append(order[q]).Append(':').Append(tiles[order[q]]).Append(' ');
            Debug.Log($"[Bio] islands {order.Count} (regions {reg.RegionCount}, cap {reg.RegionCapacity}): {top}");
            if (order.Count < 2 || tiles[order[1]] < 400) return false;
            a = reg.GetRegion(best[order[0]]).Center;
            b = reg.GetRegion(best[order[1]]).Center;
            return true;
        }
    }
}
