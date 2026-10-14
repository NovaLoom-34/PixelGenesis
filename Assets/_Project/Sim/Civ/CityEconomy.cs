using PG.Content;
using PG.Core;
using PG.World;
using Unity.Mathematics;

namespace PG.Sim
{
    public static class CropClimate
    {
        public static float GrowthFactor(in ZoneData zone, bool irrigated)
        {
            float temp = zone.TemperatureC;
            float tempPenalty = temp < 8f ? 8f - temp : temp > 30f ? temp - 30f : 0f;
            float temperature = math.clamp(1f - tempPenalty * 0.04f, 0.2f, 1f);
            float moisture = zone.Moisture / 255f;
            float water = irrigated ? math.lerp(0.65f, 1f, moisture) : math.lerp(0.25f, 1f, moisture);
            return temperature * water;
        }

        public static float YieldFactor(CivWorld civ, int city, in ZoneData zone)
        {
            int kingdom = civ.KingdomOf(city);
            bool irrigated = civ.HasInvention(kingdom, "inv.irrigation");
            float invention = civ.HasInvention(kingdom, "inv.plow") ? 1.4f : 1f;
            if (irrigated) invention *= 1.15f;
            if (civ.HasInvention(kingdom, "inv.watermill")) invention *= 1.15f;
            return GrowthFactor(zone, irrigated) * invention;
        }
    }

    // Bölüm 5.8: last 8 events per unit, each lasting 6 months.
    public static class Happiness
    {
        public const int EventMonths = 6;

        public static void Add(UnitStore u, int i, int hap)
        {
            if (hap < 0) return;
            ushort id = (ushort)(hap + 1);
            int slot = -1, oldest = -1, oldestLeft = int.MaxValue;
            for (int s = 0; s < UnitStore.HapSlots; s++)
            {
                int k = i * UnitStore.HapSlots + s;
                if (u.HapEvent[k] == id) { slot = k; break; }
                if (u.HapEvent[k] == 0 && slot < 0) slot = k;
                if (u.HapLeft[k] < oldestLeft) { oldestLeft = u.HapLeft[k]; oldest = k; }
            }
            if (slot < 0) slot = oldest;
            u.HapEvent[slot] = id;
            u.HapLeft[slot] = EventMonths;
        }

        // Ages the log by a month and returns the sum of the active events.
        public static int MonthlySum(UnitStore u, ContentDB db, int i)
        {
            int sum = 0;
            for (int s = 0; s < UnitStore.HapSlots; s++)
            {
                int k = i * UnitStore.HapSlots + s;
                int id = u.HapEvent[k] - 1;
                if (id < 0) continue;
                sum += db.HappinessEvents[id].Value;
                if (--u.HapLeft[k] == 0) u.HapEvent[k] = 0;
            }
            return sum;
        }
    }

    // Bölüm 5.5-5.6, 5.8: monthly housing, feeding, spoilage, famine, tax, crops, jobs, equipment and city mood.
    public sealed class CityEconomySystem : ISimSystem
    {
        public const float Spoilage = 0.04f, GranarySpoilage = 0.02f;
        public const float TaxPerResident = 0.1f;
        public const int FamineMonths = 3;

        public static float TaxRateMultiplier(float level) => 0.5f + math.clamp(level, 0f, 1f);
        public static int TaxMoodModifier(float level) => (int)math.round((0.5f - math.clamp(level, 0f, 1f)) * 20f);

        public SimPhase Phase => SimPhase.Civ;
        public int Order => 30;

        readonly int[] _quota = new int[64], _count = new int[64];
        int[] _priority;

        public static int LumberjackQuota(int population, int woodStock, int woodNeed)
        {
            int shortage = math.max(0, woodNeed - woodStock);
            return shortage > 0 ? math.min(math.max(2, population / 4), 1 + shortage / 40) : 0;
        }

