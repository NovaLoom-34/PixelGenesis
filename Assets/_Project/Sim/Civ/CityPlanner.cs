using PG.Content;
using PG.Core;
using PG.World;
using Unity.Mathematics;

namespace PG.Sim
{
    // Bölüm 5.4 planner: every 120 ticks per city pick the most needed building, find a site, pay and lay the foundation.
    public sealed class CityPlannerSystem : ISimSystem
    {
        public const int PlanInterval = 120, MaxActiveBuilds = 3, SiteSamples = 64, MaxPlanWaits = 8;

        public SimPhase Phase => SimPhase.Civ;
        public int Order => 10;

        readonly int[] _defs = new int[CityPlanner.MaxCandidates];
        readonly float[] _scores = new float[CityPlanner.MaxCandidates];

        public void Tick(in SimContext ctx)
        {
            var civ = ctx.Civ;
            long tick = ctx.Clock.Tick;
            ref var rng = ref ctx.Rng.Get(RngStream.Civ);
            for (int c = 0; c < civ.CityCount; c++)
            {
                if (!civ.CityAlive(c)) continue;
                ref var city = ref civ.City(c);
                if (city.NextPlanTick > tick) continue;
                city.NextPlanTick = tick + PlanInterval;
                if (city.Population == 0 || civ.ActiveConstructions(c) >= MaxActiveBuilds) continue;
                // highest need first. Without space the next need is tried; without resources the city saves up for
                // MaxPlanWaits cycles, then builds what it can meanwhile (DECISIONS #51).
                // The kingdom policy (governor or player, Bölüm 10) reweights the needs (DECISIONS #98).
                var policy = ctx.Meta != null && city.Kingdom >= 0 ? ctx.Meta.PolicyOf(city.Kingdom) : KingdomPolicy.Balanced;
                int n = CityPlanner.Candidates(civ, ctx.Units, c, _defs, _scores, policy);
                bool placed = false, crowded = false;
                for (int k = 0; k < n; k++)
                {
                    int def = _defs[k];
                    if (!civ.CanAfford(c, civ.Content.Buildings[def]))
                    {
                        if (city.PlanWaits < MaxPlanWaits) { city.PlanWaits++; break; }
                        continue;
                    }
                    if (!CityPlanner.FindSite(civ, c, def, ref rng, out int2 origin)) { crowded = true; continue; }
                    civ.Pay(c, civ.Content.Buildings[def]);
                    if (civ.IsHall(def) && city.CenterBuilding >= 0) { civ.Remove(city.CenterBuilding); city.CenterBuilding = -1; }
                    civ.PlaceBuilding(c, def, origin, 0f, tick);
                    city.PlanWaits = 0;
                    placed = true;
                    break;
                }
                // no room for the most needed building: the city claims land for it (DECISIONS #51, beyond the yearly growth)
                if (crowded && !placed) SettlementSystem.GrowZone(civ, ctx.Units, c);
            }
        }
    }

    public static class CityPlanner
    {
        public const float BuildRate = 0.01f; // progress per builder tick for a 2x2-cell building

        // --- Bölüm 5.2 terrain score (also used for zone growth) ---
        public static float ZoneScore(CivWorld civ, UnitWorld w, int zone, int city)
        {
            var map = civ.Map;
            int x0 = (zone % map.ZonesX) << WorldMap.ZoneShift, y0 = (zone / map.ZonesX) << WorldMap.ZoneShift;
            int buildable = 0, food = 0, trees = 0;
            bool hills = false, coast = false, danger = false;
            for (int y = y0; y < y0 + WorldMap.ZoneSize; y++)
                for (int x = x0; x < x0 + WorldMap.ZoneSize; x++)
                {
                    int i = map.Index(x, y);
                    ushort f = map.Flags[i];
                    ushort feat = map.Feature[i];
                    if ((f & (ushort)TileFlags.Buildable) != 0 && (f & (ushort)TileFlags.River) == 0 && map.Building[i] < 0)
                    {
                        buildable++;
                        if (feat == 0) food++;
                    }
                    if (feat != 0)
                    {
                        byte kind = map.Tables.FeatureKind[feat];
                        if (kind == FeatureDef.KindTree) trees++;
                        if (w.FeatureNutrition[feat] > 0f) food++;
                    }
                    int level = map.Tables.Level[map.Ground[i]];
                    if (level == 6 || level == 7) hills = true;
                    if ((f & ((ushort)TileFlags.Water | (ushort)TileFlags.River)) != 0) coast = true;
                    if ((f & (ushort)TileFlags.Burning) != 0 || map.Tables.Active[map.Ground[i]] != 0) danger = true;
                }
            float s = 3f * buildable / 64f + 2f * food / 64f + trees / 24f + (hills ? 1f : 0f) + (coast ? 1f : 0f) - (danger ? 3f : 0f);
            if (NearOtherCity(map, zone, city)) s -= 5f;
            return s;
        }

