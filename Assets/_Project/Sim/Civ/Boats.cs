using PG.Content;
using PG.Core;
using PG.World;
using Unity.Collections;
using Unity.Mathematics;

namespace PG.Sim
{
    // Bölüm 5.10. Transport and trade ships use the same persisted hull record and water path service.
    public enum BoatType : byte { Fishing, Trade, Transport, War }
    public enum BoatState : byte { Sailing, Done }

    public struct BoatData
    {
        public BoatType Type;
        public float2 Pos;
        public int City;
        public float Hp;
        public FixedList64Bytes<EntityId> Passengers; // 7 at most
        public ushort CargoRes;
        public int CargoAmount;
        public int2 Destination;    // water tile next to the landing
        public int2 Landing;        // land tile the colonists walk to
        public int PathHandle, PathStep;
        public BoatState State;
    }

    public readonly struct ColonyShipEvent : ISimEvent { public readonly int City; public ColonyShipEvent(int c) { City = c; } }

    public sealed class BoatSystem : ISimSystem
    {
        public const float Speed = 0.15f;          // tiles per tick
        public const int MaxPassengers = 7;
        public const float ColonizeChance = 0.5f;

        public SimPhase Phase => SimPhase.Civ;
        public int Order => 40;

        public void Tick(in SimContext ctx)
        {
            var civ = ctx.Civ;
            var w = ctx.Units;
            var u = w.Store;
            for (int b = 0; b < civ.Boats.Length; b++)
            {
                ref var boat = ref civ.Boats.ElementAt(b);
                if (boat.State != BoatState.Sailing) continue;
                if (!civ.TradeEnabled && boat.Type == BoatType.Trade)
                {
                    if (boat.PathHandle >= 0) w.Paths.Release(boat.PathHandle);
                    boat.PathHandle = -1;
                    if (civ.CityAlive(boat.City) && boat.CargoAmount > 0) civ.AddRes(boat.City, boat.CargoRes, boat.CargoAmount);
                    boat.CargoAmount = 0;
                    boat.State = BoatState.Done;
                    continue;
                }
                if (boat.PathHandle < 0)
                {
                    var status = w.Paths.Request((int2)boat.Pos, boat.Destination, Mobility.Water, out int h);
                    if (status == PathStatus.Ready) { boat.PathHandle = h; boat.PathStep = 0; }
                    else if (status != PathStatus.OverBudget) Unload(civ, w, ref boat, ctx.Clock.Tick); // no route: everyone swims for it
                    continue;
                }
                int len = w.Paths.Length(boat.PathHandle);
                if (boat.PathStep >= len) { Unload(civ, w, ref boat, ctx.Clock.Tick); continue; }
                float2 next = (float2)w.Paths.Point(boat.PathHandle, boat.PathStep) + 0.5f;
                float2 d = next - boat.Pos;
                float dist = math.length(d);
                if (dist <= Speed) { boat.Pos = next; boat.PathStep++; }
                else boat.Pos += d / dist * Speed;
                for (int p = 0; p < boat.Passengers.Length; p++)
                {
                    var id = boat.Passengers[p];
                    if (u.IsAlive(id)) u.Pos[id.Index] = boat.Pos + new float2((p % 3 - 1) * 0.3f, (p / 3 - 1) * 0.3f);
                }
            }
        }

        static void Unload(CivWorld civ, UnitWorld w, ref BoatData boat, long tick)
        {
            if (boat.Type == BoatType.Trade)
            {
                FinishTradeBoat(civ, w, ref boat, boat.PathHandle >= 0, tick);
                return;
            }
            var u = w.Store;
            foreach (var id in boat.Passengers)
            {
                if (!u.IsAlive(id)) continue;
                int i = id.Index;
                u.Flags[i] &= ~(uint)UnitFlags.Aboard;
                u.Pos[i] = (float2)boat.Landing + 0.5f;
                u.Dest[i] = boat.Landing;
                u.NextThink[i] = 0;
            }
            if (boat.PathHandle >= 0) w.Paths.Release(boat.PathHandle);
            boat.PathHandle = -1;
            boat.Passengers.Clear();
            boat.State = BoatState.Done; // the transport stays with the colony (not simulated further)
        }