        public void Tick(in SimContext ctx)
        {
            long tick = ctx.Clock.Tick;
            if (tick % SimConst.TicksPerMonth != 1) return; // one tick after the settlement recount
            var civ = ctx.Civ;
            ref var rng = ref ctx.Rng.Get(RngStream.Civ);
            for (int c = 0; c < civ.CityCount; c++)
            {
                if (!civ.CityAlive(c)) continue;
                Capacity(civ, c);
                Housing(civ, ctx.Units, c);
                Feed(ctx, civ, ctx.Units, c, ref rng);
                Spoil(civ, c, ctx.Nature.Season.SpoilageMul, ref rng);
                Tax(civ, c, ref rng);
                if (IsMaintenanceCollectionTick(tick))
                    PayAnnualMaintenance(civ, c, AnnualMaintenanceDue(civ, c));
                if (!ctx.Powers.Blighted(c, ctx.Clock.Year) || rng.Chance(0.2f)) Crops(civ, c, ctx.Nature.Season.CropGrowthMul, ref rng); // dis.famine: x0.2
                Jobs(civ, ctx.Units, c);
                Equip(civ, ctx.Units, c);
                Mood(civ, ctx.Units, c);
            }
            Craft.PickUp(ctx.Units, civ);
        }

        static void Capacity(CivWorld civ, int c)
        {
            ref var city = ref civ.City(c);
            int stores = civ.CountBuildings(c, civ.BldStorage, true) + civ.CountBuildings(c, civ.BldGranary, true);
            city.StorageCap = CivWorld.BaseStorage + stores * CivWorld.StoragePerBuilding + city.HallTier * CivWorld.StoragePerTier;
        }

        static void Housing(CivWorld civ, UnitWorld w, int c)
        {
            var u = w.Store;
            ref var city = ref civ.City(c);
            var buildings = civ.CityBuildings[c];
            float housing = 0f;
            foreach (int b in buildings)
            {
                var d = civ.Buildings[b];
                d.Residents = 0;
                civ.Buildings[b] = d;
                if (d.Complete) housing += civ.HouseCapacity[d.Def];
            }
            city.Housing = (int)housing;
            var residents = civ.CityResidents[c];
            foreach (int i in residents)
            {
                int h = u.HomeBuilding[i];
                if (h >= 0 && h < civ.Buildings.Length && civ.Buildings[h].Complete && civ.Buildings[h].City == c && civ.HouseCapacity[civ.Buildings[h].Def] > 0f)
                {
                    var d = civ.Buildings[h];
                    if (d.Residents < (int)civ.HouseCapacity[d.Def]) { d.Residents++; civ.Buildings[h] = d; continue; }
                }
                u.HomeBuilding[i] = -1;
            }
            foreach (int i in residents)
            {
                if (u.HomeBuilding[i] >= 0) continue;
                int home = -1;
                foreach (int b in buildings)
                {
                    var d = civ.Buildings[b];
                    if (d.Complete && civ.HouseCapacity[d.Def] > 0f && d.Residents < (int)civ.HouseCapacity[d.Def]) { home = b; break; }
                }
                if (home < 0)
                {
                    Happiness.Add(u, i, civ.HapHomeless);
                    continue;
                }
                var hd = civ.Buildings[home];
                hd.Residents++;
                civ.Buildings[home] = hd;
                u.HomeBuilding[i] = home;
                u.Home[i] = hd.Door;
                Happiness.Add(u, i, civ.HapNewHome);
            }
        }

        // Monthly top-up for hungry residents (they also eat whenever hungry, see UnitThinkSystem); DECISIONS #53.
        static void Feed(in SimContext ctx, CivWorld civ, UnitWorld w, int c, ref SimRandom rng)
        {
            var u = w.Store;
            ref var city = ref civ.City(c);
            bool starving = false;
            foreach (int i in civ.CityResidents[c])
            {
                if (u.Has(i, UnitFlags.NoNeeds) || u.Army[i] >= 0 || u.Saturation[i] >= 60) continue;
                int nutrition = civ.EatFromStore(u, i);
                if (nutrition < 0)
                {
                    starving = true;
                    Happiness.Add(u, i, civ.HapHungry);
                    continue;
                }
                if (nutrition >= 4) Happiness.Add(u, i, civ.HapAteWell);
            }
            if (!starving) { city.FamineMonths = 0; return; }
            if (++city.FamineMonths == FamineMonths) ctx.Events.Publish(new FamineEvent(c, ClimateCause(civ, c, ctx.Clock.Tick)));
        }

