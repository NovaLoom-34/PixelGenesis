using System;
using System.Diagnostics;
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
using Debug = UnityEngine.Debug;

namespace PG.EditorTools
{
    // Editor-only paired climate control: keep each zone's generated climate fixed before Units/Civ run.
    public sealed class FixedClimateControlSystem : ISimSystem
    {
        readonly short[] _temperature;
        readonly byte[] _moisture, _dryMonths, _drought;

        public FixedClimateControlSystem(WorldMap world)
        {
            int count = world.Zones.Length;
            _temperature = new short[count];
            _moisture = new byte[count];
            _dryMonths = new byte[count];
            _drought = new byte[count];
            for (int i = 0; i < count; i++)
            {
                var zone = world.Zones[i];
                _temperature[i] = zone.TemperatureC;
                _moisture[i] = zone.Moisture;
                _dryMonths[i] = zone.DryMonths;
                _drought[i] = zone.Drought;
            }
        }

        public SimPhase Phase => SimPhase.Climate;
        public int Order => 1000;

        public void Tick(in SimContext ctx)
        {
            var modifiers = ctx.Nature.Mods;
            if (modifiers.GlobalRain)
            {
                modifiers.GlobalRain = false;
                ctx.Nature.Mods = modifiers;
            }
            if (!ctx.Clock.IsMonthStart) return;
            for (int i = 0; i < _temperature.Length; i++)
            {
                var zone = ctx.World.Zones[i];
                zone.TemperatureC = _temperature[i];
                zone.Moisture = _moisture[i];
                zone.DryMonths = _dryMonths[i];
                zone.Drought = _drought[i];
                ctx.World.Zones[i] = zone;
            }
        }
    }

    // Bölüm 5.12 #1/#7 numbers without tests: settlers on open land, decade report of cities, halls, population and cost.
    // Batch: Unity -batchmode -nographics -projectPath . -executeMethod PG.EditorTools.CivSoak.Run -logFile Logs/civ.log
    public static class CivSoak
    {
        static readonly bool TraceOn = Environment.GetEnvironmentVariable("PG_CIV_TRACE") == "1";
        static readonly bool PlannerEvery = Environment.GetEnvironmentVariable("PG_CIV_PLANNER_EVERY") == "1"; // planner dump every year