        static bool NearOtherCity(WorldMap map, int zone, int city)
        {
            int zx = zone % map.ZonesX, zy = zone / map.ZonesX;
            for (int dy = -2; dy <= 2; dy++)
                for (int dx = -2; dx <= 2; dx++)
                {
                    int x = zx + dx, y = zy + dy;
                    if (x < 0 || y < 0 || x >= map.ZonesX || y >= map.ZonesY) continue;
                    int owner = map.Zones[y * map.ZonesX + x].OwnerCity;
                    if (owner >= 0 && owner != city) return true;
                }
            return false;
        }

        // --- needs (Bölüm 5.4 table) ---
        public const int MaxCandidates = 24;

        // Needed buildings sorted by score (descending); returns the count.
        // `policy` reweights needs: score * (0.5 + share); balanced shares (0.2) keep the base order.
        public static int Candidates(CivWorld civ, UnitWorld w, int c, int[] defs, float[] scores, KingdomPolicy policy = default)
        {
            if (policy.Food <= 0f && policy.Housing <= 0f && policy.Production <= 0f && policy.Defense <= 0f && policy.Culture <= 0f)
                policy = KingdomPolicy.Balanced;
            float wFood = 0.5f + policy.Food, wHousing = 0.5f + policy.Housing;
            float wProd = 0.5f + policy.Production, wDef = 0.5f + policy.Defense, wCul = 0.5f + policy.Culture;
            ref var city = ref civ.City(c);
            var db = civ.Content;
            int pop = city.Population;
            int n = 0;

            void Consider(int def, float score)
            {
                if (def < 0 || score <= 0f || n >= defs.Length || !Allowed(civ, w, c, def)) return;
                // one unfinished foundation per work/service type; only housing may be laid in parallel (DECISIONS #206)
                if (db.Buildings[def].CategoryCode != BuildingCategory.House && UnderConstruction(civ, c, def)) return;
                for (int k = 0; k < n; k++) if (defs[k] == def) return;
                int at = n++;
                while (at > 0 && scores[at - 1] < score) { defs[at] = defs[at - 1]; scores[at] = scores[at - 1]; at--; }
                defs[at] = def;
                scores[at] = score;
            }

            // center upgrade (also housing capacity, DECISIONS #51)
            if (city.HallTier + 1 < civ.Halls.Length && !UnderConstruction(civ, c, civ.Halls[city.HallTier + 1]))
                Consider(civ.Halls[city.HallTier + 1], 100f * wHousing);

            // housing
            // Keep a modest reserve so construction begins before the next birth wave
            // turns into homelessness. Small villages still get at least four spare beds.
            int deficit = pop - city.Housing + math.max(4, pop / 10);
            if (deficit > 0) Consider(BestHouse(civ, w, c), (80f + math.min(deficit, 15)) * wHousing);

            // storage
            if (city.StorageCap > 0 && civ.StoredTotal(c) > city.StorageCap * 0.8f)
            {
                Consider(civ.BldStorage, 70f * wProd);
                if (civ.FoodTotal(c) > civ.StoredTotal(c) / 2) Consider(civ.BldGranary, 65f * wFood);
            }

            // food
            if (pop * SimConst.MonthsPerYear > city.FoodProducedLastYear || civ.FoodTotal(c) < pop * 6)
            {
                Consider(civ.BldFarmShed, 75f * wFood);
                Consider(civ.BldFishingHut, 74f * wFood);
                Consider(civ.BldPasture, 45f * wFood);
                Consider(civ.BldBeehive, 40f * wFood);
            }

            // production
            if (civ.Res(c, civ.ResWood) < 100) Consider(civ.BldLumberCamp, 60f * wProd);
            Consider(civ.BldMine, pop >= 12 ? 55f * wProd : 0f);
            bool craftMaterial = civ.Res(c, civ.ResIron) >= Craft.MaterialCost || civ.Res(c, civ.ResCopper) >= Craft.MaterialCost ||
                                 civ.Res(c, civ.ResWood) >= Craft.MaterialCost || civ.Res(c, civ.ResBone) >= Craft.MaterialCost;
            if (craftMaterial) Consider(civ.BldSmithy, 52f * wProd);
            if (civ.Res(c, civ.ResWheat) > 20) Consider(db.Buildings.IdOrDefault("bld.windmill"), 40f * wProd);
            Consider(civ.BldBakery, civ.Res(c, civ.ResWheat) > 20 ? 40f * wProd : 0f);

            // welfare, culture, defense
            if (pop > 40)
            {
                Consider(civ.BldMarket, 35f * wCul);
                Consider(civ.BldInn, 30f * wCul);
                Consider(civ.BldWell, 30f * wCul);
            }
            if (pop > 15) Consider(civ.BldTemple, 33f * wCul);
            if (pop > 30) Consider(civ.BldLibrary, 28f * wCul);
            if (pop > 30) Consider(civ.BldHealers, 26f * wCul);
            Consider(civ.BldGraveyard, 25f * wCul);
            Consider(civ.BldPyre, 20f * wCul);
            if (pop > 30) Consider(civ.BldWatchtower, 20f * wDef);
            if (pop > 60) Consider(civ.BldBarracks, 15f * wDef);
            return n;
        }