        public static int TradeBoatDeliverable(int amount, int storageCapacity, float resourceValue, int buyerStock, float distance, int buyerGold, bool tradeAgreement = false)
        {
            int deliverable = math.min(math.max(0, amount), math.max(0, storageCapacity));
            while (deliverable > 0)
            {
                int payment = KingdomSystem.TradePayment(resourceValue, deliverable, buyerStock, tradeAgreement);
                int routeCost = tradeAgreement ? KingdomSystem.TradeAgreementRouteCost(distance, deliverable)
                    : KingdomSystem.TradeRouteCost(distance, deliverable);
                if (payment <= buyerGold && payment > routeCost) return deliverable;
                deliverable--;
            }
            return 0;
        }

        public static bool TryStartTradeRoute(in SimContext ctx, CivWorld civ, MetaWorld meta, int sourceCity)
        {
            if (!civ.TradeEnabled) return false;
            if (!civ.CityAlive(sourceCity) || civ.BldDocks < 0 || civ.BldMarket < 0) return false;
            int sourceKingdom = civ.KingdomOf(sourceCity);
            if (sourceKingdom < 0 || !meta.KingdomAlive(sourceKingdom)
                || civ.CountBuildings(sourceCity, civ.BldMarket, true) == 0) return false;
            int sourceDock = civ.NearestBuilding(sourceCity, civ.BldDocks, (float2)civ.City(sourceCity).Center);
            if (sourceDock < 0 || !civ.Buildings[sourceDock].Complete
                || !NearestWater(ctx.World, civ.Buildings[sourceDock].Door, 5, out int2 sourcePort)) return false;

            int sourceLand = ctx.Regions.IslandAt(CityPlanner.Below(civ.City(sourceCity).Center).x,
                CityPlanner.Below(civ.City(sourceCity).Center).y, MoveClass.Land);
            int sourceWater = ctx.Regions.IslandAt(sourcePort.x, sourcePort.y, MoveClass.Water);
            if (sourceWater < 0) return false;

            int bestCity = -1, bestRes = -1, bestAmount = 0;
            float bestProfit = 0f;
            for (int targetCity = 0; targetCity < civ.CityCount; targetCity++)
            {
                if (targetCity == sourceCity || !civ.CityAlive(targetCity)
                    || civ.CountBuildings(targetCity, civ.BldMarket, true) == 0) continue;
                int targetKingdom = civ.KingdomOf(targetCity);
                if (targetKingdom < 0 || !meta.KingdomAlive(targetKingdom)) continue;
                bool friendly = targetKingdom == sourceKingdom || !meta.AtWar(sourceKingdom, targetKingdom)
                    && !meta.HasTreaty(TreatyKind.TradeEmbargo, sourceKingdom, targetKingdom, ctx.Clock.Tick)
                    && (meta.Opinion(sourceKingdom, targetKingdom) > KingdomSystem.TradeOpinion
                        || meta.Kingdom(sourceKingdom).Alliance >= 0 && meta.Kingdom(sourceKingdom).Alliance == meta.Kingdom(targetKingdom).Alliance
                        || meta.HasTreaty(TreatyKind.TradeAgreement, sourceKingdom, targetKingdom, ctx.Clock.Tick)
                        || meta.HasVassalage(sourceKingdom, targetKingdom, ctx.Clock.Tick));
                if (!friendly) continue;
                int2 targetCenter = CityPlanner.Below(civ.City(targetCity).Center);
                int targetLand = ctx.Regions.IslandAt(targetCenter.x, targetCenter.y, MoveClass.Land);
                if (targetLand < 0 || targetLand == sourceLand) continue;
                int targetDock = civ.NearestBuilding(targetCity, civ.BldDocks, (float2)civ.City(targetCity).Center);
                if (targetDock < 0 || !civ.Buildings[targetDock].Complete
                    || !NearestWater(ctx.World, civ.Buildings[targetDock].Door, 5, out int2 targetPort)
                    || ctx.Regions.IslandAt(targetPort.x, targetPort.y, MoveClass.Water) != sourceWater) continue;

                float distance = math.distance(sourcePort, targetPort);
                for (int res = 0; res < civ.ResCount; res++)
                {
                    int surplus = civ.Res(sourceCity, res) - KingdomSystem.TradeKeep;
                    int demand = KingdomSystem.TradeKeep - civ.Res(targetCity, res);
                    int offered = math.min(KingdomSystem.TradeMax, math.min(surplus, demand));
                    if (offered <= 0) continue;
                    int storage = TradeStorageFree(civ, targetCity, res);
                    int buyerStock = civ.Res(targetCity, res);
                    bool treaty = sourceKingdom != targetKingdom && meta.HasTreaty(TreatyKind.TradeAgreement, sourceKingdom, targetKingdom, ctx.Clock.Tick);
                    int deliverable = TradeBoatDeliverable(offered, storage, civ.Content.Resources[res].Value,
                        buyerStock, distance, civ.City(targetCity).Gold, treaty);
                    if (deliverable <= 0) continue;
                    float profit = (KingdomSystem.TradePayment(civ.Content.Resources[res].Value, deliverable, buyerStock, treaty)
                        - (treaty ? KingdomSystem.TradeAgreementRouteCost(distance, deliverable) : KingdomSystem.TradeRouteCost(distance, deliverable)))
                        * KingdomSystem.TradeDemandPriority(civ.Content.Resources[res].IsFood, civ.FoodTotal(targetCity));
                    if (profit <= bestProfit) continue;
                    bestProfit = profit;
                    bestCity = targetCity;
                    bestRes = res;
                    bestAmount = deliverable;
                }
            }
            if (bestCity < 0 || bestRes < 0 || bestAmount <= 0 || !civ.TakeRes(sourceCity, bestRes, bestAmount)) return false;
            int2 destination = default;
            int destDock = civ.NearestBuilding(bestCity, civ.BldDocks, (float2)civ.City(bestCity).Center);
            if (destDock < 0 || !NearestWater(ctx.World, civ.Buildings[destDock].Door, 5, out destination))
            {
                civ.AddRes(sourceCity, bestRes, bestAmount);
                return false;
            }
            civ.Boats.Add(new BoatData
            {
                Type = BoatType.Trade, Pos = (float2)sourcePort + 0.5f, City = sourceCity, Hp = 20f,
                CargoRes = (ushort)bestRes, CargoAmount = bestAmount, Destination = destination,
                Landing = CityPlanner.Below(civ.City(bestCity).Center), PathHandle = -1, State = BoatState.Sailing,
            });
            return true;
        }

