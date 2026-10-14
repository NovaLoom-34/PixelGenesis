using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using PG.Content;
using PG.Core;
using PG.Sim;
using UnityEngine;

namespace PG.EditorTools
{
    [Serializable]
    public sealed class SoakMetricsReport
    {
        public string Seed;
        public string Scenario;
        public int RequestedYears;
        public List<SpeciesPopulationMetrics> InitialPopulationBySpecies = new List<SpeciesPopulationMetrics>();
        public int InitialVegetationTiles;
        public float InitialVegetationCoveragePercent;
        public List<SoakYearMetrics> Samples = new List<SoakYearMetrics>();
        public SoakSummaryMetrics Summary = new SoakSummaryMetrics();
    }

    [Serializable]
    public sealed class SoakYearMetrics
    {
        public int Year;
        public int Population;
        public int WildPopulation;
        public int PredatorPopulation;
        public int PreyPopulation;
        public int MaxWildSpeciesPopulation;
        public int VegetationTiles;
        public float VegetationCoveragePercent;
        public int Cities;
        public int MarketPriceSampleCount;
        public float AverageMarketPriceDeviation;
        public long TotalGold;
        public SoakGoldFlowMetrics GoldFlow = new SoakGoldFlowMetrics();
        public int LivingKingdoms;
        public int ActiveWars;
        public int LongestActiveWarYears;
        public float LargestKingdomPopulationShare;
        public List<SoakKingdomPopulationMetrics> KingdomPopulations = new List<SoakKingdomPopulationMetrics>();
        public int HumanPopulation;
        public int FamineEvents, ClimateLinkedFamineEvents;
        public int DeclaredWars;
        public int PeaceEvents;
        public int CraftedItems;
        public int TradeDeliveries, TradeAmount, SeaTradeDeliveries;
        public int AcceptedTreaties, VassalagesFormed, Rebellions;
        public int MaxHallTier;
        public int MaxSpeciesPopulation;
        public List<SpeciesPopulationMetrics> PopulationBySpecies = new List<SpeciesPopulationMetrics>();
        public List<SoakCityMetrics> CitiesById = new List<SoakCityMetrics>();
    }

    [Serializable]
    public sealed class SoakKingdomPopulationMetrics
    {
        public int KingdomId, Population, WarsParticipated;
        public float PopulationShare;
        public bool Alive, Fell;
    }

    [Serializable]
    public sealed class SoakFoodFlowMetrics
    {
        public int AddedToStock, EatenFromStock, EatenFromCarry, Spoiled, Overflow, StockChange;
    }

    [Serializable]
    public sealed class SoakGoldFlowMetrics
    {
        public long Income, Expenses, TransfersIn, TransfersOut, RouteFees, NetChange;
        public long MaintenanceDue, MaintenancePaid, MaintenanceUnpaid;
    }

    [Serializable]
    public sealed class SoakCityMetrics
    {
        public int CityId;
        public int Population, Housing, Unhoused, AdultPopulation, IdleAdults;
        public int Zones, ZoneCap, HallTier, ActiveConstructions, PlanWaits, FamineMonths;
        public bool NextHallAvailable, NextHallAffordable, NextHallHasSite;
        public string NextHallNeed;
        public List<SoakCountMetric> NextHallMissingResources = new List<SoakCountMetric>();
        public int FoodProducedLastYear, FoodStock, StoredTotal, StorageCap, Gold;
        public SoakGoldFlowMetrics GoldFlow = new SoakGoldFlowMetrics();
        public string TopBuildingNeed;
        public bool TopNeedAffordable, TopNeedHasSite;
        public List<SoakCountMetric> Jobs = new List<SoakCountMetric>();
        public List<SoakCountMetric> Resources = new List<SoakCountMetric>();
        public SoakFoodFlowMetrics FoodFlow = new SoakFoodFlowMetrics();
    }

    [Serializable]
    public sealed class SoakCountMetric
    {
        public string Id;
        public int Count;
    }

