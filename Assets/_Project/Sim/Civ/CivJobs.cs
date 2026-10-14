using PG.Content;
using PG.Core;
using PG.World;
using Unity.Mathematics;

namespace PG.Sim
{
    // Bölüm 5.6 job task chains, run by UnitActSystem for UnitTask.Work.
    // Action codes: 0 pick a target, 1 walk, 2 work (Timer), 3 walk to plant (farmer), 100 carry to the store.
    public static class CivJobs
    {
        public const int ResourceRadius = 24, FarmRadius = 6, MaxCarry = 5, GatherLoad = 8, QuarryLoad = 6, QuarryTicks = 60, GiveUpTicks = 900;
        public const int PlantTicks = 60, HarvestTicks = 60, ChopTicks = 40, MineTicks = 200, GatherTicks = 40, FishTicks = 200,
                         HerdTicks = 200, SmithTicks = 400, BakeTicks = 200, IdleTicks = 200, PatrolWait = 60;
        public const int ActPick = 0, ActWalk = 1, ActWork = 2, ActWalkPlant = 3, ActCarry = 100;
        public const int EquipmentDurabilityPerUse = 1;

        public static float EquipmentYieldMultiplier(UnitWorld w, int unit, string behaviorKey)
        {
            var u = w.Store;
            var civ = w.Civ;
            if (civ == null || unit < 0 || unit >= u.Capacity) return 1f;
            int itemIndex = u.Equip[unit * UnitStore.EquipSlots + (int)EquipSlot.Weapon];
            if (itemIndex < 0 || itemIndex >= civ.Items.Length) return 1f;
            var item = civ.Items[itemIndex];
            if (item.Dead != 0 || itemIndex >= civ.ItemDurability.Length || civ.ItemDurability[itemIndex] == 0 || item.Type >= civ.Content.EquipmentTypes.Count) return 1f;
            var type = civ.Content.EquipmentTypes[item.Type];
            if (type.Behavior == null || !type.Behavior.TryGetValue(behaviorKey, out float bonusPct)) return 1f;
            float quality = item.Quality < civ.Content.ItemQualities.Count ? civ.Content.ItemQualities[item.Quality].Multiplier : 1f;
            float material = type.UsesMaterial && item.Material < civ.Content.Materials.Count
                ? civ.Content.Materials[item.Material].Multiplier : 1f;
            return math.max(0f, 1f + bonusPct * quality * material / 100f);
        }

        public static void WearEquipment(UnitWorld w, int unit, string behaviorKey)
        {
            var civ = w.Civ;
            var u = w.Store;
            if (civ == null || unit < 0 || unit >= u.Capacity) return;
            int slot = unit * UnitStore.EquipSlots + (int)EquipSlot.Weapon;
            int itemIndex = u.Equip[slot];
            if (itemIndex < 0 || itemIndex >= civ.Items.Length || itemIndex >= civ.ItemDurability.Length) return;
            var item = civ.Items[itemIndex];
            if (item.Dead != 0 || item.Type >= civ.Content.EquipmentTypes.Count) return;
            var type = civ.Content.EquipmentTypes[item.Type];
            if (type.Behavior == null || !type.Behavior.TryGetValue(behaviorKey, out float bonusPct) || bonusPct <= 0f) return;
            byte remaining = (byte)math.max(0, civ.ItemDurability[itemIndex] - EquipmentDurabilityPerUse);
            civ.ItemDurability[itemIndex] = remaining;
            if (remaining != 0) return;
            item.Dead = 1;
            civ.Items[itemIndex] = item;
            u.Equip[slot] = -1;
            u.StatsDirty[unit] = 1;
        }

        static int ApplyYieldMultiplier(int baseYield, float multiplier) =>
            math.max(baseYield, (int)math.round(baseYield * multiplier));

        public static bool Work(UnitWorld w, CivWorld civ, int i, float dt, long tick, ref SimRandom rng)
        {
            var u = w.Store;
            int c = u.City[i];
            if (c < 0 || !civ.CityAlive(c))
            {
                u.City[i] = -1;
                u.Job[i] = 0;
                return true;
            }
            if (u.Army[i] >= 0 && civ.Meta != null) return March(w, civ, i, dt);
            if (!civ.TradeEnabled && u.Job[i] - 1 == civ.JobTrader)
            {
                u.Dest[i] = new int2(-1, -1);
                u.Action[i] = ActPick;
                w.ClearPath(i);
                return ReturnToCity(w, civ, i, c, dt);
            }
            if (u.Dest[i].x >= 0 && u.Job[i] - 1 == civ.JobTrader) return Caravan(w, civ, i, c, dt, tick);
            if (u.CarryAmount[i] > 0 && u.Action[i] != ActCarry && u.Action[i] != ActWork) u.Action[i] = ActCarry;
            if (u.Action[i] == ActCarry) return Carry(w, civ, i, c, dt);

            int job = u.Job[i] - 1;
            if (job < 0 || job == civ.JobLeader) return ReturnToCity(w, civ, i, c, dt);
            if (job == civ.JobBuilder) return Builder(w, civ, i, c, dt, tick);
            if (job == civ.JobFarmer) return Farmer(w, civ, i, c, dt);
            if (job == civ.JobLumberjack) return Lumberjack(w, civ, i, c, dt);
            if (job == civ.JobGatherer) return Gatherer(w, civ, i, c, dt);
            if (job == civ.JobHunter) return Hunter(w, civ, i, c, dt, tick, ref rng);
            if (job == civ.JobMiner)
                return civ.NearestBuilding(c, civ.BldMine, u.Pos[i]) >= 0 ? AtBuilding(w, civ, i, c, civ.BldMine, MineTicks, dt, tick, ref rng) : Quarry(w, civ, i, c, dt);
            if (job == civ.JobFisher) return AtBuilding(w, civ, i, c, civ.BldFishingHut, FishTicks, dt, tick, ref rng);
            if (job == civ.JobHerder) return AtBuilding(w, civ, i, c, civ.BldPasture, HerdTicks, dt, tick, ref rng);
            if (job == civ.JobSmith) return AtBuilding(w, civ, i, c, civ.BldSmithy, SmithTicks, dt, tick, ref rng);
            if (job == civ.JobBaker) return AtBuilding(w, civ, i, c, civ.BldBakery, BakeTicks, dt, tick, ref rng);
            if (job == civ.JobWarrior || job == civ.JobGuard) return Patrol(w, civ, i, c, dt, ref rng);
            int def = civ.Content.Jobs[job].RequiredBuilding;
            return def >= 0 ? AtBuilding(w, civ, i, c, def, IdleTicks, dt, tick, ref rng) : ReturnToCity(w, civ, i, c, dt);
        }