        static int TradeStorageFree(CivWorld civ, int city, int res)
        {
            int stored = civ.StoredTotal(city);
            int free = math.max(0, civ.City(city).StorageCap - stored);
            if (!civ.Content.Resources[res].IsFood)
                free = math.min(free, math.max(0, (int)(civ.City(city).StorageCap * CivWorld.NonFoodShare) - (stored - civ.FoodTotal(city))));
            return free;
        }

        static void FinishTradeBoat(CivWorld civ, UnitWorld w, ref BoatData boat, bool reachedLanding, long tick)
        {
            if (boat.PathHandle >= 0) w.Paths.Release(boat.PathHandle);
            boat.PathHandle = -1;
            if (!reachedLanding)
            {
                if (civ.CityAlive(boat.City) && boat.CargoAmount > 0) civ.AddRes(boat.City, boat.CargoRes, boat.CargoAmount);
                boat.CargoAmount = 0;
                boat.State = BoatState.Done;
                return;
            }

            int landingCity = civ.CityAtTile(boat.Landing.x, boat.Landing.y);
            if (landingCity == boat.City)
            {
                if (civ.CityAlive(boat.City) && boat.CargoAmount > 0) civ.AddRes(boat.City, boat.CargoRes, boat.CargoAmount);
                boat.CargoAmount = 0;
                boat.State = BoatState.Done;
                return;
            }

            if (boat.CargoAmount > 0 && landingCity >= 0 && civ.CityAlive(landingCity) && civ.CityAlive(boat.City))
            {
                int sellerKingdom = civ.KingdomOf(boat.City), buyerKingdom = civ.KingdomOf(landingCity);
                bool safe = civ.Meta == null || sellerKingdom < 0 || buyerKingdom < 0 || !civ.Meta.AtWar(sellerKingdom, buyerKingdom)
                    && !civ.Meta.HasTreaty(TreatyKind.TradeEmbargo, sellerKingdom, buyerKingdom, tick);
                int res = boat.CargoRes;
                if (safe && res < civ.ResCount)
                {
                    float distance = math.max(1, boat.PathStep);
                    bool treaty = civ.Meta != null && sellerKingdom >= 0 && buyerKingdom >= 0
                        && civ.Meta.HasTreaty(TreatyKind.TradeAgreement, sellerKingdom, buyerKingdom, tick);
                    int deliverable = TradeBoatDeliverable(boat.CargoAmount, TradeStorageFree(civ, landingCity, res),
                        civ.Content.Resources[res].Value, civ.Res(landingCity, res), distance, civ.City(landingCity).Gold, treaty);
                    if (deliverable > 0)
                    {
                        int buyerStock = civ.Res(landingCity, res);
                        int payment = KingdomSystem.TradePayment(civ.Content.Resources[res].Value, deliverable, buyerStock, treaty);
                        int routeCost = treaty ? KingdomSystem.TradeAgreementRouteCost(distance, deliverable)
                            : KingdomSystem.TradeRouteCost(distance, deliverable);
                        int accepted = civ.AddRes(landingCity, res, deliverable);
                        if (accepted > 0)
                        {
                            w.Events?.Publish(new TradeDeliveryEvent(boat.City, landingCity, res, accepted, true));
                            civ.City(landingCity).Gold -= payment;
                            civ.City(boat.City).Gold += math.max(0, payment - routeCost);
                            civ.RecordGoldExpense(landingCity, payment);
                            civ.RecordGoldIncome(boat.City, payment);
                            civ.RecordGoldRouteFee(boat.City, routeCost);
                            boat.CargoAmount -= accepted;
                            if (civ.Meta != null && sellerKingdom >= 0 && buyerKingdom >= 0 && sellerKingdom != buyerKingdom)
                            {
                                civ.Meta.SetOpinion(sellerKingdom, buyerKingdom, civ.Meta.Opinion(sellerKingdom, buyerKingdom) + CivJobs.TradeOpinionGain);
                                civ.Meta.SetOpinion(buyerKingdom, sellerKingdom, civ.Meta.Opinion(buyerKingdom, sellerKingdom) + CivJobs.TradeOpinionGain);
                            }
                        }
                    }
                }
            }
            StartTradeBoatReturn(civ, ref boat);
        }

