using PG.Content;
using PG.Core;
using PG.World;
using Unity.Mathematics;

namespace PG.Sim
{
    // Bölüm 5.2-5.3: monthly founding/joining, yearly zone growth and abandonment.
    public sealed class SettlementSystem : ISimSystem
    {
        public const float GroupRadius = 20f, JoinRadius = 30f, SiteRadius = 15f;
        public const int GroupSize = 6;
        public const float FullFactor = 1.2f;
        public const int AbandonYears = 2;

        public SimPhase Phase => SimPhase.Civ;
        public int Order => 0;

        readonly int[] _group = new int[256];

        public void Tick(in SimContext ctx)
        {
            long tick = ctx.Clock.Tick;
            if (tick % SimConst.TicksPerMonth != 0) return;
            var civ = ctx.Civ;
            var w = ctx.Units;
            ref var rng = ref ctx.Rng.Get(RngStream.Civ);
            civ.RecountResidents(w.Store);
            Found(ctx, civ, w, tick, ref rng);
            if (tick % SimConst.TicksPerYear == 0)
                for (int c = 0; c < civ.CityCount; c++)
                    if (civ.CityAlive(c)) Yearly(ctx, civ, c, tick);
        }

        void Found(in SimContext ctx, CivWorld civ, UnitWorld w, long tick, ref SimRandom rng)
        {
            var u = w.Store;
            var alive = u.Alive;
            for (int k = 0; k < alive.Length; k++)
            {
                int i = alive[k];
                if (u.State[i] != UnitStore.StateAlive || u.City[i] >= 0 || (AgeStage)u.Age[i] < AgeStage.Adult) continue;
                if ((w.Abilities(i) & AbilityFlags.Sapient) == 0 || u.Has(i, UnitFlags.Aboard)) continue;

                if (u.Dest[i].x >= 0 && math.distance(u.Pos[i], u.Dest[i]) > 6f) continue; // still on the way
                int near = NearestOpenCity(civ, u.Species[i], u.Pos[i]);
                if (near >= 0)
                {
                    u.City[i] = near;
                    continue;
                }
                int n = CollectGroup(w, i);
                if (n < GroupSize) continue;
                FoundCity(ctx, civ, w, n, tick, ref rng);
            }
        }

        static int NearestOpenCity(CivWorld civ, int species, float2 pos)
        {
            int best = -1;
            float bestD = JoinRadius * JoinRadius;
            for (int c = 0; c < civ.CityCount; c++)
            {
                if (!civ.CityAlive(c)) continue;
                ref var city = ref civ.City(c);
                if (city.Species != species) continue;
                if (city.Population >= math.max(GroupSize, city.Housing * FullFactor)) continue;
                float d = math.distancesq(pos, city.Center);
                if (d < bestD) { bestD = d; best = c; }
            }
            return best;
        }

        int CollectGroup(UnitWorld w, int self)
        {
            var u = w.Store;
            var idx = w.Index;
            idx.CellRange(u.Pos[self], GroupRadius, out int x0, out int y0, out int x1, out int y1);
            int n = 0;
            int sub = u.Subspecies[self];
            for (int cy = y0; cy <= y1; cy++)
                for (int cx = x0; cx <= x1; cx++)
                {
                    int cell = cy * idx.CellsX + cx;
                    for (int k = idx.CellStart[cell]; k < idx.CellStart[cell + 1] && n < _group.Length; k++)
                    {
                        int o = idx.Sorted[k];
                        if (u.State[o] != UnitStore.StateAlive || u.City[o] >= 0 || u.Subspecies[o] != sub) continue;
                        if (math.distancesq(u.Pos[o], u.Pos[self]) > GroupRadius * GroupRadius) continue;
                        _group[n++] = o;
                    }
                }
            return n;
        }