        // Unhappy cities work slower (Bölüm 5.8: production x0.7 under -40).
        static short WorkTicks(CivWorld civ, int c, int ticks) => (short)(civ.City(c).Happiness < -40 ? ticks * 10 / 7 : ticks);

        static bool Walk(UnitWorld w, int i, float dt, out bool failed)
        {
            var u = w.Store;
            bool arrived = w.MoveTo(i, u.TargetTile[i], dt, out failed);
            if (++u.Timer[i] > GiveUpTicks) failed = true;
            return arrived;
        }

        public const float TradeMarkup = 1.3f;
        public const int TradeOpinionGain = 2;

        // Bölüm 5.9 caravan: price by buyer scarcity and return any unsold load to its source.
        static bool Caravan(UnitWorld w, CivWorld civ, int i, int c, float dt, long tick)
        {
            var u = w.Store;
            if (u.Action[i] != ActWalk) { u.TargetTile[i] = u.Dest[i]; u.Timer[i] = 0; u.Action[i] = ActWalk; w.ClearPath(i); }
            bool arrived = Walk(w, i, dt, out bool failed);
            if (!arrived && !failed) return false;
            if (failed)
            {
                int2 origin = CityPlanner.Below(civ.City(c).Center);
                if (math.any(u.Dest[i] != origin))
                {
                    u.Dest[i] = origin;
                    u.Action[i] = 0;
                    return true;
                }
                civ.AddRes(c, u.CarryRes[i], u.CarryAmount[i]);
                u.CarryAmount[i] = 0;
                u.Dest[i] = new int2(-1, -1);
                return true;
            }
            int partner = civ.CityAtTile(u.Dest[i].x, u.Dest[i].y);
            if (arrived && partner == c && u.CarryAmount[i] > 0)
            {
                civ.AddRes(c, u.CarryRes[i], u.CarryAmount[i]);
                u.CarryAmount[i] = 0;
                u.Dest[i] = new int2(-1, -1);
                return true;
            }
            if (arrived && partner >= 0 && civ.CityAlive(partner) && u.CarryAmount[i] > 0)
            {
                int res = u.CarryRes[i], amount = u.CarryAmount[i];
                int sellerKingdom = civ.KingdomOf(c), buyerKingdom = civ.KingdomOf(partner);
                bool safe = civ.Meta == null || sellerKingdom < 0 || buyerKingdom < 0 || !civ.Meta.AtWar(sellerKingdom, buyerKingdom)
                    && !civ.Meta.HasTreaty(TreatyKind.TradeEmbargo, sellerKingdom, buyerKingdom, tick);
                if (safe && res >= 0 && res < civ.ResCount)
                {
                    bool treaty = civ.Meta != null && sellerKingdom >= 0 && buyerKingdom >= 0
                        && civ.Meta.HasTreaty(TreatyKind.TradeAgreement, sellerKingdom, buyerKingdom, tick);
                    int affordable = amount;
                    int buyerStock = civ.Res(partner, res);
                    while (affordable > 0 && KingdomSystem.TradePayment(civ.Content.Resources[res].Value, affordable, buyerStock, treaty) > civ.City(partner).Gold) affordable--;
                    if (affordable > 0)
                    {
                        int stored = civ.StoredTotal(partner);
                        int free = math.max(0, civ.City(partner).StorageCap - stored);
                        if (!civ.Content.Resources[res].IsFood)
                            free = math.min(free, math.max(0, (int)(civ.City(partner).StorageCap * CivWorld.NonFoodShare) - (stored - civ.FoodTotal(partner))));
                        int deliverable = math.min(affordable, free);
                        float distance = math.distance(CityPlanner.Below(civ.City(c).Center), CityPlanner.Below(civ.City(partner).Center));
                        int payment = KingdomSystem.TradePayment(civ.Content.Resources[res].Value, deliverable, buyerStock, treaty);
                        int routeCost = treaty ? KingdomSystem.TradeAgreementRouteCost(distance, deliverable)
                            : KingdomSystem.TradeRouteCost(distance, deliverable);
                        int accepted = deliverable > 0 && payment > routeCost ? civ.AddRes(partner, res, deliverable) : 0;
                        payment = KingdomSystem.TradePayment(civ.Content.Resources[res].Value, accepted, buyerStock, treaty);
                        routeCost = treaty ? KingdomSystem.TradeAgreementRouteCost(distance, accepted)
                            : KingdomSystem.TradeRouteCost(distance, accepted);
                        if (accepted > 0)
                        {
                            w.Events?.Publish(new TradeDeliveryEvent(c, partner, res, accepted, false));
                            civ.City(partner).Gold -= payment;
                            civ.City(c).Gold += math.max(0, payment - routeCost);
                            civ.RecordGoldExpense(partner, payment);
                            civ.RecordGoldIncome(c, payment);
                            civ.RecordGoldRouteFee(c, routeCost);
                            int ka = civ.City(c).Kingdom, kb = civ.City(partner).Kingdom;
                            if (civ.Meta != null && ka >= 0 && kb >= 0 && ka != kb)
                            {
                                civ.Meta.SetOpinion(ka, kb, civ.Meta.Opinion(ka, kb) + TradeOpinionGain);
                                civ.Meta.SetOpinion(kb, ka, civ.Meta.Opinion(kb, ka) + TradeOpinionGain);
                            }
                            u.CarryAmount[i] -= (byte)accepted;
                        }
                    }
                }
                if (u.CarryAmount[i] > 0)
                {
                    u.Dest[i] = CityPlanner.Below(civ.City(c).Center);
                    u.Action[i] = 0;
                    return true;
                }
            }
            if (arrived && u.CarryAmount[i] > 0)
            {
                u.Dest[i] = CityPlanner.Below(civ.City(c).Center);
                u.Action[i] = 0;
                return true;
            }
            u.Dest[i] = new int2(-1, -1);
            return true;
        }