        static bool UnderConstruction(CivWorld civ, int c, int def)
        {
            foreach (int b in civ.CityBuildings[c])
            {
                var d = civ.Buildings[b];
                if (d.Def == def && d.BuildProgress < 1f && (d.Flags & (BuildingFlags.Ruin | BuildingFlags.Dead)) == 0) return true;
            }
            return false;
        }

        static int BestHouse(CivWorld civ, UnitWorld w, int c)
        {
            int best = -1;
            float cap = 0f;
            for (int b = 0; b < civ.HouseCapacity.Length; b++)
            {
                if (civ.HouseCapacity[b] <= cap || !Allowed(civ, w, c, b)) continue;
                best = b;
                cap = civ.HouseCapacity[b];
            }
            // cheaper tier when the best one is unaffordable, so growth never stalls on one resource
            if (best >= 0 && !civ.CanAfford(c, civ.Content.Buildings[best]))
                for (int b = 0; b < civ.HouseCapacity.Length; b++)
                    if (civ.HouseCapacity[b] > 0f && Allowed(civ, w, c, b) && civ.CanAfford(c, civ.Content.Buildings[b])) return b;
            return best;
        }

        public static bool Allowed(CivWorld civ, UnitWorld w, int c, int def)
        {
            var d = civ.Content.Buildings[def];
            if (d.MaxPerCity <= 0 || civ.CountBuildings(c, def, false) >= d.MaxPerCity) return false;
            foreach (var r in d.Reqs) if (!Met(civ, w, c, r)) return false;
            // beasts build nests, hive minds hives, the rest the regular houses
            bool beast = civ.City(c).Style == civ.StyleBeast, hive = civ.City(c).Style == civ.StyleInsect;
            if (d.CategoryCode == BuildingCategory.House)
            {
                bool nest = def == civ.BldNest, hiveDef = def == civ.BldHive;
                if (hive) return hiveDef;
                if (beast) return nest;
                return !nest && !hiveDef;
            }
            return true;
        }