    [Serializable]
    public sealed class SpeciesPopulationMetrics
    {
        public string Species;
        public bool IsCiv;
        public bool IsPredator;
        public bool IsPrey;
        public int Population;
        public int Female, Male, OtherSex;
        public int Adults, Juveniles;
        public int Births, Deaths;
        public List<SpeciesDeathCauseMetrics> DeathsByCause = new List<SpeciesDeathCauseMetrics>();
    }

    [Serializable]
    public sealed class SpeciesDeathCauseMetrics
    {
        public string Cause;
        public int Count;
    }

    [Serializable]
    public sealed class SoakSummaryMetrics
    {
        public int FoundedCities;
        public int CompletedBuildings;
        public int DestroyedBuildings;
        public int FamineEvents, ClimateLinkedFamineEvents;
        public int DeclaredWars;
        public int PeaceEvents;
        public int CraftedItems;
        public int KingdomsFounded;
        public int AlliancesFormed;
        public int AcceptedTreaties, VassalagesFormed, Rebellions;
        public int TradeDeliveries, SeaTradeDeliveries, TradeAmount;
        public int MaxContinuousWarPairYears;
        public float MaxLargestKingdomPopulationShare;
        public int MaxHallTier;
        public int MaxHumanPopulation;
        public int MaxLivingKingdoms;
        public double CivMillisecondsPerTick;
        public double ClimateMillisecondsPerTick;
        public double ClimateMillisecondsPerMonth;
        public double MetaMillisecondsPerTick;
        public double TotalMillisecondsPerTick;
    }

    // Captures deterministic end-of-year state and event counters for machine comparisons.
    // It is editor-only measurement code and never participates in simulation decisions.
    public sealed class SoakMetrics
    {
        static readonly int DeathCauseCount = Enum.GetValues(typeof(DeathCause)).Length;
        readonly SoakMetricsReport _report;
        readonly Dictionary<int, int[]> _lastFoodFlow = new Dictionary<int, int[]>();
        readonly Dictionary<int, long[]> _lastGoldFlow = new Dictionary<int, long[]>();
        readonly Dictionary<int, int> _birthsBySpecies = new Dictionary<int, int>();
        readonly Dictionary<int, int[]> _deathsBySpecies = new Dictionary<int, int[]>();
        readonly Dictionary<int, int> _warsByKingdom = new Dictionary<int, int>();
        readonly HashSet<int> _fallenKingdoms = new HashSet<int>();
        float _maxKingdomShare;
        int _maxWarPairYears;

        public SoakMetrics(ulong seed, string scenario, int years)
        {
            _report = new SoakMetricsReport
            {
                Seed = seed.ToString(CultureInfo.InvariantCulture),
                Scenario = string.IsNullOrWhiteSpace(scenario) ? "single" : scenario,
                RequestedYears = years,
            };
        }

        public void CaptureInitialPopulation(SimWorld sim)
        {
            _report.InitialPopulationBySpecies.Clear();
            var units = sim.Units.Store;
            var db = sim.Content;
            BuildWildlifeRoles(db, out bool[] predators, out bool[] prey);
            var bySpecies = new Dictionary<string, SpeciesPopulationMetrics>();
            for (int n = 0; n < units.Alive.Length; n++)
            {
                int i = units.Alive[n];
                int species = units.Species[i];
                if (units.State[i] != UnitStore.StateAlive || species < 0 || species >= db.Species.Count) continue;
                var definition = db.Species[species];
                if (!bySpecies.TryGetValue(definition.Id, out var metric))
                {
                    metric = new SpeciesPopulationMetrics
                    {
                        Species = definition.Id,
                        IsCiv = definition.IsCiv,
                        IsPredator = predators[species],
                        IsPrey = prey[species],
                    };
                    bySpecies.Add(definition.Id, metric);
                }
                metric.Population++;
                if (units.Sex[i] == 0) metric.Female++;
                else if (units.Sex[i] == 1) metric.Male++;
                else metric.OtherSex++;
                if ((AgeStage)units.Age[i] >= AgeStage.Adult) metric.Adults++;
                else metric.Juveniles++;
            }
            foreach (var pair in bySpecies) _report.InitialPopulationBySpecies.Add(pair.Value);
            _report.InitialPopulationBySpecies.Sort((a, b) => string.CompareOrdinal(a.Species, b.Species));
        }