        public static FamineCause ClimateCause(CivWorld civ, int city, long tick)
        {
            bool drought = false, cold = false;
            foreach (int z in civ.CityZones[city])
            {
                var zone = civ.Map.Zones[z];
                drought |= zone.Drought != 0;
                cold |= zone.TemperatureC <= 2;
                if (drought && cold) break;
            }
            var cause = (drought ? FamineCause.Drought : FamineCause.None) | (cold ? FamineCause.Cold : FamineCause.None);
            if (civ.HasRecentFloodCropLoss(city, tick)) cause |= FamineCause.Flood;
            return cause;
        }

        static void Spoil(CivWorld civ, int c, float seasonMul, ref SimRandom rng)
        {
            float rate = (civ.CountBuildings(c, civ.BldGranary, true) > 0 ? GranarySpoilage : Spoilage) * seasonMul;
            bool salt = civ.Res(c, civ.ResSalt) > 0;
            foreach (int r in civ.FoodByNutrition)
            {
                int k = c * civ.ResCount + r;
                int amount = civ.Inventory[k];
                if (amount <= 0) continue;
                float loss = amount * rate * (salt && (r == civ.ResMeat || r == civ.ResFish) ? 0.5f : 1f);
                int whole = (int)loss;
                if (rng.Chance(loss - whole)) whole++;
                int spoiled = math.min(amount, whole);
                civ.Inventory[k] = amount - spoiled;
                civ.CityFoodFlows[c].Spoiled += spoiled;
            }
        }

        static void Tax(CivWorld civ, int c, ref SimRandom rng)
        {
            ref var city = ref civ.City(c);
            int kingdom = civ.KingdomOf(c);
            float taxModifier = civ.Meta != null && kingdom >= 0 && kingdom < civ.Meta.Kingdoms.Count && civ.Meta.KingdomAlive(kingdom)
                ? civ.Meta.TraitValue(civ.Meta.TraitGold, kingdom) : 0f;
            float taxPolicy = civ.Meta != null && kingdom >= 0 && kingdom < civ.Meta.Kingdoms.Count && civ.Meta.KingdomAlive(kingdom)
                ? civ.Meta.PolicyOf(kingdom).Tax : 0.5f;
            float gold = city.Population * TaxPerResident * TaxRateMultiplier(taxPolicy) * math.max(0f, 1f + taxModifier / 100f);
            int whole = (int)gold;
            if (rng.Chance(gold - whole)) whole++;
            city.Gold += whole;
            civ.RecordGoldIncome(c, whole);
        }

        public static bool IsMaintenanceCollectionTick(long tick)
        {
            if (tick < 0) return false;
            long collectionOffset = SimConst.TicksPerYear - SimConst.TicksPerMonth + 1;
            return tick % SimConst.TicksPerYear == collectionOffset;
        }

        public static long AnnualMaintenanceDue(CivWorld civ, int city)
        {
            if (city < 0 || city >= civ.CityBuildings.Count) return 0;
            long due = 0;
            foreach (int buildingIndex in civ.CityBuildings[city])
            {
                if (buildingIndex < 0 || buildingIndex >= civ.Buildings.Length) continue;
                var building = civ.Buildings[buildingIndex];
                if (!building.Complete || !building.Alive || building.Def >= civ.Content.Buildings.Count) continue;
                due += civ.Content.Buildings[building.Def].GoldUpkeepPerYear;
            }
            return due;
        }

        public static long PayAnnualMaintenance(CivWorld civ, int city, long due)
        {
            if (city < 0 || city >= civ.CityCount || due <= 0) return 0;
            long available = System.Math.Max(0L, civ.City(city).Gold);
            long paid = System.Math.Min(due, available);
            civ.City(city).Gold -= (int)paid;
            civ.RecordGoldMaintenance(city, due, paid);
            return paid;
        }

        // Crops on city land ripen a stage a month (sampled nature growth would take years, DECISIONS #53).
        static void Crops(CivWorld civ, int c, float growth, ref SimRandom rng)
        {
            if (civ.FeatWheat == 0) return;
            var map = civ.Map;
            foreach (int z in civ.CityZones[c])
            {
                float climateGrowth = growth * CropClimate.GrowthFactor(map.Zones[z], civ.HasInvention(civ.KingdomOf(c), "inv.irrigation"));
                int x0 = (z % map.ZonesX) << WorldMap.ZoneShift, y0 = (z / map.ZonesX) << WorldMap.ZoneShift;
                for (int y = y0; y < y0 + WorldMap.ZoneSize; y++)
                    for (int x = x0; x < x0 + WorldMap.ZoneSize; x++)
                    {
                        int i = map.Index(x, y);
                        if (map.Feature[i] != civ.FeatWheat) continue;
                        int stage = map.GetFeatureStage(i);
                        if (stage >= 3) continue;
                        int steps = (int)climateGrowth;
                        float partial = climateGrowth - steps;
                        if (partial > 0f && rng.Chance(partial)) steps++;
                        if (steps > 0) map.SetFeatureStage(x, y, math.min(3, stage + steps));
                    }
            }
        }