        static bool Met(CivWorld civ, UnitWorld w, int c, BuildingRequirement r)
        {
            ref var city = ref civ.City(c);
            switch (r.Kind)
            {
                case BuildingReq.Pop: return city.Population >= r.Value;
                case BuildingReq.Building: return civ.CountBuildings(c, r.Value, true) > 0;
                case BuildingReq.Herbs: return civ.Res(c, civ.ResHerbs) > 0;
                case BuildingReq.BeastCiv: return city.Style == civ.StyleBeast;
                case BuildingReq.HiveMind: return city.Style == civ.StyleInsect;
                case BuildingReq.Coast: return TerrainNear(civ, c, TerrainKind.Coast);
                case BuildingReq.Hills: return TerrainNear(civ, c, TerrainKind.Hills);
                case BuildingReq.Forest: return TerrainNear(civ, c, TerrainKind.Forest);
                case BuildingReq.Flowers: return TerrainNear(civ, c, TerrainKind.Flowers);
                case BuildingReq.Livestock: return LivestockNear(civ, w, c);
                case BuildingReq.Language: return city.Language >= 0;
                // "din": a people able to hold a faith; the first temple founds one (Bölüm 6.9)
                case BuildingReq.Religion: return city.Religion >= 0 || w != null && civ.CityResidents[c].Count > 0 && (w.Abilities(civ.CityResidents[c][0]) & AbilityFlags.CanHoldReligion) != 0;
                case BuildingReq.Capital: return civ.Meta != null && civ.Meta.KingdomAlive(city.Kingdom) && civ.Meta.Kingdom(city.Kingdom).Capital == c;
                case BuildingReq.WarVictory: return civ.Meta != null && WonWar(civ.Meta, city.Kingdom);
                default: return false;
            }
        }

        static bool WonWar(MetaWorld meta, int k)
        {
            for (int w = 0; k >= 0 && w < meta.Wars.Count; w++)
            {
                var d = meta.Wars.Items[w];
                if (d.Result == WarResult.AttackerWon && Has(d.AttackerSide, k) || d.Result == WarResult.DefenderWon && Has(d.DefenderSide, k) || d.CapturedCities > 0 && d.Attacker == k) return true;
            }
            return false;
        }

                static bool Has(in Unity.Collections.FixedList64Bytes<int> l, int k)
        {
            for (int i = 0; i < l.Length; i++) if (l[i] == k) return true;
            return false;
        }

        enum TerrainKind { Coast, Hills, Forest, Flowers }

        static bool TerrainNear(CivWorld civ, int c, TerrainKind kind)
        {
            var map = civ.Map;
            int trees = 0;
            foreach (int z in civ.CityZones[c])
            {
                int x0 = (z % map.ZonesX) << WorldMap.ZoneShift, y0 = (z / map.ZonesX) << WorldMap.ZoneShift;
                for (int y = y0; y < y0 + WorldMap.ZoneSize; y++)
                    for (int x = x0; x < x0 + WorldMap.ZoneSize; x++)
                    {
                        int i = map.Index(x, y);
                        switch (kind)
                        {
                            case TerrainKind.Coast:
                                if ((map.Flags[i] & (ushort)TileFlags.Water) != 0) return true;
                                break;
                            case TerrainKind.Hills:
                                int level = map.Tables.Level[map.Ground[i]];
                                if (level == 6 || level == 7) return true;
                                break;
                            case TerrainKind.Forest:
                                if (map.Feature[i] != 0 && map.Tables.FeatureKind[map.Feature[i]] == FeatureDef.KindTree && ++trees >= 5) return true;
                                break;
                            case TerrainKind.Flowers:
                                if (map.Feature[i] != 0 && civ.FeatureRes[map.Feature[i]] == civ.ResHerbs) return true;
                                break;
                        }
                    }
            }
            return false;
        }