        public const float SiegeDamageFactor = 0.5f;
        public const int SiegeHitTicks = 20;

        // Bölüm 6.5 soldiers: rally, march in formation around the target center, siege (two in three hit buildings), go home.
        static bool March(UnitWorld w, CivWorld civ, int i, float dt)
        {
            var u = w.Store;
            int a = u.Army[i];
            var army = civ.Meta.Armies[a];
            int2 dest;
            switch (army.State)
            {
                case ArmyState.Gathering: dest = army.RallyTile; break;
                case ArmyState.Marching:
                case ArmyState.Sieging: dest = CityPlanner.Below(civ.City(army.TargetCity).Center) + new int2(i % 5 - 2, (i / 5) % 5 - 2); break;
                case ArmyState.Returning: dest = CityPlanner.Below(civ.City(army.HomeCity).Center); break;
                default: u.Army[i] = -1; return true;
            }
            if (army.State == ArmyState.Sieging && i % 3 != 0 && Siege(w, civ, i, army.TargetCity, dt)) return false;
            if (u.Action[i] != ActWalk || math.distancesq(u.TargetTile[i], dest) > 4)
            {
                u.TargetTile[i] = dest;
                u.Timer[i] = 0;
                u.Action[i] = ActWalk;
                w.ClearPath(i);
            }
            bool arrived = Walk(w, i, dt, out bool failed);
            return arrived || failed;
        }

        // true while busy with an enemy building (walking to it or hitting it at dmg x 0.5)
        static bool Siege(UnitWorld w, CivWorld civ, int i, int city, float dt)
        {
            var u = w.Store;
            int best = -1;
            float bestD = 16f * 16f;
            foreach (int b in civ.CityBuildings[city])
            {
                var d = civ.Buildings[b];
                if (!d.Alive || (d.Flags & BuildingFlags.Ruin) != 0) continue;
                float dd = math.distancesq(u.Pos[i], CityPlanner.Below(d.Door));
                if (dd < bestD) { bestD = dd; best = b; }
            }
            if (best < 0) return false;
            var door = CityPlanner.Below(civ.Buildings[best].Door);
            if (bestD > 2.5f * 2.5f)
            {
                if (u.Action[i] != ActWalk || math.any(u.TargetTile[i] != door)) { u.TargetTile[i] = door; u.Timer[i] = 0; u.Action[i] = ActWalk; w.ClearPath(i); }
                bool arrived = Walk(w, i, dt, out bool failed);
                if (failed) u.Action[i] = ActPick;
                return !failed || arrived;
            }
            u.Action[i] = ActWork;
            if (u.AttackCooldown[i] > 0) return true;
            var bd = civ.Buildings[best];
            bd.Hp -= u.Stat(i, StatId.Dmg) * SiegeDamageFactor;
            civ.Buildings[best] = bd;
            u.AttackCooldown[i] = SiegeHitTicks;
            return true;
        }

        static bool ReturnToCity(UnitWorld w, CivWorld civ, int i, int c, float dt)
        {
            var u = w.Store;
            if (u.Action[i] == ActPick)
            {
                var center = CityPlanner.Below(civ.City(c).Center);
                if (math.distance(u.Pos[i], center) < 12f) return true;
                u.TargetTile[i] = center;
                u.Timer[i] = 0;
                u.Action[i] = ActWalk;
            }
            bool arrived = Walk(w, i, dt, out bool failed);
            return arrived || failed;
        }