        // Bölüm 5.6 job quotas and assignment.
        void Jobs(CivWorld civ, UnitWorld w, int c)
        {
            var u = w.Store;
            var db = civ.Content;
            ref var city = ref civ.City(c);
            int pop = city.Population;
            int jobs = math.min(db.Jobs.Count, _quota.Length);
            for (int j = 0; j < jobs; j++) { _quota[j] = 0; _count[j] = 0; }

            void Q(int job, int n) { if (job >= 0 && job < jobs) _quota[job] = math.max(0, n); }
            int Slots(int def) => def < 0 ? 0 : civ.CountBuildings(c, def, true) * db.Buildings[def].JobSlots;

            var diet = db.Species[city.Species].Diet;
            bool meatEater = diet == Diet.Carn || diet == Diet.Blood; // more hunters; crops feed them at half value
            int food = civ.FoodTotal(c);
            Q(civ.JobBuilder, math.max(1, pop / 8) + civ.ActiveConstructions(c));
            int foodGoalEarly = math.min(pop * 12, city.StorageCap / 2);
            Q(civ.JobFarmer, math.min(Slots(civ.BldFarmShed), food < foodGoalEarly ? math.max(2, pop / 2) : math.max(2, pop / 5))); // farmers follow the need
            // doc: pop/10 while food < 2 years; pop/4 below one year so a young village does not starve (DECISIONS #53)
            int foodGoal = math.min(pop * 12, city.StorageCap / 2); // a full store cannot take more
            Q(civ.JobGatherer, food < foodGoal / 2 ? math.max(1, pop / 4) : food < foodGoal ? math.max(1, pop / 10) : 0);
            int woodNeed = 100 + PlannedCost(civ, c);
            Q(civ.JobLumberjack, LumberjackQuota(pop, civ.Res(c, civ.ResWood), woodNeed));
            int nextHall = city.HallTier + 1 < civ.Halls.Length ? civ.Halls[city.HallTier + 1] : -1;
            int stoneNeed = math.max(60, RequiredCost(civ, nextHall, civ.ResStone));
            int stoneShortage = math.max(0, stoneNeed - civ.Res(c, civ.ResStone));
            int quarryWorkers = stoneShortage > 0 && civ.CountBuildings(c, civ.BldMine, true) == 0 && city.HallTier >= 1
                ? math.min(math.max(2, pop / 6), 1 + stoneShortage / 20) : 0;
            Q(civ.JobMiner, Slots(civ.BldMine) + quarryWorkers);
            Q(civ.JobFisher, Slots(civ.BldFishingHut));
            Q(civ.JobHunter, meatEater ? math.max(2, pop / 6) : civ.Res(c, civ.ResLeather) < 10 || food < pop * 12 ? 1 + pop / 15 : 0);
            Q(civ.JobHerder, Slots(civ.BldPasture));
            Q(civ.JobSmith, Slots(civ.BldSmithy));
            Q(civ.JobBaker, Slots(civ.BldBakery));
            Q(civ.JobTrader, civ.TradeEnabled ? Slots(civ.BldMarket) : 0);
            bool atWar = civ.Meta != null && city.Kingdom >= 0 && civ.Meta.ActiveWars(city.Kingdom) > 0;
            Q(civ.JobWarrior, atWar ? math.max(2, pop / 5) : (int)(pop * 0.05f)); // Bölüm 6.5: cities arm while at war
            Q(civ.JobGuard, civ.CountBuildings(c, civ.BldWatchtower, true));
            Q(civ.JobPriest, Slots(db.Buildings.IdOrDefault("bld.temple")));
            Q(civ.JobScholar, Slots(db.Buildings.IdOrDefault("bld.library")));
            Q(civ.JobHealer, Slots(db.Buildings.IdOrDefault("bld.healers_house")));
            Q(civ.JobSailor, Slots(civ.BldDocks));

            var residents = civ.CityResidents[c];
            var leader = city.Leader;
            foreach (int i in residents)
            {
                int j = u.Job[i] - 1;
                bool leaderUnit = u.IsAlive(leader) && leader.Index == i;
                if (leaderUnit) { if (civ.JobLeader >= 0) u.Job[i] = (byte)(civ.JobLeader + 1); continue; }
                if (j < 0 || j >= jobs) continue;
                if ((AgeStage)u.Age[i] < AgeStage.Adult || _count[j] >= _quota[j]) { u.Job[i] = 0; continue; } // release the excess
                _count[j]++;
            }

            // Bölüm 6.5: at war workers outside food and construction are called up until the warrior quota is met
            int wj = civ.JobWarrior;
            if (atWar && wj >= 0 && wj < jobs)
                foreach (int i in residents)
                {
                    if (_count[wj] >= _quota[wj]) break;
                    int j = u.Job[i] - 1;
                    if (j < 0 || j == wj || j == civ.JobGuard || j == civ.JobBuilder || j == civ.JobFarmer || j == civ.JobLeader || j >= jobs) continue;
                    _count[j]--;
                    u.Job[i] = (byte)(wj + 1);
                    u.Action[i] = 0;
                    _count[wj]++;
                }

            // priority: food > construction > defense > production > culture
            if (_priority == null)
                _priority = new[] { civ.JobFarmer, civ.JobFisher, civ.JobGatherer, civ.JobHunter, civ.JobHerder, civ.JobBaker, civ.JobBuilder,
                                    civ.JobLumberjack, civ.JobMiner, civ.JobSmith, civ.JobWarrior, civ.JobGuard,
                                    civ.JobPriest, civ.JobScholar, civ.JobHealer, civ.JobTrader, civ.JobSailor };
            // one builder per active site first so construction never stops, then the doc order (DECISIONS #53)
            int reserved = civ.JobBuilder >= 0 ? math.min(_quota[civ.JobBuilder], math.max(1, civ.ActiveConstructions(c))) : 0;
            if (reserved > 0) FillUpTo(u, residents, leader, civ.JobBuilder, reserved);
            // pass 1: every needed job gets one worker, pass 2: the rest in priority order (DECISIONS #53)
            foreach (int j in _priority) if (j >= 0 && j < jobs && _count[j] == 0) FillUpTo(u, residents, leader, j, 1);
            foreach (int j in _priority) Fill(u, residents, leader, j);

            if (!u.IsAlive(leader) || u.City[leader.Index] != c) PickLeader(civ, u, c);
        }