        static void StartTradeBoatReturn(CivWorld civ, ref BoatData boat)
        {
            if (!civ.CityAlive(boat.City)) { boat.State = BoatState.Done; return; }
            int dock = civ.NearestBuilding(boat.City, civ.BldDocks, (float2)civ.City(boat.City).Center);
            int2 origin = CityPlanner.Below(civ.City(boat.City).Center);
            int2 port = default;
            bool hasPort = dock >= 0 && NearestWater(civ.Map, civ.Buildings[dock].Door, 5, out port);
            if (!hasPort) hasPort = NearestWater(civ.Map, origin, 5, out port);
            if (!hasPort)
            {
                if (boat.CargoAmount > 0) civ.AddRes(boat.City, boat.CargoRes, boat.CargoAmount);
                boat.CargoAmount = 0;
                boat.State = BoatState.Done;
                return;
            }
            boat.Destination = port;
            boat.Landing = origin;
            boat.PathStep = 0;
            boat.PathHandle = -1;
            boat.State = BoatState.Sailing;
        }

        // Yearly (Bölüm 5.10): a crowded city without free land and with docks ships colonists to another island.
        public static void TryColonize(in SimContext ctx, CivWorld civ, int city)
        {
            ref var c = ref civ.City(city);
            if (c.Population <= c.Housing * 1.1f || civ.BldDocks < 0 || HasFreeNeighbour(civ, city)) return;
            int law = ctx.Nature.Laws.IndexOf("law.colonization");
            if (law >= 0 && !ctx.Nature.Laws.IsOn(law)) return;
            int dock = civ.NearestBuilding(city, civ.BldDocks, c.Center);
            if (dock < 0) return;
            var map = ctx.World;
            ref var rng = ref ctx.Rng.Get(RngStream.Civ);
            float chance = ColonizeChance;
            var meta = civ.Meta;
            if (meta != null && meta.KingdomAlive(c.Kingdom))
                chance *= 1f + ColonizePct(ctx.Content, meta.Kingdom(c.Kingdom).Trait) / 100f;
            if (!rng.Chance(chance)) return;
            if (!NearestWater(map, civ.Buildings[dock].Door, 5, out int2 port)) return;
            int waterIsland = ctx.Regions.IslandAt(port.x, port.y, MoveClass.Water);
            int2 below = CityPlanner.Below(c.Center);
            int homeIsland = ctx.Regions.IslandAt(below.x, below.y, MoveClass.Land);
            int2 landing = new int2(-1), sea = default;
            float best = float.MaxValue;
            for (int t = 0; t < 128; t++)
            {
                int x = rng.Range(1, map.Width - 1), y = rng.Range(1, map.Height - 1);
                if (!map.HasFlag(x, y, TileFlags.Buildable) || ctx.Regions.IslandAt(x, y, MoveClass.Land) == homeIsland) continue;
                if (!SettlementSystem.Claimable(map, map.ZoneIndexOf(x, y), -1)) continue;
                for (int n = 0; n < 4; n++)
                {
                    int wx = x + (n == 0 ? 1 : n == 1 ? -1 : 0), wy = y + (n == 2 ? 1 : n == 3 ? -1 : 0);
                    if (!map.IsWater(wx, wy) || ctx.Regions.IslandAt(wx, wy, MoveClass.Water) != waterIsland) continue;
                    float d = math.distancesq(new float2(x, y), port);
                    if (d < best) { best = d; landing = new int2(x, y); sea = new int2(wx, wy); }
                    break;
                }
            }
            if (landing.x < 0) return;

            var boat = new BoatData { Type = BoatType.Transport, Pos = (float2)port + 0.5f, City = city, Hp = 20f, Destination = sea, Landing = landing, PathHandle = -1, State = BoatState.Sailing };
            var u = ctx.Units.Store;
            var residents = civ.CityResidents[city];
            for (int pass = 0; pass < 2 && boat.Passengers.Length < MaxPassengers; pass++)
                for (int k = residents.Count - 1; k >= 0 && boat.Passengers.Length < MaxPassengers; k--)
                {
                    int i = residents[k];
                    if ((AgeStage)u.Age[i] != AgeStage.Adult || u.Army[i] >= 0 || c.Leader.Index == i || u.Has(i, UnitFlags.Aboard)) continue;
                    if (pass == 0 && u.HomeBuilding[i] >= 0) continue; // homeless first
                    boat.Passengers.Add(u.IdOf(i));
                    u.City[i] = -1;
                    u.Job[i] = 0;
                    u.HomeBuilding[i] = -1;
                    u.CarryAmount[i] = 0;
                    u.Task[i] = (byte)UnitTask.None;
                    u.Home[i] = c.Center; // the colony joins the kingdom (Settlement)
                    u.Dest[i] = new int2(-1, -1);
                    u.Flags[i] |= (uint)UnitFlags.Aboard;
                    u.Pos[i] = boat.Pos;
                    ctx.Units.ClearPath(i);
                }
            if (boat.Passengers.Length == 0) return;
            civ.Boats.Add(boat);
            ctx.Events.Publish(new ColonyShipEvent(city));
        }