        static bool Carry(UnitWorld w, CivWorld civ, int i, int c, float dt)
        {
            var u = w.Store;
            int store = civ.NearestBuilding(c, -1, u.Pos[i]);
            if (store < 0)
            {
                Deposit(civ, u, i, c);
                return true;
            }
            var door = civ.Buildings[store].Door;
            if (math.distance(u.Pos[i], (float2)door + 0.5f) < 1.5f)
            {
                Deposit(civ, u, i, c);
                return true;
            }
            if (!math.all(u.TargetTile[i] == door)) { u.TargetTile[i] = door; u.Timer[i] = 0; w.ClearPath(i); }
            Walk(w, i, dt, out bool failed);
            if (failed) Deposit(civ, u, i, c); // lost the way: the load still counts (keeps the economy moving)
            return failed;
        }

        static void Deposit(CivWorld civ, UnitStore u, int i, int c)
        {
            civ.AddRes(c, u.CarryRes[i], u.CarryAmount[i]);
            u.CarryAmount[i] = 0;
        }

        static void Pick(UnitStore u, int i, int res, int amount)
        {
            if (u.CarryAmount[i] > 0 && u.CarryRes[i] != res) u.CarryAmount[i] = 0;
            u.CarryRes[i] = (ushort)res;
            u.CarryAmount[i] = (byte)math.min(255, u.CarryAmount[i] + amount);
        }

        // --- builder ---
        static bool Builder(UnitWorld w, CivWorld civ, int i, int c, float dt, long tick)
        {
            var u = w.Store;
            var map = w.Map;
            switch (u.Action[i])
            {
                case ActPick:
                    int site = -1;
                    float best = float.MaxValue;
                    foreach (int b in civ.CityBuildings[c])
                    {
                        var d = civ.Buildings[b];
                        if (d.BuildProgress >= 1f || !d.Alive || (d.Flags & BuildingFlags.Ruin) != 0) continue;
                        float dist = math.distancesq(u.Pos[i], d.Door);
                        if (dist < best) { best = dist; site = b; }
                    }
                    if (site < 0) return ReturnToCity(w, civ, i, c, dt);
                    u.TargetTile[i] = civ.Buildings[site].Door;
                    u.Timer[i] = 0;
                    u.Action[i] = ActWalk;
                    return false;
                case ActWalk:
                    bool arrived = Walk(w, i, dt, out bool failed);
                    if (failed) return true;
                    if (arrived) u.Action[i] = ActWork;
                    return false;
                default:
                    int2 t = u.TargetTile[i];
                    int bi = map.InBounds(t.x, t.y) ? map.Building[map.Index(t.x, t.y)] : -1;
                    if (bi < 0 || civ.Buildings[bi].BuildProgress >= 1f) return true;
                    return CityPlanner.Build(civ, w, bi, tick);
            }
        }

        // --- farmer: till and sow around the farm shed, harvest ripe wheat ---
        static bool Farmer(UnitWorld w, CivWorld civ, int i, int c, float dt)
        {
            var u = w.Store;
            var map = w.Map;
            switch (u.Action[i])
            {
                case ActPick:
                    CollectTargets(w, civ, i, c);
                    int shed = civ.NearestBuilding(c, civ.BldFarmShed, u.Pos[i]);
                    if (shed < 0 || civ.FeatWheat == 0) return ReturnToCity(w, civ, i, c, dt);
                    var center = civ.Buildings[shed].Door;
                    int2 harvest = new int2(-1), sow = new int2(-1);
                    for (int r = 1; r <= FarmRadius && harvest.x < 0; r++)
                        for (int dy = -r; dy <= r && harvest.x < 0; dy++)
                            for (int dx = -r; dx <= r; dx++)
                            {
                                if (math.max(math.abs(dx), math.abs(dy)) != r) continue;
                                int x = center.x + dx, y = center.y + dy;
                                if (!map.InBounds(x, y) || civ.CityAtTile(x, y) != c) continue;
                                int ti = map.Index(x, y);
                                if (Taken(ti)) continue;
                                if (map.Feature[ti] == civ.FeatWheat)
                                {
                                    if (map.GetFeatureStage(ti) >= 3) { harvest = new int2(x, y); break; }
                                    continue;
                                }
                                if (sow.x >= 0 || map.Feature[ti] != 0 || map.Building[ti] >= 0) continue;
                                ushort f = map.Flags[ti];
                                if ((f & (ushort)TileFlags.Road) != 0 || (f & (ushort)TileFlags.Walkable) == 0) continue;
                                if (map.Ground[ti] == civ.TileField || (f & (ushort)TileFlags.Buildable) != 0) sow = new int2(x, y);
                            }
                    if (harvest.x >= 0) { u.TargetTile[i] = harvest; u.Action[i] = ActWalk; }
                    else if (sow.x >= 0) { u.TargetTile[i] = sow; u.Action[i] = ActWalkPlant; }
                    else return ReturnToCity(w, civ, i, c, dt);
                    u.Timer[i] = 0;
                    return false;
                case ActWalk:
                case ActWalkPlant:
                    bool arrived = Walk(w, i, dt, out bool failed);
                    if (failed) return true;
                    if (!arrived) return false;
                    u.Timer[i] = (short)-(u.Action[i] == ActWalk ? WorkTicks(civ, c, HarvestTicks) : WorkTicks(civ, c, PlantTicks));
                    u.Action[i] = (byte)(u.Action[i] == ActWalk ? ActWork : ActWork + 2); // 2 harvest, 4 sow
                    return false;
                default:
                    if (++u.Timer[i] < 0) return false;
                    int2 t = u.TargetTile[i];
                    int tile = map.Index(t.x, t.y);
                    if (u.Action[i] == ActWork)
                    {
                        if (map.Feature[tile] != civ.FeatWheat || map.GetFeatureStage(tile) < 3) return true;
                        var climate = map.Zones[map.ZoneIndexOf(t.x, t.y)];
                        int amount = math.max(1, (int)(map.GetFeatureResource(tile) * CropClimate.YieldFactor(civ, c, climate))); // ripe-field yield follows weather and farming inventions
                        amount = ApplyYieldMultiplier(amount, EquipmentYieldMultiplier(w, i, "pct.farmYield"));
                        map.SetFeature(t.x, t.y, 0, ChangeSource.Unit);
                        civ.StatHarvested += amount;
                        Pick(u, i, civ.ResWheat, amount);
                        WearEquipment(w, i, "pct.farmYield");
                        u.Action[i] = ActCarry;
                        return false;
                    }
                    if (map.Feature[tile] != 0 || map.Building[tile] >= 0) return true;
                    if (map.Ground[tile] != civ.TileField) map.SetGround(t.x, t.y, civ.TileField, ChangeSource.Unit);
                    map.SetFeature(t.x, t.y, civ.FeatWheat, 0, ChangeSource.Unit);
                    return true;
            }
        }