        // Tame-able herbivores grazing on city land.
        static bool LivestockNear(CivWorld civ, UnitWorld w, int c)
        {
            var u = w.Store;
            int n = 0;
            for (int k = 0; k < u.Alive.Length; k++)
            {
                int i = u.Alive[k];
                var sp = u.SpeciesOf(i);
                if (sp.Diet != Diet.Herb || sp.IsCiv || sp.Habitat != Habitat.Land) continue;
                int2 t = u.Tile(i);
                if (civ.CityAtTile(t.x, t.y) == c && ++n >= 2) return true;
            }
            return false;
        }

        // --- site finding (Bölüm 5.4) ---
        public static bool FindSite(CivWorld civ, int c, int def, ref SimRandom rng, out int2 best)
        {
            var map = civ.Map;
            var d = civ.Content.Buildings[def];
            var zones = civ.CityZones[c];
            best = default;
            if (zones.Count == 0) return false;
            if (civ.IsHall(def)) return HallSite(civ, c, d, out best);
            float bestScore = float.MinValue;
            float2 center = civ.City(c).Center;
            bool outskirts = def == civ.BldFarmShed || def == civ.BldMine || def == civ.BldLumberCamp || def == civ.BldPasture;
            bool needsWater = def == civ.BldFishingHut || def == civ.BldDocks || d.CategoryCode == BuildingCategory.Naval;
            bool needsHills = def == civ.BldMine;
            for (int s = 0; s < CityPlannerSystem.SiteSamples; s++)
            {
                int z = zones[rng.Range(0, zones.Count)];
                var o = new int2(((z % map.ZonesX) << WorldMap.ZoneShift) + rng.Range(0, WorldMap.ZoneSize) - d.TileW / 2,
                                 ((z / map.ZonesX) << WorldMap.ZoneShift) + rng.Range(0, WorldMap.ZoneSize) - d.TileH / 2);
                if (!civ.FootprintFree(o, d.TileW, d.TileH, c, clearOre: def == civ.BldMine)) continue;
                if (needsWater && !Around(map, o, d.TileW, d.TileH, 1, TileFlags.Water)) continue;
                if (needsHills && !HillsAround(map, o, d.TileW, d.TileH, 3)) continue;
                float dist = math.distance(center, new float2(o.x + d.TileW * 0.5f, o.y));
                float score = outskirts ? dist * 0.3f : -dist * 0.5f;
                if (Around(map, o, d.TileW, d.TileH, 1, TileFlags.Road)) score += 2f;
                if (Flat(map, o, d.TileW, d.TileH)) score += 2f;
                if (score > bestScore) { bestScore = score; best = o; }
            }
            // Random samples are a fast first pass, but a crowded city can have only a few
            // valid footprints left. Exhaustively scan owned zones before reporting `nosite`.
            // This fallback is reached only when the cheap sample found nothing.
            if (bestScore == float.MinValue)
            {
                foreach (int z in zones)
                {
                    int x0 = (z % map.ZonesX) << WorldMap.ZoneShift;
                    int y0 = (z / map.ZonesX) << WorldMap.ZoneShift;
                    for (int y = y0; y < y0 + WorldMap.ZoneSize; y++)
                        for (int x = x0; x < x0 + WorldMap.ZoneSize; x++)
                        {
                            var o = new int2(x, y);
                            if (!civ.FootprintFree(o, d.TileW, d.TileH, c, clearOre: def == civ.BldMine)) continue;
                            if (needsWater && !Around(map, o, d.TileW, d.TileH, 1, TileFlags.Water)) continue;
                            if (needsHills && !HillsAround(map, o, d.TileW, d.TileH, 3)) continue;
                            float dist = math.distance(center, new float2(o.x + d.TileW * 0.5f, o.y));
                            float score = outskirts ? dist * 0.3f : -dist * 0.5f;
                            if (Around(map, o, d.TileW, d.TileH, 1, TileFlags.Road)) score += 2f;
                            if (Flat(map, o, d.TileW, d.TileH)) score += 2f;
                            if (score > bestScore) { bestScore = score; best = o; }
                        }
                }
            }
            return bestScore > float.MinValue;
        }