        static int PlannedCost(CivWorld civ, int c)
        {
            int sum = 0;
            foreach (int b in civ.CityBuildings[c])
            {
                var d = civ.Buildings[b];
                if (d.BuildProgress < 1f && d.Alive) sum += civ.Content.Buildings[d.Def].CostTotal;
            }
            return sum;
        }

        static int RequiredCost(CivWorld civ, int def, int resource)
        {
            if (def < 0) return 0;
            foreach (var cost in civ.Content.Buildings[def].Costs)
                if (cost.Res == resource) return cost.Amount;
            return 0;
        }

        void FillUpTo(UnitStore u, System.Collections.Generic.List<int> residents, EntityId leader, int j, int limit)
        {
            int keep = _quota[j];
            _quota[j] = math.min(keep, limit);
            Fill(u, residents, leader, j);
            _quota[j] = keep;
        }

        void Fill(UnitStore u, System.Collections.Generic.List<int> residents, EntityId leader, int j)
        {
            if (j < 0 || j >= _quota.Length) return;
            foreach (int i in residents)
            {
                if (_count[j] >= _quota[j]) break;
                if (u.Job[i] != 0 || (AgeStage)u.Age[i] < AgeStage.Adult || (u.IsAlive(leader) && leader.Index == i)) continue;
                u.Job[i] = (byte)(j + 1);
                _count[j]++;
            }
        }