        // --- lumberjack: chop grown trees near the city, carry up to 5 ---
        static bool Lumberjack(UnitWorld w, CivWorld civ, int i, int c, float dt)
        {
            var u = w.Store;
            var map = w.Map;
            switch (u.Action[i])
            {
                case ActPick:
                    CollectTargets(w, civ, i, c);
                    if (!FindFeature(w, civ, u.Pos[i], c, FeatureDef.KindTree, civ.ResWood, out int2 tree)) return ReturnToCity(w, civ, i, c, dt);
                    u.TargetTile[i] = tree;
                    u.Timer[i] = 0;
                    u.Action[i] = ActWalk;
                    return false;
                case ActWalk:
                    bool arrived = Walk(w, i, dt, out bool failed);
                    if (failed) return true;
                    if (arrived) { u.Action[i] = ActWork; u.Timer[i] = (short)-WorkTicks(civ, c, ChopTicks); }
                    return false;
                default:
                    if (++u.Timer[i] < 0) return false;
                    int2 t = u.TargetTile[i];
                    int tile = map.Index(t.x, t.y);
                    ushort f = map.Feature[tile];
                    if (f == 0 || civ.FeatureRes[f] != civ.ResWood) { u.Action[i] = u.CarryAmount[i] > 0 ? (byte)ActCarry : (byte)ActPick; return u.CarryAmount[i] == 0; }
                    int left = map.GetFeatureResource(tile) - 1;
                    if (left > 0) map.SetFeatureResource(t.x, t.y, left);
                    else map.SetFeature(t.x, t.y, 0, ChangeSource.Unit);
                    Pick(u, i, civ.ResWood, 1);
                    if (u.CarryAmount[i] >= MaxCarry || left <= 0) u.Action[i] = ActCarry;
                    else u.Timer[i] = (short)-WorkTicks(civ, c, ChopTicks);
                    return false;
            }
        }

        // --- miner without a mine: quarry surface rocks and veins (DECISIONS #53) ---
        static bool Quarry(UnitWorld w, CivWorld civ, int i, int c, float dt)
        {
            var u = w.Store;
            var map = w.Map;
            switch (u.Action[i])
            {
                case ActPick:
                    CollectTargets(w, civ, i, c);
                    // surface rocks first, else hill/mountain ground (resources.json: stone comes from hill and mountain quarrying)
                    if (!FindFeature(w, civ, u.Pos[i], c, FeatureDef.KindOre, civ.ResStone, out int2 rock) && !FindHill(w, civ, u.Pos[i], c, out rock))
                        return ReturnToCity(w, civ, i, c, dt);
                    u.TargetTile[i] = rock;
                    u.Timer[i] = 0;
                    u.Action[i] = ActWalk;
                    return false;
                case ActWalk:
                    bool arrived = Walk(w, i, dt, out bool failed);
                    if (failed) return true;
                    if (arrived) { u.Action[i] = ActWork; u.Timer[i] = (short)-WorkTicks(civ, c, QuarryTicks); }
                    return false;
                default:
                    if (++u.Timer[i] < 0) return false;
                    int2 t = u.TargetTile[i];
                    int tile = map.Index(t.x, t.y);
                    ushort f = map.Feature[tile];
                    int res = f != 0 && map.Tables.FeatureKind[f] == FeatureDef.KindOre ? civ.FeatureRes[f] : -1;
                    if (res < 0)
                    {
                        int level = map.Tables.Level[map.Ground[tile]];
                        if (level != 6 && level != 7) return true;
                        Pick(u, i, civ.ResStone, ApplyYieldMultiplier(QuarryLoad, EquipmentYieldMultiplier(w, i, "pct.mineYield"))); // hill ground is not used up
                        WearEquipment(w, i, "pct.mineYield");
                        u.Action[i] = ActCarry;
                        return false;
                    }
                    int have = map.GetFeatureResource(tile);
                    int take = math.min(have, ApplyYieldMultiplier(QuarryLoad, EquipmentYieldMultiplier(w, i, "pct.mineYield")));
                    if (have - take > 0) map.SetFeatureResource(t.x, t.y, have - take);
                    else map.SetFeature(t.x, t.y, 0, ChangeSource.Unit);
                    Pick(u, i, res, math.max(1, take));
                    WearEquipment(w, i, "pct.mineYield");
                    u.Action[i] = ActCarry;
                    return false;
            }
        }