        void FoundCity(in SimContext ctx, CivWorld civ, UnitWorld w, int n, long tick, ref SimRandom rng)
        {
            var u = w.Store;
            var map = ctx.World;
            float2 center = 0f;
            for (int k = 0; k < n; k++) center += u.Pos[_group[k]];
            center /= n;

            int bestZone = -1;
            float bestScore = float.MinValue;
            int zr = (int)math.ceil(SiteRadius / WorldMap.ZoneSize);
            int czx = (int)center.x >> WorldMap.ZoneShift, czy = (int)center.y >> WorldMap.ZoneShift;
            for (int zy = czy - zr; zy <= czy + zr; zy++)
                for (int zx = czx - zr; zx <= czx + zr; zx++)
                {
                    if (zx < 0 || zy < 0 || zx >= map.ZonesX || zy >= map.ZonesY) continue;
                    int z = zy * map.ZonesX + zx;
                    if (!Claimable(map, z, -1)) continue;
                    float2 zc = new float2((zx << WorldMap.ZoneShift) + 4, (zy << WorldMap.ZoneShift) + 4);
                    if (math.distance(zc, center) > SiteRadius + 4f) continue;
                    float s = CityPlanner.ZoneScore(civ, w, z, -1);
                    if (s > bestScore) { bestScore = s; bestZone = z; }
                }
            if (bestZone < 0) return;

            int founder = _group[0];
            int sp = u.Species[founder];
            int sub = u.Subspecies[founder];
            int style = civ.SpeciesStyle[sp];
            if (!ctx.Content.Species[sp].IsCiv) style = civ.StyleBeast;
            if (civ.SstHiveMind >= 0 && sub >= 0 && w.Subspecies.Traits(sub).Has(civ.SstHiveMind)) style = civ.StyleInsect;

            int bonfire = civ.BldBonfire;
            if (!FindCenter(civ, bonfire, bestZone, -1, out int2 origin)) return;
            int city = civ.CreateCity(new int2((int)center.x, (int)center.y), sp, sub, style, tick, u.Uid[founder]);
            civ.Cities.Header(city).Color = rng.NextUInt() | 0xFFu;
            ClaimInitialZones(civ, map, city, bestZone);
            int b = civ.PlaceBuilding(city, bonfire, origin, 1f, tick);
            civ.City(city).CenterBuilding = b;
            civ.City(city).Center = civ.Buildings[b].Door;

            ref var c = ref civ.City(city);
            c.StorageCap = CivWorld.BaseStorage;
            // starter stock (DECISIONS #52): enough for the first tents and a few months of food
            civ.AddRes(city, civ.ResWood, 20);
            civ.AddRes(city, civ.ResLeather, 8);
            var diet = ctx.Content.Species[sp].Diet;
            if (diet == Diet.Carn || diet == Diet.Blood) civ.AddRes(city, civ.ResMeat, 60); // meat eaters start with meat
            else if (civ.FoodByNutrition.Length > 0) civ.AddRes(city, civ.ResWheat >= 0 ? civ.ResWheat : civ.FoodByNutrition[0], 60);
            c.FoodProducedYear = 0;

            int leader = -1;
            for (int k = 0; k < n; k++)
            {
                int o = _group[k];
                u.City[o] = city;
                if (leader < 0 || u.Level[o] > u.Level[leader] || (u.Level[o] == u.Level[leader] && u.BirthTick[o] < u.BirthTick[leader])) leader = o;
            }
            c.Leader = u.IdOf(leader);
            // colonies stay in the founders' kingdom (Bölüm 6.4); Home still points at the city they left
            int fromCity = u.Home[founder].x >= 0 ? civ.CityAtTile(u.Home[founder].x, u.Home[founder].y) : -1;
            if (fromCity >= 0 && civ.CityAlive(fromCity) && civ.Meta != null && civ.Meta.KingdomAlive(civ.City(fromCity).Kingdom)) c.Kingdom = civ.City(fromCity).Kingdom;
            for (int k = 0; k < n; k++) u.Dest[_group[k]] = new int2(-1, -1);
            if (civ.JobLeader >= 0) u.Job[leader] = (byte)(civ.JobLeader + 1);
            ctx.Events.Publish(new CityFoundedEvent(city, c.Center));
        }

        public static bool Claimable(WorldMap map, int z, int city)
        {
            var zone = map.Zones[z];
            if (zone.OwnerCity >= 0 && zone.OwnerCity != city) return false;
            return zone.LandTiles >= WorldMap.ZoneSize * WorldMap.ZoneSize * 0.4f;
        }