        static void PickLeader(CivWorld civ, UnitStore u, int c)
        {
            int best = -1;
            foreach (int i in civ.CityResidents[c])
            {
                if ((AgeStage)u.Age[i] < AgeStage.Adult) continue;
                if (best < 0 || u.Level[i] > u.Level[best] || (u.Level[i] == u.Level[best] && u.BirthTick[i] < u.BirthTick[best])) best = i;
            }
            civ.City(c).Leader = best >= 0 ? u.IdOf(best) : EntityId.None;
            if (best >= 0 && civ.JobLeader >= 0) u.Job[best] = (byte)(civ.JobLeader + 1);
        }

        // Warriors and guards take stock items into empty slots.
        static void Equip(CivWorld civ, UnitWorld w, int c)
        {
            var u = w.Store;
            var items = civ.Items;
            foreach (int i in civ.CityResidents[c])
            {
                int j = u.Job[i] - 1;
                if (j != civ.JobWarrior && j != civ.JobGuard) continue;
                for (int it = 0; it < items.Length; it++)
                {
                    var item = items[it];
                    if (item.Dead != 0 || item.Owner >= 0 || item.City != c) continue;
                    int slot = (int)civ.Content.EquipmentTypes[item.Type].SlotCode;
                    int k = i * UnitStore.EquipSlots + slot;
                    if (u.Equip[k] >= 0) continue;
                    u.Equip[k] = it;
                    item.Owner = i;
                    item.City = -1;
                    items[it] = item;
                    w.Events?.Publish(new ItemEquippedEvent(u.Uid[i], item.Uid, item.Quality));
                    u.StatsDirty[i] = 1;
                }
            }
        }

        static void Mood(CivWorld civ, UnitWorld w, int c)
        {
            var u = w.Store;
            var residents = civ.CityResidents[c];
            int sum = 0;
            foreach (int i in residents) sum += u.Happiness[i];
            int kingdom = civ.KingdomOf(c);
            float taxPolicy = civ.Meta != null && kingdom >= 0 && civ.Meta.KingdomAlive(kingdom) ? civ.Meta.PolicyOf(kingdom).Tax : 0.5f;
            int happiness = (residents.Count > 0 ? sum / residents.Count : 0) + TaxMoodModifier(taxPolicy);
            civ.City(c).Happiness = (short)math.clamp(happiness, -100, 100);
        }
    }

    // Bölüm 5.4 damage: burning footprints, lost ground (terraform, lava), expired ruins.
    public sealed class BuildingSystem : ISimSystem
    {
        public const int CheckEvery = 10;
        public const float BurnDamagePerTick = 4f;
        public const int RuinYears = 5;

        public SimPhase Phase => SimPhase.Civ;
        public int Order => 20;

        public void Tick(in SimContext ctx)
        {
            long tick = ctx.Clock.Tick;
            if (tick % CheckEvery != 0) return;
            var civ = ctx.Civ;
            var map = ctx.World;
            for (int b = 0; b < civ.Buildings.Length; b++)
            {
                var d = civ.Buildings[b];
                if (!d.Alive) continue;
                if ((d.Flags & BuildingFlags.Ruin) != 0)
                {
                    if (tick - d.RuinTick >= RuinYears * SimConst.TicksPerYear) civ.Remove(b);
                    continue;
                }
                bool burning = false, lost = false;
                for (int y = d.Origin.y; y < d.Origin.y + d.H && !lost; y++)
                    for (int x = d.Origin.x; x < d.Origin.x + d.W; x++)
                    {
                        int i = map.Index(x, y);
                        ushort f = map.Flags[i];
                        if ((f & (ushort)TileFlags.Burning) != 0) burning = true;
                        if ((f & (ushort)TileFlags.Water) != 0 || map.Tables.Active[map.Ground[i]] != 0 || (map.Tables.TypeFlags[map.Ground[i]] & (ushort)TileFlags.Buildable) == 0 && map.Ground[i] != civ.TileField)
                        {
                            lost = true;
                            break;
                        }
                    }
                var flags = burning ? d.Flags | BuildingFlags.Burning : d.Flags & ~BuildingFlags.Burning;
                if (flags != d.Flags) civ.Version++;
                d.Flags = flags;
                if (burning) d.Hp -= BurnDamagePerTick * CheckEvery;
                civ.Buildings[b] = d;
                if (lost || d.Hp <= 0f)
                {
                    civ.Ruin(b, tick);
                    ctx.Events.Publish(new BuildingDestroyedEvent(b, d.Def, lost));
                }
            }
        }
    }
}