        // --- gatherer: berries, fruit, mushrooms, herbs ---
        static bool Gatherer(UnitWorld w, CivWorld civ, int i, int c, float dt)
        {
            var u = w.Store;
            var map = w.Map;
            switch (u.Action[i])
            {
                case ActPick:
                    CollectTargets(w, civ, i, c);
                    if (!FindFeature(w, civ, u.Pos[i], c, 255, -2, out int2 food)) return ReturnToCity(w, civ, i, c, dt);
                    u.TargetTile[i] = food;
                    u.Timer[i] = 0;
                    u.Action[i] = ActWalk;
                    return false;
                case ActWalk:
                    bool arrived = Walk(w, i, dt, out bool failed);
                    if (failed) return true;
                    if (arrived) { u.Action[i] = ActWork; u.Timer[i] = (short)-WorkTicks(civ, c, GatherTicks); }
                    return false;
                default:
                    if (++u.Timer[i] < 0) return false;
                    int2 t = u.TargetTile[i];
                    int tile = map.Index(t.x, t.y);
                    ushort f = map.Feature[tile];
                    int res = f != 0 ? civ.FeatureFood[f] : -1;
                    if (res < 0) return true;
                    // the whole bush (up to a load): walks are long next to a 3 s month (DECISIONS #53)
                    int have = map.GetFeatureResource(tile);
                    int take = math.min(have, GatherLoad);
                    if (have - take > 0) map.SetFeatureResource(t.x, t.y, have - take);
                    else if (map.Tables.FeatureKind[f] != FeatureDef.KindTree) map.SetFeature(t.x, t.y, 0, ChangeSource.Unit);
                    else map.SetFeatureResource(t.x, t.y, 0);
                    Pick(u, i, res, math.max(1, take));
                    u.Action[i] = ActCarry;
                    return false;
            }
        }

        static bool FindHill(UnitWorld w, CivWorld civ, float2 from, int c, out int2 found)
        {
            var map = w.Map;
            int2 center = (int2)math.floor(from);
            var cityCenter = civ.City(c).Center;
            for (int r = 0; r <= ResourceRadius; r++)
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (math.max(math.abs(dx), math.abs(dy)) != r) continue;
                        int x = center.x + dx, y = center.y + dy;
                        if (!map.InBounds(x, y)) continue;
                        int ti = map.Index(x, y);
                        int level = map.Tables.Level[map.Ground[ti]];
                        if (level != 6 && level != 7 || (map.Flags[ti] & (ushort)TileFlags.Walkable) == 0 || Taken(ti)) continue;
                        if (math.distancesq(new float2(x, y), cityCenter) > 40f * 40f) continue;
                        found = new int2(x, y);
                        return true;
                    }
            found = default;
            return false;
        }

        // Tiles co-workers of the same job are already heading to or working on, so they spread out instead of
        // racing for one field (derived from saved unit state, so loaded games decide the same way).
        static readonly int[] _taken = new int[256];
        static int _takenCount;

        static void CollectTargets(UnitWorld w, CivWorld civ, int self, int c)
        {
            var u = w.Store;
            _takenCount = 0;
            byte job = u.Job[self];
            foreach (int o in civ.CityResidents[c])
            {
                if (o == self || u.Job[o] != job || (UnitTask)u.Task[o] != UnitTask.Work || u.Action[o] == ActPick || u.Action[o] == ActCarry) continue;
                if (_takenCount == _taken.Length) break;
                int2 t = u.TargetTile[o];
                if (w.Map.InBounds(t.x, t.y)) _taken[_takenCount++] = w.Map.Index(t.x, t.y);
            }
        }

        static bool Taken(int tile)
        {
            for (int k = 0; k < _takenCount; k++) if (_taken[k] == tile) return true;
            return false;
        }