        public void CaptureInitialEnvironment(SimWorld sim)
        {
            int tiles = CountVegetationTiles(sim);
            _report.InitialVegetationTiles = tiles;
            _report.InitialVegetationCoveragePercent = 100f * tiles / Math.Max(1, sim.World.TileCount);
        }

        public void Capture(SimWorld sim, int year, int famines, int wars, int peaces, int items, int maxTier, int climateFamines = 0,
            int tradeDeliveries = 0, int tradeAmount = 0, int seaTradeDeliveries = 0, int acceptedTreaties = 0,
            int vassalagesFormed = 0, int rebellions = 0)
        {
            int vegetationTiles = CountVegetationTiles(sim);
            var sample = new SoakYearMetrics
            {
                Year = year,
                VegetationTiles = vegetationTiles,
                VegetationCoveragePercent = 100f * vegetationTiles / Math.Max(1, sim.World.TileCount),
                FamineEvents = famines,
                ClimateLinkedFamineEvents = climateFamines,
                DeclaredWars = wars,
                PeaceEvents = peaces,
                CraftedItems = items,
                MaxHallTier = maxTier,
                TradeDeliveries = tradeDeliveries,
                TradeAmount = tradeAmount,
                SeaTradeDeliveries = seaTradeDeliveries,
                AcceptedTreaties = acceptedTreaties,
                VassalagesFormed = vassalagesFormed,
                Rebellions = rebellions,
            };

            var db = sim.Content;
            var units = sim.Units.Store;
            BuildWildlifeRoles(db, out bool[] predators, out bool[] prey);
            var populationBySpecies = new Dictionary<string, SpeciesPopulationMetrics>();
            for (int n = 0; n < units.Alive.Length; n++)
            {
                int i = units.Alive[n];
                int species = units.Species[i];
                if (units.State[i] != UnitStore.StateAlive || species < 0 || species >= db.Species.Count) continue;
                var definition = db.Species[species];
                string id = definition.Id;
                if (!populationBySpecies.TryGetValue(id, out var speciesMetrics))
                {
                    speciesMetrics = new SpeciesPopulationMetrics
                    {
                        Species = id,
                        IsCiv = definition.IsCiv,
                        IsPredator = predators[species],
                        IsPrey = prey[species],
                    };
                    populationBySpecies.Add(id, speciesMetrics);
                }
                speciesMetrics.Population++;
                if (units.Sex[i] == 0) speciesMetrics.Female++;
                else if (units.Sex[i] == 1) speciesMetrics.Male++;
                else speciesMetrics.OtherSex++;
                if ((AgeStage)units.Age[i] >= AgeStage.Adult) speciesMetrics.Adults++;
                else speciesMetrics.Juveniles++;
                if (definition.IsCiv)
                {
                    sample.Population++;
                    if (speciesMetrics.Population > sample.MaxSpeciesPopulation) sample.MaxSpeciesPopulation = speciesMetrics.Population;
                    if (id == "sp.human") sample.HumanPopulation++;
                }
                else
                {
                    sample.WildPopulation++;
                    if (predators[species]) sample.PredatorPopulation++;
                    if (prey[species]) sample.PreyPopulation++;
                }
            }
            for (int species = 0; species < db.Species.Count; species++)
            {
                bool hasBirths = _birthsBySpecies.TryGetValue(species, out int births);
                bool hasDeaths = _deathsBySpecies.TryGetValue(species, out int[] deaths);
                if (!hasBirths && !hasDeaths) continue;
                string id = db.Species[species].Id;
                if (!populationBySpecies.TryGetValue(id, out var speciesMetrics))
                {
                    speciesMetrics = new SpeciesPopulationMetrics
                    {
                        Species = id,
                        IsCiv = db.Species[species].IsCiv,
                        IsPredator = predators[species],
                        IsPrey = prey[species],
                    };
                    populationBySpecies.Add(id, speciesMetrics);
                }
                speciesMetrics.Births = births;
                if (deaths == null) continue;
                for (int cause = 0; cause < deaths.Length; cause++)
                {
                    if (deaths[cause] == 0) continue;
                    speciesMetrics.Deaths += deaths[cause];
                    speciesMetrics.DeathsByCause.Add(new SpeciesDeathCauseMetrics
                    {
                        Cause = ((DeathCause)cause).ToString(),
                        Count = deaths[cause],
                    });
                }
            }
            _birthsBySpecies.Clear();
            _deathsBySpecies.Clear();
            foreach (var pair in populationBySpecies)
            {
                sample.PopulationBySpecies.Add(pair.Value);
                if (!pair.Value.IsCiv && pair.Value.Population > sample.MaxWildSpeciesPopulation)
                    sample.MaxWildSpeciesPopulation = pair.Value.Population;
            }
            sample.PopulationBySpecies.Sort((a, b) => string.CompareOrdinal(a.Species, b.Species));
            var civ = sim.Civ;
            for (int c = 0; c < civ.CityCount; c++)
            {
                ref var city = ref civ.City(c);
                sample.TotalGold += city.Gold;
                var goldFlow = civ.CityGoldFlows[c];
                _lastGoldFlow.TryGetValue(c, out long[] previousGoldFlow);
                var annualGoldFlow = new SoakGoldFlowMetrics
                {
                    Income = goldFlow.Income - (previousGoldFlow == null ? 0 : previousGoldFlow[0]),
                    Expenses = goldFlow.Expenses - (previousGoldFlow == null ? 0 : previousGoldFlow[1]),
                    TransfersIn = goldFlow.TransfersIn - (previousGoldFlow == null ? 0 : previousGoldFlow[2]),
                    TransfersOut = goldFlow.TransfersOut - (previousGoldFlow == null ? 0 : previousGoldFlow[3]),
                    RouteFees = goldFlow.RouteFees - (previousGoldFlow == null ? 0 : previousGoldFlow[4]),
                    MaintenanceDue = goldFlow.MaintenanceDue - (previousGoldFlow == null ? 0 : previousGoldFlow[5]),
                    MaintenancePaid = goldFlow.MaintenancePaid - (previousGoldFlow == null ? 0 : previousGoldFlow[6]),
                    MaintenanceUnpaid = goldFlow.MaintenanceUnpaid - (previousGoldFlow == null ? 0 : previousGoldFlow[7]),
                };
                annualGoldFlow.NetChange = annualGoldFlow.Income - annualGoldFlow.Expenses
                    + annualGoldFlow.TransfersIn - annualGoldFlow.TransfersOut - annualGoldFlow.RouteFees;
                sample.GoldFlow.Income += annualGoldFlow.Income;
                sample.GoldFlow.Expenses += annualGoldFlow.Expenses;
                sample.GoldFlow.TransfersIn += annualGoldFlow.TransfersIn;
                sample.GoldFlow.TransfersOut += annualGoldFlow.TransfersOut;
                sample.GoldFlow.RouteFees += annualGoldFlow.RouteFees;
                sample.GoldFlow.NetChange += annualGoldFlow.NetChange;
                sample.GoldFlow.MaintenanceDue += annualGoldFlow.MaintenanceDue;
                sample.GoldFlow.MaintenancePaid += annualGoldFlow.MaintenancePaid;
                sample.GoldFlow.MaintenanceUnpaid += annualGoldFlow.MaintenanceUnpaid;
                _lastGoldFlow[c] = new[]
                {
                    goldFlow.Income, goldFlow.Expenses, goldFlow.TransfersIn, goldFlow.TransfersOut,
                    goldFlow.RouteFees, goldFlow.MaintenanceDue, goldFlow.MaintenancePaid, goldFlow.MaintenanceUnpaid,
                };
                if (!civ.CityAlive(c)) continue;
                sample.Cities++;
                var cityMetrics = new SoakCityMetrics
                {
                    CityId = c,
                    Population = city.Population,
                    Housing = city.Housing,
                    Unhoused = Math.Max(0, city.Population - city.Housing),
                    Zones = civ.CityZones[c].Count,
                    ZoneCap = CivWorld.MaxZonesPerCity,
                    HallTier = city.HallTier,
                    ActiveConstructions = civ.ActiveConstructions(c),
                    PlanWaits = city.PlanWaits,
                    FamineMonths = city.FamineMonths,
                    FoodProducedLastYear = city.FoodProducedLastYear,
                    FoodStock = civ.FoodTotal(c),
                    StoredTotal = civ.StoredTotal(c),
                    StorageCap = city.StorageCap,
                    Gold = city.Gold,
                    GoldFlow = annualGoldFlow,
                };
                if (civ.BldMarket >= 0 && civ.CountBuildings(c, civ.BldMarket, true) > 0)
                {
                    for (int resource = 0; resource < civ.ResCount; resource++)
                    {
                        sample.AverageMarketPriceDeviation += Math.Abs(
                            PG.Sim.KingdomSystem.TradePriceMultiplier(civ.Res(c, resource)) - 1f);
                        sample.MarketPriceSampleCount++;
                    }
                }
                int nextHall = city.HallTier + 1 < civ.Halls.Length ? civ.Halls[city.HallTier + 1] : -1;
                if (nextHall >= 0 && nextHall < db.Buildings.Count)
                {
                    var hall = db.Buildings[nextHall];
                    cityMetrics.NextHallAvailable = true;
                    cityMetrics.NextHallNeed = hall.Id;
                    cityMetrics.NextHallAffordable = civ.CanAfford(c, hall);
                    var hallRng = new SimRandom((ulong)(year + 1) ^ 0x48414c4CUL, (ulong)(c + 1));
                    cityMetrics.NextHallHasSite = CityPlanner.FindSite(civ, c, nextHall, ref hallRng, out _);
                    foreach (var cost in hall.Costs)
                    {
                        if (cost.Res < 0 || cost.Res >= civ.ResCount) continue;
                        int missing = Math.Max(0, cost.Amount - civ.Res(c, cost.Res));
                        if (missing > 0)
                            cityMetrics.NextHallMissingResources.Add(new SoakCountMetric { Id = db.Resources[cost.Res].Id, Count = missing });
                    }
                }
                var flow = civ.CityFoodFlows[c];
                _lastFoodFlow.TryGetValue(c, out int[] previous);
                cityMetrics.FoodFlow = new SoakFoodFlowMetrics
                {
                    AddedToStock = flow.AddedToStock - (previous == null ? 0 : previous[0]),
                    EatenFromStock = flow.EatenFromStock - (previous == null ? 0 : previous[1]),
                    EatenFromCarry = flow.EatenFromCarry - (previous == null ? 0 : previous[2]),
                    Spoiled = flow.Spoiled - (previous == null ? 0 : previous[3]),
                    Overflow = flow.Overflow - (previous == null ? 0 : previous[4]),
                    StockChange = cityMetrics.FoodStock - (previous == null ? 0 : previous[5]),
                };
                _lastFoodFlow[c] = new[] { flow.AddedToStock, flow.EatenFromStock, flow.EatenFromCarry, flow.Spoiled, flow.Overflow, cityMetrics.FoodStock };
                var jobCounts = new int[db.Jobs.Count + 1];
                foreach (int unit in civ.CityResidents[c])
                {
                    if (units.State[unit] != UnitStore.StateAlive) continue;
                    if ((AgeStage)units.Age[unit] >= AgeStage.Adult)
                    {
                        cityMetrics.AdultPopulation++;
                        if (units.Job[unit] == 0) cityMetrics.IdleAdults++;
                    }
                    if (units.Job[unit] < jobCounts.Length) jobCounts[units.Job[unit]]++;
                }
                for (int j = 0; j < db.Jobs.Count; j++)
                    if (jobCounts[j + 1] > 0) cityMetrics.Jobs.Add(new SoakCountMetric { Id = db.Jobs[j].Id, Count = jobCounts[j + 1] });
                for (int r = 0; r < civ.ResCount; r++)
                    if (civ.Res(c, r) > 0) cityMetrics.Resources.Add(new SoakCountMetric { Id = db.Resources[r].Id, Count = civ.Res(c, r) });

                var defs = new int[CityPlanner.MaxCandidates];
                var scores = new float[CityPlanner.MaxCandidates];
                int candidateCount = CityPlanner.Candidates(civ, sim.Units, c, defs, scores,
                    sim.Meta != null && city.Kingdom >= 0 ? sim.Meta.PolicyOf(city.Kingdom) : KingdomPolicy.Balanced);
                if (candidateCount > 0)
                {
                    int def = defs[0];
                    cityMetrics.TopBuildingNeed = db.Buildings[def].Id;
                    cityMetrics.TopNeedAffordable = civ.CanAfford(c, db.Buildings[def]);
                    var rng = new SimRandom((ulong)(year + 1), (ulong)(c + 1));
                    cityMetrics.TopNeedHasSite = CityPlanner.FindSite(civ, c, def, ref rng, out _);
                }
                sample.CitiesById.Add(cityMetrics);
            }
            if (sample.MarketPriceSampleCount > 0)
                sample.AverageMarketPriceDeviation /= sample.MarketPriceSampleCount;
            var kingdomPopulation = new Dictionary<int, int>();
            int totalCivPopulation = 0;
            for (int n = 0; n < units.Alive.Length; n++)
            {
                int unit = units.Alive[n];
                int species = units.Species[unit];
                if (units.State[unit] != UnitStore.StateAlive || species < 0 || species >= db.Species.Count || !db.Species[species].IsCiv) continue;
                totalCivPopulation++;
                int cityId = units.City[unit];
                int kingdom = civ.CityAlive(cityId) ? civ.KingdomOf(cityId) : -1;
                if (kingdom < 0 || !sim.Meta.KingdomAlive(kingdom)) continue;
                kingdomPopulation.TryGetValue(kingdom, out int population);
                kingdomPopulation[kingdom] = population + 1;
            }
            for (int k = 0; k < sim.Meta.Kingdoms.Count; k++)
            {
                bool alive = sim.Meta.KingdomAlive(k);
                kingdomPopulation.TryGetValue(k, out int population);
                _warsByKingdom.TryGetValue(k, out int warsParticipated);
                sample.KingdomPopulations.Add(new SoakKingdomPopulationMetrics
                {
                    KingdomId = k, Population = population, WarsParticipated = warsParticipated,
                    PopulationShare = (float)population / Math.Max(1, totalCivPopulation),
                    Alive = alive, Fell = _fallenKingdoms.Contains(k),
                });
                if (!alive) continue;
                sample.LivingKingdoms++;
                float share = (float)population / Math.Max(1, totalCivPopulation);
                if (share > sample.LargestKingdomPopulationShare) sample.LargestKingdomPopulationShare = share;
            }
            _maxKingdomShare = Math.Max(_maxKingdomShare, sample.LargestKingdomPopulationShare);
            for (int w = 0; w < sim.Meta.Wars.Count; w++)
            {
                if (!sim.Meta.Wars.IsAlive(w)) continue;
                sample.ActiveWars++;
                var war = sim.Meta.Wars.Items[w];
                int duration = Math.Max(0, (int)((sim.Clock.Tick - war.StartTick) / SimConst.TicksPerYear));
                if (duration > sample.LongestActiveWarYears) sample.LongestActiveWarYears = duration;
                for (int a = 0; a < war.AttackerSide.Length; a++)
                    for (int d = 0; d < war.DefenderSide.Length; d++)
                        if (war.AttackerSide[a] != war.DefenderSide[d] && duration > sample.LongestActiveWarYears)
                            sample.LongestActiveWarYears = duration;
            }
            _maxWarPairYears = Math.Max(_maxWarPairYears, sample.LongestActiveWarYears);
            _report.Samples.Add(sample);
        }