        static void ClaimInitialZones(CivWorld civ, WorldMap map, int city, int centerZone)
        {
            civ.ClaimZone(city, centerZone);
            int zx = centerZone % map.ZonesX, zy = centerZone / map.ZonesX;
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int x = zx + dx, y = zy + dy;
                    if (x < 0 || y < 0 || x >= map.ZonesX || y >= map.ZonesY) continue;
                    int z = y * map.ZonesX + x;
                    if (Claimable(map, z, city) && map.Zones[z].OwnerCity < 0) civ.ClaimZone(city, z);
                }
        }

        // Center building: the free footprint nearest to the zone middle.
        public static bool FindCenter(CivWorld civ, int def, int zone, int city, out int2 origin)
        {
            var map = civ.Map;
            var d = civ.Content.Buildings[def];
            int zx = (zone % map.ZonesX) << WorldMap.ZoneShift, zy = (zone / map.ZonesX) << WorldMap.ZoneShift;
            int2 mid = new int2(zx + 4 - d.TileW / 2, zy + 4 - d.TileH / 2);
            for (int r = 0; r <= 8; r++)
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (math.max(math.abs(dx), math.abs(dy)) != r) continue;
                        origin = mid + new int2(dx, dy);
                        if (civ.FootprintFree(origin, d.TileW, d.TileH, city)) return true;
                    }
            origin = default;
            return false;
        }

        static void Yearly(in SimContext ctx, CivWorld civ, int city, long tick)
        {
            ref var c = ref civ.City(city);
            c.FoodProducedLastYear = c.FoodProducedYear;
            c.FoodProducedYear = 0;

            if (c.Population == 0)
            {
                if (c.AbandonedTick == 0) c.AbandonedTick = tick;
                else if (tick - c.AbandonedTick >= AbandonYears * SimConst.TicksPerYear) RuinCity(ctx, civ, city, tick);
                return;
            }
            c.AbandonedTick = 0;

            // Bölüm 5.3 zone growth: one zone a year while under the target
            int target = math.min(CivWorld.MaxZonesPerCity, CivWorld.InitialZones + c.Population / 3 + c.HallTier * 4);
            if (civ.CityZones[city].Count < target) GrowZone(civ, ctx.Units, city);
            SendSettlers(ctx, civ, city);
            BoatSystem.TryColonize(ctx, civ, city);
        }

        public const float BigCityColonyChance = 0.2f;
        public const int BigCityPop = 60, SettlerGroup = 8, SettlerMinPop = 30, SettlerMinDistance = 40, SettlerMaxDistance = 90;

        // Overcrowded cities (pop >= 1.3 x housing) send homeless adults to found a new city on open land of the same island;
        // a preview of Bölüm 5.10-5.11 so villages spread (DECISIONS #55).
        static void SendSettlers(in SimContext ctx, CivWorld civ, int city)
        {
            ref var c = ref civ.City(city);
            if (c.Population < SettlerMinPop) return;
            ref var rng = ref ctx.Rng.Get(RngStream.Civ);
            // big cities also send colonies now and then, housed or not (DECISIONS #68)
            bool crowded = c.Population >= c.Housing * 1.3f;
            bool bigCityColony = c.Population >= BigCityPop && rng.Chance(BigCityColonyChance);
            var u = ctx.Units.Store;
            int adults = 0, idleAdults = 0;
            foreach (int resident in civ.CityResidents[city])
            {
                if ((AgeStage)u.Age[resident] < AgeStage.Adult) continue;
                adults++;
                if (u.Job[resident] == 0) idleAdults++;
            }
            int foodGoal = math.min(c.Population * 12, c.StorageCap / 2);
            bool surplusLaborAndFood = adults > 0 && idleAdults * 3 >= adults && civ.FoodTotal(city) >= foodGoal * 3 / 4 && rng.Chance(0.25f);
            if (!crowded && !bigCityColony && !surplusLaborAndFood) return;
            var map = ctx.World;
            int island = ctx.Regions.IslandAt(c.Center.x, c.Center.y - 1, MoveClass.Land);
            int2 dest = new int2(-1);
            float best = float.MinValue;
            for (int t = 0; t < 48; t++)
            {
                float angle = rng.Range(0f, 6.2831853f);
                float dist = rng.Range(SettlerMinDistance, SettlerMaxDistance);
                int2 p = c.Center + (int2)(new float2(math.cos(angle), math.sin(angle)) * dist);
                if (!map.InBounds(p.x, p.y) || !map.HasFlag(p.x, p.y, TileFlags.Buildable) || ctx.Regions.IslandAt(p.x, p.y, MoveClass.Land) != island) continue;
                int z = map.ZoneIndexOf(p.x, p.y);
                if (!Claimable(map, z, -1)) continue;
                float s = CityPlanner.ZoneScore(civ, ctx.Units, z, -1);
                if (s > best) { best = s; dest = p; }
            }
            if (dest.x < 0 || best < 2f) return;
            int sent = 0;
            var leader = c.Leader;
            for (int pass = 0; pass < (crowded ? 1 : 2); pass++)
            foreach (int i in civ.CityResidents[city])
            {
                if (sent >= SettlerGroup) break;
                if (u.City[i] != city || pass == 0 && u.HomeBuilding[i] >= 0 || u.Army[i] >= 0 || (AgeStage)u.Age[i] != AgeStage.Adult || (u.IsAlive(leader) && leader.Index == i)) continue;
                u.HomeBuilding[i] = -1;
                u.City[i] = -1;
                u.Job[i] = 0;
                u.CarryAmount[i] = 0;
                u.Home[i] = c.Center; // origin: the colony joins its kingdom (Bölüm 6.4)
                u.Dest[i] = dest + new int2(rng.Range(-3, 4), rng.Range(-3, 4));
                u.Task[i] = (byte)UnitTask.None;
                u.NextThink[i] = 0;
                sent++;
            }
        }

        // Claims the best unowned neighbour zone (terrain score - 0.3 x distance to the center); false when none is left.
        public static bool GrowZone(CivWorld civ, UnitWorld units, int city)
        {
            ref var c = ref civ.City(city);
            var zones = civ.CityZones[city];
            if (zones.Count >= CivWorld.MaxZonesPerCity) return false;
            var map = civ.Map;
            int czx = (int)c.Center.x >> WorldMap.ZoneShift, czy = (int)c.Center.y >> WorldMap.ZoneShift;
            int best = -1;
            float bestScore = float.MinValue;
            for (int k = 0; k < zones.Count; k++)
            {
                int z = zones[k];
                int zx = z % map.ZonesX, zy = z / map.ZonesX;
                for (int n = 0; n < 4; n++)
                {
                    int x = zx + (n == 0 ? 1 : n == 1 ? -1 : 0), y = zy + (n == 2 ? 1 : n == 3 ? -1 : 0);
                    if (x < 0 || y < 0 || x >= map.ZonesX || y >= map.ZonesY) continue;
                    int nz = y * map.ZonesX + x;
                    if (map.Zones[nz].OwnerCity >= 0 || !Claimable(map, nz, city)) continue;
                    float s = CityPlanner.ZoneScore(civ, units, nz, city) - 0.3f * math.distance(new float2(x, y), new float2(czx, czy));
                    if (s > bestScore || (s == bestScore && nz < best)) { bestScore = s; best = nz; }
                }
            }
            if (best < 0) return false;
            civ.ClaimZone(city, best);
            return true;
        }

        public static void RuinCity(in SimContext ctx, CivWorld civ, int city, long tick)
        {
            var list = civ.CityBuildings[city];
            for (int k = list.Count - 1; k >= 0; k--) civ.Ruin(list[k], tick);
            civ.ReleaseZones(city);
            civ.Cities.MarkDead(city, tick);
            var u = ctx.Units.Store;
            for (int k = 0; k < u.Alive.Length; k++)
            {
                int i = u.Alive[k];
                if (u.City[i] != city) continue;
                u.City[i] = -1;
                u.Job[i] = 0;
                u.HomeBuilding[i] = -1;
            }
            ctx.Events.Publish(new CityRuinedEvent(city));
        }
    }
}