        static float ColonizePct(ContentDB db, int trait) => trait >= 0 && trait < db.KingdomTraits.Count ? db.KingdomTraits[trait].Pct("colonizeChance") : 0f; // yearly, not per tick

        static bool HasFreeNeighbour(CivWorld civ, int city)
        {
            var map = civ.Map;
            foreach (int z in civ.CityZones[city])
            {
                int zx = z % map.ZonesX, zy = z / map.ZonesX;
                for (int n = 0; n < 4; n++)
                {
                    int x = zx + (n == 0 ? 1 : n == 1 ? -1 : 0), y = zy + (n == 2 ? 1 : n == 3 ? -1 : 0);
                    if (x < 0 || y < 0 || x >= map.ZonesX || y >= map.ZonesY) continue;
                    int nz = y * map.ZonesX + x;
                    if (map.Zones[nz].OwnerCity < 0 && SettlementSystem.Claimable(map, nz, city)) return true;
                }
            }
            return false;
        }

        static bool NearestWater(WorldMap map, int2 c, int radius, out int2 found)
        {
            for (int r = 1; r <= radius; r++)
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (math.max(math.abs(dx), math.abs(dy)) != r) continue;
                        int x = c.x + dx, y = c.y + dy;
                        if (map.InBounds(x, y) && map.IsWater(x, y)) { found = new int2(x, y); return true; }
                    }
            found = default;
            return false;
        }
    }
}