        [MenuItem("PixelGenesis/Civ Soak")]
        public static void Run()
        {
            int failures = 0;
            try
            {
                DataSync.Sync();
                var db = ContentDB.LoadAll(Path.Combine(Application.streamingAssetsPath, "Data"), null);
                int years = int.TryParse(Environment.GetEnvironmentVariable("PG_CIV_YEARS"), out int y) ? y : 100;
                string scenario = Environment.GetEnvironmentVariable("PG_CIV_SCENARIO") ?? "single";
                bool fixedClimate = Environment.GetEnvironmentVariable("PG_CIV_CLIMATE_CONTROL") == "1";
                bool tradeEnabled = Environment.GetEnvironmentVariable("PG_CIV_TRADE_ENABLED") != "0";
                string reportScenario = (fixedClimate ? scenario + "-fixed-climate" : scenario + "-variable-climate")
                    + (tradeEnabled ? "-trade-on" : "-trade-off");
                failures += Settle(db, years, scenario == "war" || scenario == "multi", scenario == "multi", reportScenario, fixedClimate, tradeEnabled);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                failures++;
            }
            Debug.Log(failures == 0 ? "[Civ] ALL OK" : $"[Civ] {failures} failure(s)");
            if (Application.isBatchMode) EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        static int Settle(ContentDB db, int years, bool war, bool multi = false, string scenario = null, bool fixedClimate = false, bool tradeEnabled = true)
        {
            var settings = new WorldGenSettings { Seed = ulong.TryParse(Environment.GetEnvironmentVariable("PG_CIV_SEED"), out ulong seed) ? seed : 2024, Size = multi ? MapSizePreset.Medium : MapSizePreset.Small };
            var map = WorldGenerator.Generate(settings, db);
            using (var sim = new SimWorld(db, map, new SimRandomProvider(settings.Seed)))
            {
                sim.Civ.TradeEnabled = tradeEnabled;
                if (fixedClimate)
                {
                    sim.Nature.Laws.Set(sim.Nature.LawSeasons, false, 0f);
                    sim.Nature.Laws.Set(sim.Nature.LawClouds, false, 0f);
                    sim.Nature.Laws.Set(sim.Nature.LawAutoDisasters, false, 0f);
                    sim.Pipeline.Add(new FixedClimateControlSystem(map));
                }
                sim.SpawnInitialAnimals();
                Taken.Clear();
                int human = db.Species.IdOf("sp.human");
                int elf = db.Species.IdOf("sp.elf");
                int2 first = default;
                bool founders = scenario != null && scenario.StartsWith("founders", StringComparison.Ordinal); // report name carries the climate suffix
                bool spawned;
                if (founders)
                {
                    // Reproduce a small, balanced player-created cohort: seven adults of each species,
                    // with alternating sexes and tight same-species clusters for courtship/settlement.
                    spawned = SpawnSettlers(sim, human, 7, 9, default, 0f, out first) &&
                              SpawnSettlers(sim, elf, 7, 13, first, 48f, out _);
                }
                else
                {
                    spawned = SpawnSettlers(sim, human, 10, 9, default, 0f, out first) &&
                              (!war || SpawnSettlers(sim, db.Species.IdOf("sp.orc"), 10, 11, first, multi ? 90f : 60f, out _)); // Bölüm 6.5: a neighbour to fight
                    if (multi) // Bölüm 6.15 #1: four civilized species on one continent
                    {
                        spawned &= SpawnSettlers(sim, elf, 10, 13, first, 70f, out _);
                        spawned &= SpawnSettlers(sim, db.Species.IdOf("sp.dwarf"), 10, 17, first, 70f, out _);
                    }
                }
                if (!spawned)
                {
                    Debug.LogError("[Civ] no open land for settlers");
                    return 1;
                }
                {
                    var p = sim.Units.Store.Tile(sim.Units.Store.Alive[sim.Units.Store.Alive.Length - 1]);
                    var zone = map.Zones[map.ZoneIndexOf(p.x, p.y)];
                    byte b = map.Biome[map.Index(p.x, p.y)];
                    Debug.Log($"[Civ] site {p} base {zone.BaseTemperatureC}C now {zone.TemperatureC}C biome {(b == 0 ? "-" : db.Biomes[b - 1].Id)} ground {db.Tiles[map.Ground[map.Index(p.x, p.y)]].Id}");
                }
                int founded = 0, completed = 0, destroyed = 0, famines = 0, climateFamines = 0, items = 0;
                var metrics = new SoakMetrics(settings.Seed, scenario, years);
                metrics.CaptureInitialPopulation(sim);
                metrics.CaptureInitialEnvironment(sim);
                sim.Events.Subscribe<CityFoundedEvent>(e => founded++);
                sim.Events.Subscribe<BuildingCompletedEvent>(e => completed++);
                int groundLost = 0;
                var causes = new int[16];
                sim.Events.Subscribe<BuildingDestroyedEvent>(e => { destroyed++; if (e.GroundLost) groundLost++; });
                sim.Events.Subscribe<UnitDiedEvent>(e => { if (e.Species == human) causes[(int)e.Cause]++; });
                sim.Events.Subscribe<UnitDiedEvent>(e => metrics.RecordDeath(e.Species, e.Cause));
                sim.Events.Subscribe<UnitBornEvent>(e => metrics.RecordBirth(e.Species));
                int orc = db.Species.IdOf("sp.orc"), orcLogged = 0;
                if (war) sim.Events.Subscribe<UnitDiedEvent>(e =>
                {
                    if (e.Species != orc || orcLogged++ >= 12) return;
                    int killer = -1;
                    var us = sim.Units.Store;
                    for (int k = 0; k < us.HighWater; k++) if (us.Uid[k] == e.KillerUid && e.KillerUid != 0) { killer = us.Species[k]; break; }
                    Debug.Log($"[Civ] orc died y{sim.Clock.Year} {e.Cause} by {(killer >= 0 ? db.Species[killer].Id : "-")} at {e.Tile}");
                });
                var spDeaths = new System.Collections.Generic.Dictionary<string, int>();
                sim.Events.Subscribe<UnitDiedEvent>(e =>
                {
                    if (!db.Species[e.Species].IsCiv) return;
                    string key = db.Species[e.Species].Id + ":" + e.Cause;
                    spDeaths.TryGetValue(key, out int v); spDeaths[key] = v + 1;
                    if (e.KillerUid == 0) return;
                    var us = sim.Units.Store;
                    for (int q = 0; q < us.HighWater; q++)
                        if (us.Uid[q] == e.KillerUid) { key = "by " + db.Species[us.Species[q]].Id; spDeaths.TryGetValue(key, out v); spDeaths[key] = v + 1; break; }
                });
                sim.Events.Subscribe<UnitBornEvent>(e => { if (!db.Species[e.Species].IsCiv) return; string key = db.Species[e.Species].Id + ":born"; spDeaths.TryGetValue(key, out int v); spDeaths[key] = v + 1; });
                sim.Events.Subscribe<FamineEvent>(e =>
                {
                    famines++;
                    if (e.Cause != FamineCause.None) climateFamines++;
                    string zoneInfo = "";
                    if ((e.Cause & FamineCause.Drought) != 0)
                        foreach (int z in sim.Civ.CityZones[e.City])
                        {
                            var zd = sim.World.Zones[z];
                            if (zd.Drought == 0) continue;
                            zoneInfo = $" drought zone {z} moist {zd.Moisture} dry {zd.DryMonths} water {zd.WaterTiles} temp {zd.TemperatureC} base {zd.BaseTemperatureC}";
                            break;
                        }
                    Debug.Log($"[Civ] famine y{sim.Clock.Year} city {e.City} cause {e.Cause}{zoneInfo}");
                });
                sim.Events.Subscribe<ItemCraftedEvent>(e => items++);
                int kingdoms = 0, wars = 0, peaces = 0, captures = 0, rebellions = 0, alliances = 0;
                int acceptedTreaties = 0, vassalages = 0, tradeDeliveries = 0, tradeAmount = 0, seaTradeDeliveries = 0;
                sim.Events.Subscribe<KingdomFoundedEvent>(e => kingdoms++);
                sim.Events.Subscribe<MetaDiedEvent>(e =>
                {
                    if (e.Kind == MetaKind.Kingdom) metrics.RecordKingdomFall(e.Index);
                });
                sim.Events.Subscribe<WarDeclaredEvent>(e =>
                {
                    wars++;
                    if ((uint)e.War < (uint)sim.Meta.Wars.Count)
                    {
                        var war = sim.Meta.Wars.Items[e.War];
                        foreach (int kingdom in war.AttackerSide) metrics.RecordWarParticipation(kingdom);
                        foreach (int kingdom in war.DefenderSide) metrics.RecordWarParticipation(kingdom);
                    }
                    Debug.Log($"[Civ] war y{sim.Clock.Year}: {sim.Meta.KingdomName(e.Attacker)} -> {sim.Meta.KingdomName(e.Defender)}");
                });
                sim.Events.Subscribe<PeaceEvent>(e => peaces++);
                sim.Events.Subscribe<CityCapturedEvent>(e => { captures++; Debug.Log($"[Civ] capture y{sim.Clock.Year}: city {e.City} {e.From} -> {e.To}"); });
                sim.Events.Subscribe<RebellionEvent>(e => rebellions++);
                sim.Events.Subscribe<AllianceFormedEvent>(e => alliances++);
                sim.Events.Subscribe<VassalageFormedEvent>(e => vassalages++);
                sim.Events.Subscribe<DiplomacyProposalResolvedEvent>(e =>
                {
                    if (e.Accepted && e.Kind != ProposalKind.Peace && e.Kind != ProposalKind.Alliance) acceptedTreaties++;
                });
                sim.Events.Subscribe<TradeDeliveryEvent>(e =>
                {
                    tradeDeliveries++;
                    tradeAmount += e.Amount;
                    if (e.BySea) seaTradeDeliveries++;
                });
                int newKings = 0, plotsOk = 0, plotsFailed = 0;
                sim.Events.Subscribe<NewKingEvent>(e => newKings++);
                sim.Events.Subscribe<PlotFinishedEvent>(e => { if (e.Success) { plotsOk++; Debug.Log($"[Civ] plot y{sim.Clock.Year}: {db.Plots[sim.Meta.Plots.Items[e.Plot].Def].Id}"); } else plotsFailed++; });
                var disasters = new StringBuilder();
                sim.Events.Subscribe<DisasterRequestEvent>(e => disasters.Append(db.Disasters[e.Disaster].Id).Append('@').Append(sim.Clock.Year).Append(' '));
                int cityFireTicks = 0;

                var civ = sim.Civ;
                var sw = Stopwatch.StartNew();
                double civMs = 0, climateMs = 0, metaMs = 0;
                long ticks = 0;
                int maxTier = 0, maxPop = 0, maxElfPopulation = 0, maxKingdoms = 0;
                for (int year = 1; year <= years; year++)
                {
                    for (int t = 0; t < SimConst.TicksPerYear; t++)
                    {
                        sim.Tick();
                        ticks++;
                        for (int s = 0; s < sim.Pipeline.Count; s++)
                        {
                            if (sim.Pipeline[s].Phase == SimPhase.Civ) civMs += sim.Pipeline.LastMs(s);
                            else if (sim.Pipeline[s].Phase == SimPhase.Climate && !(sim.Pipeline[s] is FixedClimateControlSystem))
                                climateMs += sim.Pipeline.LastMs(s);
                            else if (sim.Pipeline[s].Phase == SimPhase.Meta) metaMs += sim.Pipeline.LastMs(s);
                        }
                        if (TraceOn && year == 35 && t % 240 == 0) Debug.Log($"[Civ] all t{t}: {Trace(sim)}");
                        if (TraceOn && year == 99 && t % 10 == 0) Debug.Log($"[Civ] one t{t}: {TraceOne(sim, sim.Content.Jobs.IdOf(Environment.GetEnvironmentVariable("PG_CIV_JOB") ?? "job.lumberjack"))}");
                        if (t % 30 == 0)
                            for (int k = 0; k < sim.Nature.Burning.Length; k++)
                            {
                                int bt = sim.Nature.Burning[k];
                                if ((map.Flags[bt] & (ushort)TileFlags.Burning) != 0 && civ.CityAtTile(bt % map.Width, bt / map.Width) >= 0) cityFireTicks++;
                            }
                    }
                    int pop = Count(sim, human);
                    maxPop = Math.Max(maxPop, pop);
                    int elfPop = founders ? Count(sim, elf) : 0;
                    maxElfPopulation = Math.Max(maxElfPopulation, elfPop);
                    for (int c = 0; c < civ.CityCount; c++) if (civ.CityAlive(c)) maxTier = Math.Max(maxTier, civ.City(c).HallTier);
                    int livingKingdoms = 0;
                    for (int k = 0; k < sim.Meta.Kingdoms.Count; k++) if (sim.Meta.KingdomAlive(k)) livingKingdoms++;
                    maxKingdoms = Math.Max(maxKingdoms, livingKingdoms);
                    metrics.Capture(sim, year, famines, wars, peaces, items, maxTier, climateFamines,
                        tradeDeliveries, tradeAmount, seaTradeDeliveries, acceptedTreaties, vassalages, rebellions);
                    if (year % 10 == 0 || year <= 12)
                    {
                        string populations = founders ? $"humans {pop}, elves {elfPop}" : $"humans {pop}";
                        Debug.Log($"[Civ] y{year}: {populations} in food {civ.StatFoodIn} (harvest {civ.StatHarvested}, lost {civ.StatLost}) wood {civ.StatWoodIn} stone {civ.StatStoneIn} | {Cities(sim)}");
                    }
                    TrackFoundations(sim.Civ, year);
                    if (year == 10 || year == 20 || year == 30 || year == 60 || PlannerEvery) Debug.Log($"[Civ] planner y{year}: {Planner(sim, year)}");
                    bool fighting = false;
                    for (int wi = 0; wi < sim.Meta.Wars.Count; wi++) fighting |= sim.Meta.Wars.IsAlive(wi);
                    if (year % 10 == 0 || fighting) Debug.Log($"[Civ] meta y{year}: {Kingdoms(sim)}");
                }
                var deaths = new StringBuilder();
                for (int k = 0; k < causes.Length; k++) if (causes[k] > 0) deaths.Append(' ').Append((DeathCause)k).Append(' ').Append(causes[k]);
                var ig = sim.Nature.IgniteStats;
                Debug.Log($"[Civ] fires: spread {ig[0]} spark {ig[1]} lightning {ig[2]} lava {ig[3]} unit {ig[4]} spell {ig[5]} power {ig[6]}");
                Debug.Log($"[Civ] human deaths:{deaths} | buildings destroyed by lost ground {groundLost} | burning city tiles (sampled) {cityFireTicks} | disasters: {disasters}");
                double climateMsPerTick = climateMs / ticks;
                double climateMsPerMonth = climateMs / (years * (double)SimConst.MonthsPerYear);
                Debug.Log($"[Civ] {years} years: founded {founded}, buildings done {completed}, destroyed {destroyed}, famines {famines} (climate-linked {climateFamines}), items {items} | " +
                          $"max hall tier {maxTier}, max humans {maxPop} | climate systems {climateMsPerTick:0.000} ms/tick, {climateMsPerMonth:0.000} ms/month; civ systems {civMs / ticks:0.000}, total {sw.Elapsed.TotalMilliseconds / ticks:0.000} ms/tick");
                Debug.Log($"[Civ] meta: kingdoms {kingdoms}, wars {wars}, peace {peaces}, captures {captures}, rebellions {rebellions}, alliances {alliances}, names {sim.Meta.Names.Count}");
                var m = sim.Meta;
                Debug.Log($"[Civ] culture: cultures {m.Cultures.Count}, languages {m.Languages.Count}, religions {m.Religions.Count}, clans {m.Clans.Count}, plots ok {plotsOk} failed {plotsFailed} active {m.Plots.Count - plotsOk - plotsFailed}, " +
                          $"books {m.Books.Length}, history {m.History.Length}, new kings {newKings}");
                var sd = new StringBuilder();
                foreach (var kv in spDeaths) sd.Append(kv.Key).Append(' ').Append(kv.Value).Append("; ");
                Debug.Log($"[Civ] civ species life: {sd}");
                metrics.SetSummary(founded, completed, destroyed, famines, wars, peaces, items, kingdoms, alliances,
                    maxTier, maxPop, maxKingdoms, civMs / ticks, sw.Elapsed.TotalMilliseconds / ticks, climateFamines, climateMsPerTick, climateMsPerMonth,
                    metaMs / ticks, tradeDeliveries, tradeAmount, seaTradeDeliveries, acceptedTreaties, vassalages, rebellions);
                string tag = Environment.GetEnvironmentVariable("PG_CIV_TAG") ?? $"{scenario ?? "single"}-{settings.Seed}-{years}y";
                string metricsPath = metrics.Write(Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", $"metrics-{tag}.json"));
                Debug.Log($"[Civ] metrics: {metricsPath}");
                bool ok = founded >= 1 && completed >= 5 && kingdoms >= 1;
                if (founders)
                {
                    spDeaths.TryGetValue("sp.human:born", out int humanBirths);
                    spDeaths.TryGetValue("sp.elf:born", out int elfBirths);
                    int finalHumans = Count(sim, human), finalElves = Count(sim, elf);
                    Debug.Log($"[Civ] founder cohort: humans peak {maxPop}, births {humanBirths}, final {finalHumans}; elves peak {maxElfPopulation}, births {elfBirths}, final {finalElves}");
                    const int minimumSurvivorsPerFounderSpecies = 7;
                    ok = years >= 100 && humanBirths > 0 && elfBirths > 0 &&
                         finalHumans >= minimumSurvivorsPerFounderSpecies && finalElves >= minimumSurvivorsPerFounderSpecies;
                }
                if (war && wars == 0) { Debug.LogError("[Civ] acceptance: expected a war between the two species"); ok = false; }
                if (multi && (wars + rebellions + plotsOk == 0 || newKings == 0)) { Debug.LogError("[Civ] acceptance 6.15 #1: expected a war, a rebellion or plot result and a new king"); ok = false; }
                if (founders)
                {
                    if (!ok) Debug.LogError("[Civ] founder acceptance: both seven-unit cohorts should reproduce and retain at least seven living members after at least 100 years");
                }
                else if (years >= 100 && !war)
                {
                    // Section 5.12 #1; only reported when its own condition fails.
                    bool growthOk = maxTier >= 2 && maxPop >= 60 && founded >= 2;
                    if (!growthOk) Debug.LogError("[Civ] acceptance: expected a city, hall_2+, 60+ people and a second city within 100 years");
                    ok &= growthOk;
                }
                else if (!ok && !war && !multi)
                    Debug.LogError("[Civ] acceptance: expected a founded city, five completed buildings and a kingdom");
                return ok ? 0 : 1;
            }
        }

        static string Kingdoms(SimWorld sim)
        {
            var meta = sim.Meta;
            var sb = new StringBuilder();
            for (int k = 0; k < meta.Kingdoms.Count; k++)
            {
                if (!meta.KingdomAlive(k)) continue;
                var d = meta.Kingdoms.Items[k];
                sb.Append($"{meta.KingdomName(k)} ({sim.Content.Species[d.Species].Id}) cities {d.Cities} soldiers {d.Soldiers} power {d.Power:0} wars {meta.ActiveWars(k)} king {(sim.Units.Store.IsAlive(d.King) ? meta.Names.Get(sim.Units.Store.NameId[d.King.Index]) : "-")}; ");
            }
            for (int a = 0; a < meta.Armies.Length; a++)
                if (meta.Armies[a].State != ArmyState.Disbanded) sb.Append($"army {a} k{meta.Armies[a].Kingdom} {meta.Armies[a].State} {meta.Armies[a].Soldiers}/{meta.Armies[a].StartSoldiers} -> city {meta.Armies[a].TargetCity}; ");
            for (int k = 0; k < meta.Kingdoms.Count; k++)
                for (int o = 0; o < meta.Kingdoms.Count; o++)
                    if (k != o && meta.KingdomAlive(k) && meta.KingdomAlive(o)) sb.Append($"op {k}->{o} {meta.Opinion(k, o)}; ");
            return sb.Length == 0 ? "no kingdoms" : sb.ToString();
        }

        static string Cities(SimWorld sim)
        {
            var civ = sim.Civ;
            var sb = new StringBuilder();
            for (int c = 0; c < civ.CityCount; c++)
            {
                if (!civ.CityAlive(c)) continue;
                ref var city = ref civ.City(c);
                int built = 0, sites = 0;
                foreach (int b in civ.CityBuildings[c])
                {
                    var d = civ.Buildings[b];
                    if ((d.Flags & BuildingFlags.Ruin) != 0) continue;
                    if (d.BuildProgress >= 1f) built++; else sites++;
                }
                var map = sim.World;
                int2 ct = math.clamp(city.Center, 0, new int2(map.Width - 1, map.Height - 1));
                sb.Append($"#{c} {map.Zones[map.ZoneIndexOf(ct.x, ct.y)].TemperatureC}C pop {city.Population}/{city.Housing} tier {city.HallTier} zones {civ.CityZones[c].Count} bld {built}+{sites} " +
                          $"food {civ.FoodTotal(c)} wood {civ.Res(c, civ.ResWood)} stone {civ.Res(c, civ.ResStone)} store {civ.StoredTotal(c)}/{city.StorageCap} " +
                          $"gold {city.Gold} mood {city.Happiness} jobs[{Jobs(sim, c)}] top[{TopRes(civ, c)}]; ");
            }
            return sb.Length == 0 ? "no cities" : sb.ToString();
        }

        static string TraceOne(SimWorld sim, int job)
        {
            var u = sim.Units.Store;
            for (int k = 0; k < u.Alive.Length; k++)
            {
                int i = u.Alive[k];
                if (u.Job[i] != job + 1) continue;
                return $"#{i} {(UnitTask)u.Task[i]}.{u.Action[i]} timer {u.Timer[i]} pos {u.Pos[i].x:0.0},{u.Pos[i].y:0.0} target {u.TargetTile[i]} path {u.PathHandle[i]} fails {u.PathFails[i]} next {u.NextThink[i] - sim.Clock.Tick} sat {u.Saturation[i]} en {u.Energy[i]} carry {u.CarryAmount[i]} stam {u.Stamina[i]:0}";
            }
            return "none";
        }

        static string Trace(SimWorld sim)
        {
            var u = sim.Units.Store;
            var civ = sim.Civ;
            var sb = new StringBuilder();
            for (int k = 0; k < u.Alive.Length; k++)
            {
                int i = u.Alive[k];
                if (!sim.Content.Species[u.Species[i]].IsCiv) continue;
                string job = u.Job[i] > 0 ? sim.Content.Jobs[u.Job[i] - 1].Id.Substring(4, 3) : "-";
                float dist = u.City[i] >= 0 ? math.distance(u.Pos[i], civ.City(u.City[i]).Center) : -1f;
                sb.Append(job).Append(':').Append((UnitTask)u.Task[i]).Append('.').Append(u.Action[i]).Append(" s").Append(u.Saturation[i])
                  .Append(" c").Append(u.CarryAmount[i]).Append(" d").Append(dist.ToString("0")).Append(" p").Append(u.PathHandle[i] >= 0 ? 1 : 0).Append(" | ");
            }
            int c0 = civ.CityCount > 0 ? 0 : -1;
            if (c0 >= 0) sb.Append("food ").Append(civ.FoodTotal(0)).Append(" wood ").Append(civ.Res(0, civ.ResWood));
            return sb.ToString();
        }

        // First year each unfinished foundation was seen (key: building slot + origin), for the planner dump.
        static readonly System.Collections.Generic.Dictionary<long, int> FoundationSeen = new System.Collections.Generic.Dictionary<long, int>();

        static long FoundationKey(int b, in BuildingData d) => ((long)b << 32) ^ ((long)d.Origin.x << 16) ^ (long)d.Origin.y;

        static void TrackFoundations(CivWorld civ, int year)
        {
            for (int c = 0; c < civ.CityCount; c++)
            {
                if (!civ.CityAlive(c)) continue;
                foreach (int b in civ.CityBuildings[c])
                {
                    var d = civ.Buildings[b];
                    if (d.BuildProgress >= 1f || (d.Flags & (BuildingFlags.Ruin | BuildingFlags.Dead)) != 0) continue;
                    FoundationSeen.TryAdd(FoundationKey(b, d), year);
                }
            }
        }

        // Unfinished buildings: def, progress, age in years, door/street walkability, and builders heading to the door.
        static string Foundations(SimWorld sim, int c, int year)
        {
            var civ = sim.Civ;
            var u = sim.Units.Store;
            var map = sim.World;
            int builders = 0;
            foreach (int r in civ.CityResidents[c]) if (u.Job[r] - 1 == civ.JobBuilder) builders++;
            var sb = new StringBuilder();
            sb.Append(" active ").Append(civ.ActiveConstructions(c)).Append(" builders ").Append(builders).Append(" found[");
            foreach (int b in civ.CityBuildings[c])
            {
                var d = civ.Buildings[b];
                if (d.BuildProgress >= 1f || (d.Flags & (BuildingFlags.Ruin | BuildingFlags.Dead)) != 0) continue;
                int2 door = d.Door, street = CityPlanner.Below(door);
                int toward = 0;
                foreach (int r in civ.CityResidents[c]) if (u.Job[r] - 1 == civ.JobBuilder && math.all(u.TargetTile[r] == door)) toward++;
                FoundationSeen.TryGetValue(FoundationKey(b, d), out int seen);
                bool streetOk = map.InBounds(street.x, street.y) && map.IsWalkable(street.x, street.y);
                var home = CityPlanner.Below(civ.City(c).Center);
                bool sameIsland = streetOk && map.InBounds(home.x, home.y) && sim.Regions.SameIsland(street.x, street.y, home.x, home.y, MoveClass.Land);
                sb.Append(civ.Content.Buildings[d.Def].ShortId).Append(" p").Append(d.BuildProgress.ToString("0.00")).Append(" age").Append(year - seen)
                  .Append(" dist").Append((int)math.distance((float2)door, civ.City(c).Center)).Append(" at").Append(door).Append(" street").Append(streetOk ? "Y" : "N").Append(" isl").Append(sameIsland ? "Y" : "N").Append(" door").Append(map.IsWalkable(door.x, door.y) ? "Y" : "N").Append(" bld").Append(toward).Append(' ');
            }
            sb.Append("] bstate[");
            foreach (int r in civ.CityResidents[c])
            {
                if (u.Job[r] - 1 != civ.JobBuilder) continue;
                sb.Append((UnitTask)u.Task[r]).Append('.').Append(u.Action[r]).Append(" t").Append(u.Timer[r]).Append(" f").Append(u.PathFails[r])
                  .Append(" to ").Append(u.TargetTile[r]).Append(" at ").Append(u.Tile(r)).Append(" sat").Append(u.Saturation[r]).Append("; ");
            }
            return sb.Append(']').ToString();
        }

        static string Planner(SimWorld sim, int year)
        {
            var civ = sim.Civ;
            var sb = new StringBuilder();
            var defs = new int[CityPlanner.MaxCandidates];
            var scores = new float[CityPlanner.MaxCandidates];
            var rng = new SimRandom(1, 1);
            for (int c = 0; c < civ.CityCount; c++)
            {
                if (!civ.CityAlive(c)) continue;
                int n = CityPlanner.Candidates(civ, sim.Units, c, defs, scores);
                sb.Append('#').Append(c).Append(Foundations(sim, c, year)).Append(':');
                for (int k = 0; k < n; k++)
                {
                    var d = sim.Content.Buildings[defs[k]];
                    bool site = CityPlanner.FindSite(civ, c, defs[k], ref rng, out _);
                    if (!site) sb.Append(" why[").Append(civ.IsHall(defs[k]) ? HallFailure(civ, c, d) : SiteFailures(civ, c, d)).Append(']');
                    sb.Append(' ').Append(d.ShortId).Append('(').Append(scores[k].ToString("0")).Append(civ.CanAfford(c, d) ? "" : " poor").Append(site ? "" : " nosite").Append(')');
                }
                sb.Append(" | ");
            }
            return sb.ToString();
        }

        // Why footprints fail: sampled over the city's zones with the planner's rules.
        static string HallFailure(CivWorld civ, int c, BuildingDef d)
        {
            var map = civ.Map;
            ref var city = ref civ.City(c);
            int old = city.CenterBuilding;
            var o = new int2(city.Center.x - d.TileW / 2, city.Center.y);
            var sb = new StringBuilder();
            sb.Append("center ").Append(city.Center).Append(" old ").Append(old).Append(": ");
            for (int y = o.y - 1; y <= o.y + d.TileH; y++)
                for (int x = o.x - 1; x <= o.x + d.TileW; x++)
                {
                    if (!map.InBounds(x, y)) { sb.Append("oob "); continue; }
                    int i = map.Index(x, y);
                    bool inside = x >= o.x && x < o.x + d.TileW && y >= o.y && y < o.y + d.TileH;
                    int other = map.Building[i];
                    if (other >= 0 && other != old && (civ.Buildings[other].Flags & BuildingFlags.Ruin) == 0) { sb.Append("B").Append(civ.Content.Buildings[civ.Buildings[other].Def].ShortId).Append('@').Append(x - o.x).Append(',').Append(y - o.y).Append(' '); continue; }
                    if (!inside) continue;
                    if ((map.Tables.TypeFlags[map.Ground[i]] & (ushort)TileFlags.Buildable) == 0) sb.Append("U").Append(civ.Content.Tiles[map.Ground[i]].Id.Substring(5)).Append('@').Append(x - o.x).Append(',').Append(y - o.y).Append(' ');
                    else if (map.Zones[map.ZoneIndexOf(x, y)].OwnerCity != c) sb.Append("O@").Append(x - o.x).Append(',').Append(y - o.y).Append(' ');
                    else if ((map.Flags[i] & (ushort)(TileFlags.Water | TileFlags.Burning)) != 0) sb.Append("F@").Append(x - o.x).Append(',').Append(y - o.y).Append(' ');
                    else if ((map.Flags[i] & (ushort)TileFlags.Reserved) != 0 && other != old) sb.Append("R").Append(other).Append('@').Append(x - o.x).Append(',').Append(y - o.y).Append(' ');
                    else if (map.Feature[i] != 0 && map.Tables.FeatureKind[map.Feature[i]] == FeatureDef.KindOre) sb.Append("ore@").Append(x - o.x).Append(',').Append(y - o.y).Append(' ');
                }
            var door = new int2(o.x + d.TileW / 2, o.y - 1);
            sb.Append(" door ").Append(door).Append(map.IsWalkable(door.x, door.y) ? " walk" : " BLOCKED").Append(map.IsWater(door.x, door.y) ? " water" : "");
            sb.Append(" free=").Append(civ.FootprintFree(o, d.TileW, d.TileH, c, old, false, clearOre: true, ringBuildingsOk: true));
            if (sb.Length > 300) sb.Length = 300;
            return sb.ToString();
        }

        static string SiteFailures(CivWorld civ, int c, BuildingDef d)
        {
            var map = civ.Map;
            int bounds = 0, building = 0, buildable = 0, owner = 0, ore = 0, door = 0, ok = 0;
            var rng = new SimRandom(3, 3);
            var zones = civ.CityZones[c];
            for (int s = 0; s < 400; s++)
            {
                int z = zones[rng.Range(0, zones.Count)];
                var o = new int2(((z % map.ZonesX) << WorldMap.ZoneShift) + rng.Range(0, WorldMap.ZoneSize) - d.TileW / 2,
                                 ((z / map.ZonesX) << WorldMap.ZoneShift) + rng.Range(0, WorldMap.ZoneSize) - d.TileH / 2);
                string why = null;
                for (int y = o.y - 1; y <= o.y + d.TileH && why == null; y++)
                    for (int x = o.x - 1; x <= o.x + d.TileW && why == null; x++)
                    {
                        if (!map.InBounds(x, y)) { why = "b"; break; }
                        int i = map.Index(x, y);
                        bool inside = x >= o.x && x < o.x + d.TileW && y >= o.y && y < o.y + d.TileH;
                        if (map.Building[i] >= 0) { why = "B"; break; }
                        if (!inside) continue;
                        ushort f = map.Flags[i];
                        if ((f & (ushort)TileFlags.Buildable) == 0 || (f & (ushort)(TileFlags.Water | TileFlags.Burning | TileFlags.Reserved)) != 0) why = "u";
                        else if (map.Zones[map.ZoneIndexOf(x, y)].OwnerCity != c) why = "o";
                        else if (map.Feature[i] != 0 && map.Tables.FeatureKind[map.Feature[i]] == FeatureDef.KindOre) why = "r";
                    }
                if (why == null && !civ.FootprintFree(o, d.TileW, d.TileH, c)) why = "d";
                switch (why) { case "b": bounds++; break; case "B": building++; break; case "u": buildable++; break; case "o": owner++; break; case "r": ore++; break; case "d": door++; break; default: ok++; break; }
            }
            return $"bounds {bounds} bld {building} unbuildable {buildable} owner {owner} ore {ore} door {door} ok {ok}";
        }

        static string TopRes(CivWorld civ, int c)
        {
            var sb = new StringBuilder();
            for (int r = 0; r < civ.ResCount; r++)
                if (civ.Res(c, r) >= 10) sb.Append(civ.Content.Resources[r].Id.Substring(4)).Append(' ').Append(civ.Res(c, r)).Append(' ');
            return sb.ToString();
        }

        static string Jobs(SimWorld sim, int c)
        {
            var u = sim.Units.Store;
            var counts = new int[sim.Content.Jobs.Count + 1];
            foreach (int i in sim.Civ.CityResidents[c]) counts[u.Job[i]]++;
            var sb = new StringBuilder();
            sb.Append("idle ").Append(counts[0]);
            for (int j = 0; j < sim.Content.Jobs.Count; j++)
                if (counts[j + 1] > 0) sb.Append(' ').Append(sim.Content.Jobs[j].Id.Substring(4)).Append(' ').Append(counts[j + 1]);
            return sb.ToString();
        }

        // Adults in a tight group on the most open buildable land near the map center.
        public static bool SpawnSettlers(SimWorld sim, int species, int count, ulong seed) => SpawnSettlers(sim, species, count, seed, default, 0f, out _);

        static readonly System.Collections.Generic.List<int2> Taken = new System.Collections.Generic.List<int2>(); // earlier sites of this run

        public static bool SpawnSettlers(SimWorld sim, int species, int count, ulong seed, int2 avoid, float minDist, out int2 site)
        {
            site = default;
            var map = sim.World;
            int2 best = new int2(-1);
            int bestScore = -1;
            for (int y = 16; y < map.Height - 16; y += 8)
                for (int x = 16; x < map.Width - 16; x += 8)
                {
                    // Bölüm 5.12 #1: an open temperate meadow (no fire/barren biome), with trees and hills within reach
                    var zone = map.Zones[map.ZoneIndexOf(x, y)];
                    if (zone.BaseTemperatureC < 8 || zone.BaseTemperatureC > 22) continue;
                    int score = 0, trees = 0;
                    bool hills = false, bad = false;
                    for (int dy = -20; dy <= 20; dy += 2)
                        for (int dx = -20; dx <= 20; dx += 2)
                        {
                            int tx = x + dx, ty = y + dy;
                            if (!map.InBounds(tx, ty)) continue;
                            int i = map.Index(tx, ty);
                            bool inner = math.abs(dx) <= 12 && math.abs(dy) <= 12;
                            if (inner && map.HasFlag(tx, ty, TileFlags.Buildable)) score++;
                            if (map.Feature[i] != 0 && map.Tables.FeatureKind[map.Feature[i]] == FeatureDef.KindTree) trees++;
                            int level = map.GetLevel(tx, ty);
                            if (level == 6 || level == 7) hills = true;
                            byte b = map.Biome[i];
                            if (inner && b != 0 && (map.Tables.BiomeEffectCode[b] == (byte)BiomeEffect.RandomFire || map.Tables.BiomeEffectCode[b] == (byte)BiomeEffect.NoPlants)) bad = true;
                        }
                    if (bad || minDist > 0f && math.distance(new float2(x, y), avoid) < minDist) continue;
                    bool near = false;
                    foreach (var t in Taken) near |= minDist > 0f && math.distance(new float2(x, y), t) < minDist;
                    if (near) continue;
                    score += math.min(trees, 20) + (hills ? 30 : 0);
                    score -= (math.abs(x - map.Width / 2) + math.abs(y - map.Height / 2)) / 32;
                    if (score > bestScore) { bestScore = score; best = new int2(x, y); }
                }
            if (best.x < 0) return false;
            site = best;
            Taken.Add(best);
            var rng = new SimRandom(seed, 7);
            var sp = sim.Content.Species[species];
            for (int n = 0, tries = 0; n < count && tries < count * 40; tries++)
            {
                int x = best.x + rng.Range(-4, 5), y = best.y + rng.Range(-4, 5);
                if (!map.IsWalkable(x, y) || map.IsWater(x, y)) continue;
                sim.Units.Spawn(new UnitSpawnRequest
                {
                    Species = species, Pos = new float2(x + 0.5f, y + 0.5f), AgeYears = sp.BaseStats[(int)StatId.Lifespan] * rng.Range(0.25f, 0.4f),
                    Sex = n % 2, Mother = PG.Core.EntityId.None, Father = PG.Core.EntityId.None, Subspecies = -1,
                }, sim.Clock.Tick, ref rng);
                n++;
            }
            return true;
        }

        static int Count(SimWorld sim, int species)
        {
            int c = 0;
            var u = sim.Units.Store;
            for (int k = 0; k < u.Alive.Length; k++) if (u.Species[u.Alive[k]] == species && u.State[u.Alive[k]] == UnitStore.StateAlive) c++;
            return c;
        }
    }
}