        // Nearest feature on (or near) city land: kind 255 = any food feature (resource -2), else kind + first-yield resource.
        static bool FindFeature(UnitWorld w, CivWorld civ, float2 from, int c, byte kind, int res, out int2 found)
        {
            var map = w.Map;
            int2 center = (int2)math.floor(from);
            var cityCenter = civ.City(c).Center;
            for (int r = 0; r <= ResourceRadius; r++)
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (math.max(math.abs(dx), math.abs(dy)) != r) continue;
                        int x = center.x + dx, y = center.y + dy;
                        if (!map.InBounds(x, y)) continue;
                        int ti = map.Index(x, y);
                        ushort f = map.Feature[ti];
                        if (f == 0 || map.GetFeatureResource(ti) <= 0 || Taken(ti)) continue;
                        if (kind == 255)
                        {
                            if (civ.FeatureFood[f] < 0 || f == civ.FeatWheat) continue;
                        }
                        else if (kind == FeatureDef.KindOre)
                        {
                            if (map.Tables.FeatureKind[f] != kind || civ.FeatureRes[f] < 0 || f == civ.BonesFeature || (res >= 0 && civ.FeatureRes[f] != res)) continue;
                        }
                        else if (map.Tables.FeatureKind[f] != kind || civ.FeatureRes[f] != res || map.GetFeatureStage(ti) < 2) continue;
                        if ((map.Flags[ti] & (ushort)TileFlags.Walkable) == 0) continue;
                        if (math.distancesq(new float2(x, y), cityCenter) > 40f * 40f) continue;
                        found = new int2(x, y);
                        return true;
                    }
            found = default;
            return false;
        }

        // --- hunter: chase an animal, meat to the store, leather straight in ---
        static bool Hunter(UnitWorld w, CivWorld civ, int i, int c, float dt, long tick, ref SimRandom rng)
        {
            var u = w.Store;
            if (u.Action[i] == ActPick)
            {
                int prey = FindPrey(w, i);
                if (prey < 0) return ReturnToCity(w, civ, i, c, dt);
                u.Target[i] = u.IdOf(prey);
                u.Timer[i] = 0;
                u.Action[i] = ActWalk;
                return false;
            }
            var target = u.Target[i];
            if (!u.IsAlive(target))
            {
                if (u.Action[i] == ActWork)
                {
                    Pick(u, i, civ.ResMeat, 3 + 3 * (int)math.round(u.Stat(i, StatId.Size)));
                    civ.AddRes(c, civ.ResLeather, 1);
                    u.Action[i] = ActCarry;
                    return false;
                }
                return true;
            }
            if (++u.Timer[i] > UnitActSystem.ChaseGiveUpTicks * 2) return true;
            int t = target.Index;
            float dist = math.distance(u.Pos[i], u.Pos[t]);
            float range = math.max(1f, u.Stat(i, StatId.Range));
            if (dist <= range + 0.5f)
            {
                w.ClearPath(i);
                if (u.AttackCooldown[i] > 0) return false;
                u.AttackCooldown[i] = (short)math.max(2, (int)(20f / u.Stat(i, StatId.AtkSpd)));
                w.MeleeHit(i, t, ref rng, tick);
                if (u.State[t] != UnitStore.StateAlive) u.Action[i] = ActWork; // killed: butcher
                return false;
            }
            if (dist < 6f) { w.ClearPath(i); w.StepToward(i, u.Pos[t], dt); return false; }
            int2 goal = u.Tile(t);
            if (u.PathHandle[i] >= 0 && math.distance(u.TargetTile[i], goal) > 3f) w.ClearPath(i);
            u.TargetTile[i] = goal;
            w.MoveTo(i, goal, dt, out bool failed);
            return failed && u.PathFails[i] >= 3;
        }

        static int FindPrey(UnitWorld w, int self)
        {
            var u = w.Store;
            var idx = w.Index;
            float radius = math.max(12f, u.Stat(self, StatId.Sight) * 3f);
            idx.CellRange(u.Pos[self], radius, out int x0, out int y0, out int x1, out int y1);
            int best = -1;
            float bestD = radius * radius;
            float size = u.Stat(self, StatId.Size);
            var predator = w.Content.Species[u.Species[self]];
            for (int cy = y0; cy <= y1; cy++)
                for (int cx = x0; cx <= x1; cx++)
                {
                    int cell = cy * idx.CellsX + cx;
                    for (int k = idx.CellStart[cell]; k < idx.CellStart[cell + 1]; k++)
                    {
                        int o = idx.Sorted[k];
                        if (u.State[o] != UnitStore.StateAlive || u.City[o] >= 0) continue;
                        var sp = u.SpeciesOf(o);
                        if (sp.IsCiv || sp.IsMonster || w.IsUndead(o) || u.Has(o, UnitFlags.Fly) || sp.Habitat == Habitat.Water) continue;
                        if (!predator.CanEatSpecies(u.Species[o])) continue;
                        if (u.Stat(o, StatId.Size) > size + 1f) continue;
                        if (w.Power(o) > w.Power(self) * HuntPowerShare) continue; // hunters leave lions and packs alone (DECISIONS #76)
                        float d = math.distancesq(u.Pos[self], u.Pos[o]);
                        if (d >= bestD) continue;
                        bestD = d;
                        best = o;
                    }
                }
            return best;
        }

        public const float HuntPowerShare = 0.6f;

        // --- workshop jobs: walk to the building, work, produce into the store ---
        static bool AtBuilding(UnitWorld w, CivWorld civ, int i, int c, int def, int ticks, float dt, long tick, ref SimRandom rng)
        {
            var u = w.Store;
            switch (u.Action[i])
            {
                case ActPick:
                    int b = def >= 0 ? civ.NearestBuilding(c, def, u.Pos[i]) : -1;
                    if (b < 0) return ReturnToCity(w, civ, i, c, dt);
                    u.TargetTile[i] = civ.Buildings[b].Door;
                    u.Timer[i] = 0;
                    u.Action[i] = ActWalk;
                    return false;
                case ActWalk:
                    bool arrived = Walk(w, i, dt, out bool failed);
                    if (failed) return true;
                    if (arrived) { u.Action[i] = ActWork; u.Timer[i] = (short)-WorkTicks(civ, c, ticks); }
                    return false;
                default:
                    if (++u.Timer[i] < 0) return false;
                    Produce(w, civ, i, c, def, tick, ref rng);
                    return true;
            }
        }

        static void Produce(UnitWorld w, CivWorld civ, int i, int c, int def, long tick, ref SimRandom rng)
        {
            var u = w.Store;
            var map = w.Map;
            if (def == civ.BldMine)
            {
                // nearest vein within 12 tiles of the mine, else plain stone
                int2 at = u.Tile(i);
                for (int r = 1; r <= 12; r++)
                    for (int dy = -r; dy <= r; dy++)
                        for (int dx = -r; dx <= r; dx++)
                        {
                            if (math.max(math.abs(dx), math.abs(dy)) != r) continue;
                            int x = at.x + dx, y = at.y + dy;
                            if (!map.InBounds(x, y)) continue;
                            int ti = map.Index(x, y);
                            ushort f = map.Feature[ti];
                            if (f == 0 || map.Tables.FeatureKind[f] != FeatureDef.KindOre || civ.FeatureRes[f] < 0 || f == civ.BonesFeature) continue;
                            int left = map.GetFeatureResource(ti) - 1;
                            if (left > 0) map.SetFeatureResource(x, y, left);
                            else map.SetFeature(x, y, 0, ChangeSource.Unit);
                            civ.AddRes(c, civ.FeatureRes[f], ApplyYieldMultiplier(2, EquipmentYieldMultiplier(w, i, "pct.mineYield")));
                            WearEquipment(w, i, "pct.mineYield");
                            return;
                        }
                civ.AddRes(c, civ.ResStone, ApplyYieldMultiplier(3, EquipmentYieldMultiplier(w, i, "pct.mineYield")));
                WearEquipment(w, i, "pct.mineYield");
            }
            else if (def == civ.BldFishingHut) HarvestFish(w, civ, i, c);
            else if (def == civ.BldPasture)
            {
                civ.AddRes(c, civ.ResMeat, 1);
                civ.AddRes(c, civ.ResWool, 1);
                civ.AddRes(c, civ.ResMilk, 2);
            }
            else if (def == civ.BldBakery)
            {
                if (civ.TakeRes(c, civ.ResWheat, 2)) civ.AddRes(c, civ.ResBread, 2);
            }
            else if (def == civ.BldSmithy) Craft.Make(w, civ, i, c, tick, ref rng);
        }

        static void HarvestFish(UnitWorld w, CivWorld civ, int i, int city)
        {
            if (civ.ResFish < 0) return;
            var map = w.Map;
            int2 origin = w.Store.Tile(i);
            int radius = 12, best = -1;
            float bestDistance = radius * radius;
            for (int y = origin.y - radius; y <= origin.y + radius; y++)
                for (int x = origin.x - radius; x <= origin.x + radius; x++)
                {
                    if (!map.InBounds(x, y)) continue;
                    int tile = map.Index(x, y);
                    ushort feature = map.Feature[tile];
                    if (feature == 0 || civ.FeatureRes[feature] != civ.ResFish || map.Tables.FeatureAquatic[feature] == 0 || map.GetFeatureResource(tile) <= 0) continue;
                    float distance = math.distancesq(new int2(x, y), origin);
                    if (distance >= bestDistance) continue;
                    bestDistance = distance;
                    best = tile;
                }
            if (best < 0) return; // a depleted local coast cannot supply an infinite fish stock
            int2 at = new int2(best % map.Width, best / map.Width);
            int left = map.GetFeatureResource(best) - 1;
            if (left > 0) map.SetFeatureResource(at.x, at.y, left);
            else map.SetFeature(at.x, at.y, 0, ChangeSource.Unit);
            civ.AddRes(city, civ.ResFish, 3);
        }

        // --- warriors and guards walk the city land ---
        static bool Patrol(UnitWorld w, CivWorld civ, int i, int c, float dt, ref SimRandom rng)
        {
            var u = w.Store;
            switch (u.Action[i])
            {
                case ActPick:
                    var zones = civ.CityZones[c];
                    if (zones.Count == 0) return true;
                    int guardPost = u.Job[i] - 1 == civ.JobGuard ? civ.NearestBuilding(c, civ.BldWatchtower, u.Pos[i]) : -1;
                    int2 dest;
                    if (guardPost >= 0) dest = CityPlanner.Below(civ.Buildings[guardPost].Door) + new int2(rng.Range(-3, 4), rng.Range(-3, 0));
                    else
                    {
                        int z = zones[rng.Range(0, zones.Count)];
                        dest = new int2(((z % w.Map.ZonesX) << WorldMap.ZoneShift) + rng.Range(0, WorldMap.ZoneSize),
                                        ((z / w.Map.ZonesX) << WorldMap.ZoneShift) + rng.Range(0, WorldMap.ZoneSize));
                    }
                    if (!w.Paths.CanStand(dest.x, dest.y, PathService.MobilityOf(u, i))) return true;
                    u.TargetTile[i] = dest;
                    u.Timer[i] = 0;
                    u.Action[i] = ActWalk;
                    return false;
                case ActWalk:
                    bool arrived = Walk(w, i, dt, out bool failed);
                    if (failed) return true;
                    if (arrived) { u.Action[i] = ActWork; u.Timer[i] = -PatrolWait; }
                    return false;
                default:
                    return ++u.Timer[i] >= 0;
            }
        }
    }
}