        static int CountVegetationTiles(SimWorld sim)
        {
            var map = sim.World;
            var definitions = sim.Content.Features;
            ushort deadTree = definitions.TryGet("feat.tree_dead", out var dead) ? dead.MapValue : (ushort)0;
            int count = 0;
            for (int i = 0; i < map.Feature.Length; i++)
            {
                ushort value = map.Feature[i];
                if (value == 0 || value == deadTree) continue;
                int definition = value - 1;
                if ((uint)definition >= (uint)definitions.Count) continue;
                byte kind = definitions[definition].KindCode;
                if (kind == FeatureDef.KindTree || kind == FeatureDef.KindPlant || kind == FeatureDef.KindCrop) count++;
            }
            return count;
        }

        static void BuildWildlifeRoles(ContentDB db, out bool[] predators, out bool[] prey)
        {
            predators = new bool[db.Species.Count];
            prey = new bool[db.Species.Count];
            for (int predator = 0; predator < db.Species.Count; predator++)
            {
                var definition = db.Species[predator];
                if (definition.IsCiv) continue;
                for (int target = 0; target < db.Species.Count; target++)
                {
                    if (db.Species[target].IsCiv || !definition.CanEatSpecies(target)) continue;
                    predators[predator] = true;
                    prey[target] = true;
                }
            }
        }