        // Halls grow in place: centered on the old center's door, the old center may be overlapped (it is removed at the start).
        static bool HallSite(CivWorld civ, int c, BuildingDef d, out int2 best)
        {
            ref var city = ref civ.City(c);
            int old = city.CenterBuilding;
            int2 door = city.Center;
            int2 baseOrigin = new int2(door.x - d.TileW / 2, door.y);
            // Higher hall tiers have larger footprints; search the whole nearby owned area
            // rather than stopping at a tiny radius that can be blocked by the old village.
            for (int r = 0; r <= 12; r++)
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (math.max(math.abs(dx), math.abs(dy)) != r) continue;
                        best = baseOrigin + new int2(dx, dy);
                        if (civ.FootprintFree(best, d.TileW, d.TileH, c, old, false, clearOre: true, ringBuildingsOk: true)) return true;
                    }
            best = default;
            return false;
        }

        static bool Around(WorldMap map, int2 o, int w, int h, int r, TileFlags flag)
        {
            for (int y = o.y - r; y < o.y + h + r; y++)
                for (int x = o.x - r; x < o.x + w + r; x++)
                {
                    if (x >= o.x && x < o.x + w && y >= o.y && y < o.y + h) continue;
                    if (map.InBounds(x, y) && map.HasFlag(x, y, flag)) return true;
                }
            return false;
        }

        static bool HillsAround(WorldMap map, int2 o, int w, int h, int r)
        {
            for (int y = o.y - r; y < o.y + h + r; y++)
                for (int x = o.x - r; x < o.x + w + r; x++)
                {
                    if (!map.InBounds(x, y)) continue;
                    int level = map.GetLevel(x, y);
                    if (level == 6 || level == 7) return true;
                }
            return false;
        }

        static bool Flat(WorldMap map, int2 o, int w, int h)
        {
            int level = map.GetLevel(o.x, o.y);
            for (int y = o.y; y < o.y + h; y++)
                for (int x = o.x; x < o.x + w; x++)
                    if (map.GetLevel(x, y) != level) return false;
            return true;
        }

        // --- construction (Bölüm 5.4) ---

        // One builder tick; true when the building got finished by this call.
        public static bool Build(CivWorld civ, UnitWorld w, int b, long tick)
        {
            var d = civ.Buildings[b];
            if (d.BuildProgress >= 1f || !d.Alive || (d.Flags & BuildingFlags.Ruin) != 0) return false;
            float cells = math.max(1f, d.W * d.H / 36f); // 2x2 cells = 1
            d.BuildProgress = math.min(1f, d.BuildProgress + BuildRate / cells);
            d.Hp = math.max(d.Hp, d.MaxHp * d.BuildProgress);
            civ.Buildings[b] = d;
            if (d.BuildProgress < 1f) return false;
            Complete(civ, w, b, tick);
            return true;
        }

        public static void Complete(CivWorld civ, UnitWorld w, int b, long tick)
        {
            var d = civ.Buildings[b];
            d.Hp = d.MaxHp;
            civ.Buildings[b] = d;
            int c = d.City;
            if (c >= 0 && civ.CityAlive(c))
            {
                ref var city = ref civ.City(c);
                for (int t = 1; t < civ.Halls.Length; t++)
                {
                    if (civ.Halls[t] != d.Def) continue;
                    int old = city.CenterBuilding;
                    city.CenterBuilding = b;
                    city.HallTier = (byte)t;
                    city.Center = d.Door;
                    if (old >= 0 && old != b) civ.Remove(old); // the new hall replaces the old center (DECISIONS #51)
                }
                if (city.CenterBuilding >= 0 && city.CenterBuilding != b)
                    civ.BuildRoad(Below(d.Door), Below(civ.City(c).Center));
            }
            civ.Version++;
            w.Events?.Publish(new BuildingCompletedEvent(b, d.Def));
        }

        public static int2 Below(int2 door) => new int2(door.x, door.y - 1);
    }
}