        public void RecordBirth(int species)
        {
            _birthsBySpecies.TryGetValue(species, out int count);
            _birthsBySpecies[species] = count + 1;
        }

        public void RecordDeath(int species, DeathCause cause)
        {
            if (!_deathsBySpecies.TryGetValue(species, out int[] causes))
            {
                causes = new int[DeathCauseCount];
                _deathsBySpecies.Add(species, causes);
            }
            int index = (int)cause;
            if ((uint)index < (uint)causes.Length) causes[index]++;
        }

        public void RecordWarParticipation(int kingdom)
        {
            if (kingdom < 0) return;
            _warsByKingdom.TryGetValue(kingdom, out int count);
            _warsByKingdom[kingdom] = count + 1;
        }

        public void RecordKingdomFall(int kingdom)
        {
            if (kingdom >= 0) _fallenKingdoms.Add(kingdom);
        }

        public void SetSummary(int founded, int completed, int destroyed, int famines, int wars, int peaces,
            int items, int kingdoms, int alliances, int maxTier, int maxHumanPop, int maxLivingKingdoms,
            double civMsPerTick, double totalMsPerTick, int climateFamines = 0, double climateMsPerTick = 0, double climateMsPerMonth = 0,
            double metaMsPerTick = 0, int tradeDeliveries = 0, int tradeAmount = 0, int seaTradeDeliveries = 0,
            int acceptedTreaties = 0, int vassalagesFormed = 0, int rebellions = 0)
        {
            _report.Summary = new SoakSummaryMetrics
            {
                FoundedCities = founded,
                CompletedBuildings = completed,
                DestroyedBuildings = destroyed,
                FamineEvents = famines,
                ClimateLinkedFamineEvents = climateFamines,
                DeclaredWars = wars,
                PeaceEvents = peaces,
                CraftedItems = items,
                KingdomsFounded = kingdoms,
                AlliancesFormed = alliances,
                AcceptedTreaties = acceptedTreaties,
                VassalagesFormed = vassalagesFormed,
                Rebellions = rebellions,
                TradeDeliveries = tradeDeliveries,
                TradeAmount = tradeAmount,
                SeaTradeDeliveries = seaTradeDeliveries,
                MaxContinuousWarPairYears = _maxWarPairYears,
                MaxLargestKingdomPopulationShare = _maxKingdomShare,
                MaxHallTier = maxTier,
                MaxHumanPopulation = maxHumanPop,
                MaxLivingKingdoms = maxLivingKingdoms,
                CivMillisecondsPerTick = civMsPerTick,
                ClimateMillisecondsPerTick = climateMsPerTick,
                ClimateMillisecondsPerMonth = climateMsPerMonth,
                MetaMillisecondsPerTick = metaMsPerTick,
                TotalMillisecondsPerTick = totalMsPerTick,
            };
        }

        public string Write(string path)
        {
            string fullPath = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllText(fullPath, JsonUtility.ToJson(_report, true));
            return fullPath;
        }
    }
}
