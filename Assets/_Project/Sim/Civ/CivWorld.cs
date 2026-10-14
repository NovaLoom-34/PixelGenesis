using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using PG.Content;
using PG.Core;
using PG.World;
using Unity.Collections;
using Unity.Mathematics;

namespace PG.Sim
{
    // Bölüm 5.1 (kingdom, loyalty: Bölüm 6). Zone/building/resident lists are derived caches in CivWorld, not saved.
    public struct CityData
    {
        public int Kingdom;             // -1 until Bölüm 6
        public int2 Center;             // door tile of the center building
        public int CenterBuilding;
        public byte HallTier;           // 0 bonfire, 1-3 halls
        public EntityId Leader;
        public int Loyalty, Gold;
        public int LoyaltyMod;          // powers (rebellion spark), fades 10 a month
        public int Culture, Language, Religion; // dominant among residents (monthly, Bölüm 6.7-6.9)
        public short Happiness;
        public int FoundedYear;
        public ushort Style;
        public ushort Species;
        public int Subspecies;
        public long NextPlanTick;
        public long AbandonedTick;      // 0 = inhabited
        public byte FamineMonths;
        public byte PlanWaits;          // plan cycles the top need has waited for resources
        public int FoodProducedYear, FoodProducedLastYear; // planner food need
        public int Population, Housing, StorageCap;       // monthly caches
    }

    // Diagnostic food ledger. This cache is derived and intentionally excluded from saves.
    public sealed class CityFoodFlow
    {
        public int AddedToStock, EatenFromStock, EatenFromCarry, Spoiled, Overflow;
    }

    // Derived per-city counters used by economy diagnostics; not part of saved simulation state.
    public sealed class CityGoldFlow
    {
        public long Income, Expenses, TransfersIn, TransfersOut, RouteFees;
        public long MaintenanceDue, MaintenancePaid, MaintenanceUnpaid;
    }

    [Flags]
    public enum BuildingFlags : byte { None = 0, Burning = 1, Ruin = 2, Abandoned = 4, Upgrading = 8, Dead = 16 }

    public struct BuildingData
    {
        public ushort Def;
        public int2 Origin;         // bottom-left tile
        public byte W, H;           // tiles
        public int City;
        public float Hp, MaxHp;
        public float BuildProgress; // 0-1
        public byte Tier;
        public ushort Style;
        public int Residents;
        public BuildingFlags Flags;
        public long RuinTick;
        public int Workers;          // monthly cache (job holders)

        public int2 Door => new int2(Origin.x + W / 2, Origin.y);
        public bool Complete => BuildProgress >= 1f && (Flags & (BuildingFlags.Ruin | BuildingFlags.Dead)) == 0;
        public bool Alive => (Flags & BuildingFlags.Dead) == 0;
    }

    public struct ItemData
    {
        public long Uid;
        public ushort Type, Material, Quality;
        public int NameId;          // -1 until the name generator (Bölüm 6.2)
        public int Kills;
        public long CreatedTick;
        public long MakerUid;
        public int Owner;           // unit index, -1 = in city stock or dropped
        public int City;            // stock city, -1 = none
        public int DroppedTile;     // -1 = not on the ground
        public byte Dead;
    }

    public readonly struct CityFoundedEvent : ISimEvent { public readonly int City; public readonly int2 Center; public CityFoundedEvent(int city, int2 center) { City = city; Center = center; } }
    public readonly struct CityRuinedEvent : ISimEvent { public readonly int City; public CityRuinedEvent(int city) { City = city; } }
    public readonly struct BuildingCompletedEvent : ISimEvent { public readonly int Building; public readonly ushort Def; public BuildingCompletedEvent(int b, ushort def) { Building = b; Def = def; } }
    public readonly struct BuildingDestroyedEvent : ISimEvent { public readonly int Building; public readonly ushort Def; public readonly bool GroundLost; public BuildingDestroyedEvent(int b, ushort def, bool groundLost) { Building = b; Def = def; GroundLost = groundLost; } }
    [Flags] public enum FamineCause : byte { None = 0, Drought = 1, Cold = 2, Flood = 4 }
    public readonly struct FamineEvent : ISimEvent
    {
        public readonly int City;
        public readonly FamineCause Cause;
        public FamineEvent(int city, FamineCause cause = FamineCause.None) { City = city; Cause = cause; }
    }
    public readonly struct FamineMigrationEvent : ISimEvent
    {
        public readonly int FromCity, ToCity, Migrants;
        public FamineMigrationEvent(int fromCity, int toCity, int migrants)
        { FromCity = fromCity; ToCity = toCity; Migrants = migrants; }
    }
    public readonly struct DisasterMigrationEvent : ISimEvent
    {
        public readonly int FromCity, ToCity, Migrants;
        public DisasterMigrationEvent(int fromCity, int toCity, int migrants)
        { FromCity = fromCity; ToCity = toCity; Migrants = migrants; }
    }
    public readonly struct ItemCraftedEvent : ISimEvent { public readonly int Item; public readonly ushort Quality; public ItemCraftedEvent(int item, ushort quality) { Item = item; Quality = quality; } }
    public readonly struct ItemEquippedEvent : ISimEvent
    {
        public readonly long UnitUid, ItemUid;
        public readonly ushort Quality;
        public ItemEquippedEvent(long unitUid, long itemUid, ushort quality) { UnitUid = unitUid; ItemUid = itemUid; Quality = quality; }
    }
    public readonly struct TradeDeliveryEvent : ISimEvent
    {
        public readonly int SourceCity, DestinationCity, Resource, Amount;
        public readonly bool BySea;
        public TradeDeliveryEvent(int sourceCity, int destinationCity, int resource, int amount, bool bySea)
        { SourceCity = sourceCity; DestinationCity = destinationCity; Resource = resource; Amount = amount; BySea = bySea; }
    }
    public sealed class CivWorld : IDisposable
    {
        public const int MaxZonesPerCity = 120, InitialZones = 9;
        public const int BaseStorage = 100, StoragePerBuilding = 200, StoragePerTier = 100;
        public const int FloodCropMemoryMonths = 24;
        public const float NonFoodShare = 0.6f;
        const int ItemDurabilityMagic = 0x31525544; // DUR1
        const int FloodCropMemoryMagic = 0x31444C46; // FLD1
        public const byte MaxItemDurability = 100;

        public readonly MetaStore<CityData> Cities = new MetaStore<CityData>(MetaKind.City);
        public NativeList<BuildingData> Buildings;
        public NativeList<int> FreeBuildings;
        public NativeList<int> Inventory;   // Cities.Count * ResCount
        public NativeList<ItemData> Items;
        public NativeList<byte> ItemDurability;
        public NativeList<BoatData> Boats;  // Bölüm 5.10
        public NativeList<VehicleData> Vehicles; // Bölüm 5.12: carts, cars and advanced vessels
        public readonly Dictionary<int, HashSet<int>> KingdomInventions = new Dictionary<int, HashSet<int>>();
        public readonly Dictionary<int, int> CurrentResearch = new Dictionary<int, int>();
        public readonly Dictionary<int, int> ResearchProgress = new Dictionary<int, int>();
        public long NextItemUid = 1;
        public int Version; // bumped on any building/border change (render)
        public MetaWorld Meta; // set by SimWorld
        public int StatFoodIn, StatWoodIn, StatStoneIn, StatHarvested, StatLost; // running totals (soak/debug)
        public bool TradeEnabled = true; // editor soak control; not persisted, defaults on for every game

        // derived per-city caches (rebuilt by RebuildCaches / monthly)
        public readonly List<List<int>> CityZones = new List<List<int>>();
        public readonly List<List<int>> CityBuildings = new List<List<int>>();
        public readonly List<List<int>> CityResidents = new List<List<int>>();
        public readonly List<CityFoodFlow> CityFoodFlows = new List<CityFoodFlow>();
        public readonly List<CityGoldFlow> CityGoldFlows = new List<CityGoldFlow>();
        readonly List<long> _cityFloodCropLossUntil = new List<long>();

        public readonly ContentDB Content;
        public readonly WorldMap Map;
        public readonly int ResCount;

        // cached content ids
        public readonly int BldBonfire, BldRuins, BldStorage, BldGranary, BldFarmShed, BldMine, BldLumberCamp, BldFishingHut, BldPasture,
                            BldSmithy, BldBakery, BldWatchtower, BldBarracks, BldMarket, BldInn, BldWell, BldGraveyard, BldPyre, BldBeehive,
                            BldDocks, BldTent, BldNest, BldHive, BldHealers, BldTemple, BldLibrary;
        public readonly int[] Halls;        // hall_1..hall_3 building ids (index 1..3), [0] = bonfire
        public readonly int ResWood, ResStone, ResWheat, ResBread, ResMeat, ResFish, ResLeather, ResWool, ResMilk, ResGold, ResHerbs, ResSalt, ResIron, ResCopper, ResBone;
        public readonly int[] FoodByNutrition; // food resource ids, best first
        public readonly int JobBuilder, JobGatherer, JobFarmer, JobLumberjack, JobMiner, JobFisher, JobHunter, JobHerder, JobSmith, JobBaker,
                            JobTrader, JobWarrior, JobGuard, JobPriest, JobScholar, JobHealer, JobSailor, JobLeader;
        public readonly int HapNewHome, HapHungry, HapHomeless, HapAteWell, HapFamilyDied, HapChildBorn, HapEraChange, HapCityCaptured, HapWarWon, HapWarLost;
        public readonly ushort FeatWheat;
        public readonly byte TileField;
        public readonly int SstHiveMind;
        public readonly int StyleBeast, StyleInsect;
        public readonly int[] SpeciesStyle; // per species
        public readonly float[] HouseCapacity; // per building def (0 = not a house)
        public readonly int[] MaterialByRes;   // resource -> material (-1)
        public readonly int[] FeatureRes;      // feature map value -> first yield resource (-1)
        public readonly int[] FeatureFood;     // feature map value -> first food yield (-1)
        public readonly ushort BonesFeature;

        public CivWorld(WorldMap map, ContentDB content)
        {
            Map = map;
            Content = content;
            ResCount = content.Resources.Count;
            Buildings = new NativeList<BuildingData>(256, Allocator.Persistent);
            FreeBuildings = new NativeList<int>(64, Allocator.Persistent);
            Inventory = new NativeList<int>(64 * math.max(1, ResCount), Allocator.Persistent);
            Items = new NativeList<ItemData>(256, Allocator.Persistent);
            ItemDurability = new NativeList<byte>(256, Allocator.Persistent);
            Boats = new NativeList<BoatData>(8, Allocator.Persistent);
            Vehicles = new NativeList<VehicleData>(16, Allocator.Persistent);

            int B(string id) => content.Buildings.IdOrDefault(id);
            BldBonfire = B("bld.bonfire"); BldRuins = B("bld.ruins"); BldStorage = B("bld.storage"); BldGranary = B("bld.granary");
            BldFarmShed = B("bld.farm_shed"); BldMine = B("bld.mine"); BldLumberCamp = B("bld.lumber_camp"); BldFishingHut = B("bld.fishing_hut");
            BldPasture = B("bld.pasture"); BldSmithy = B("bld.smithy"); BldBakery = B("bld.bakery"); BldWatchtower = B("bld.watchtower");
            BldBarracks = B("bld.barracks"); BldMarket = B("bld.market"); BldInn = B("bld.inn"); BldWell = B("bld.well");
            BldGraveyard = B("bld.graveyard"); BldPyre = B("bld.pyre"); BldBeehive = B("bld.beehive"); BldDocks = B("bld.docks"); BldHealers = B("bld.healers_house"); BldTemple = B("bld.temple"); BldLibrary = B("bld.library");
            BldTent = B("bld.tent"); BldNest = B("bld.nest"); BldHive = B("bld.hive");
            Halls = new[] { BldBonfire, B("bld.hall_1"), B("bld.hall_2"), B("bld.hall_3") };

            int R(string id) => content.Resources.IdOrDefault(id);
            ResWood = R("res.wood"); ResStone = R("res.stone"); ResWheat = R("res.wheat"); ResBread = R("res.bread"); ResMeat = R("res.meat");
            ResFish = R("res.fish"); ResLeather = R("res.leather"); ResWool = R("res.wool"); ResMilk = R("res.milk"); ResGold = R("res.gold");
            ResHerbs = R("res.herbs"); ResSalt = R("res.salt"); ResIron = R("res.iron"); ResCopper = R("res.copper"); ResBone = R("res.bone");
            var foods = new List<int>();
            for (int r = 0; r < ResCount; r++) if (content.Resources[r].IsFood) foods.Add(r);
            foods.Sort((a, b) => content.Resources[b].Nutrition != content.Resources[a].Nutrition ? content.Resources[b].Nutrition.CompareTo(content.Resources[a].Nutrition) : a.CompareTo(b));
            FoodByNutrition = foods.ToArray();

            int J(string id) => content.Jobs.IdOrDefault(id);
            JobBuilder = J("job.builder"); JobGatherer = J("job.gatherer"); JobFarmer = J("job.farmer"); JobLumberjack = J("job.lumberjack");
            JobMiner = J("job.miner"); JobFisher = J("job.fisher"); JobHunter = J("job.hunter"); JobHerder = J("job.herder"); JobSmith = J("job.smith");
            JobBaker = J("job.baker"); JobTrader = J("job.trader"); JobWarrior = J("job.warrior"); JobGuard = J("job.guard"); JobPriest = J("job.priest");
            JobScholar = J("job.scholar"); JobHealer = J("job.healer"); JobSailor = J("job.sailor"); JobLeader = J("job.leader");

            int H(string id) => content.HappinessEvents.IdOrDefault(id);
            HapNewHome = H("hap.new_home"); HapHungry = H("hap.hungry"); HapHomeless = H("hap.homeless"); HapAteWell = H("hap.ate_well");
            HapFamilyDied = H("hap.family_died"); HapChildBorn = H("hap.child_born"); HapEraChange = H("hap.era_change"); HapCityCaptured = H("hap.city_captured"); HapWarWon = H("hap.war_won"); HapWarLost = H("hap.war_lost");

            FeatWheat = content.Features.TryGet("feat.wheat_crop", out var wheat) ? wheat.MapValue : (ushort)0;
            TileField = (byte)math.max(0, content.Tiles.IdOrDefault("tile.field"));
            SstHiveMind = content.SubspeciesTraits.IdOrDefault("sst.hive_mind");

            StyleBeast = content.BuildingStyles.IdOrDefault("style.beast");
            StyleInsect = content.BuildingStyles.IdOrDefault("style.insect");
            SpeciesStyle = new int[content.Species.Count];
            for (int s = 0; s < content.Species.Count; s++)
            {
                var sp = content.Species[s];
                int style = content.BuildingStyles.IdOrDefault("style." + sp.Id.Substring(sp.Id.IndexOf('.') + 1));
                SpeciesStyle[s] = style >= 0 ? style : math.max(0, StyleBeast);
            }

            // Housing (not in buildings.json): cells x 0.5 per hall tier the house needs, +0.5 (tent 2, hut 4, house 9, manor 18) - DECISIONS #51
            HouseCapacity = new float[content.Buildings.Count];
            for (int b = 0; b < content.Buildings.Count; b++)
            {
                var def = content.Buildings[b];
                if (def.CategoryCode != BuildingCategory.House) continue;
                int tier = 0;
                foreach (var r in def.Reqs)
                    if (r.Kind == BuildingReq.Building)
                        for (int t = 1; t < Halls.Length; t++) if (Halls[t] == r.Value) tier = t;
                int cells = def.TileW * def.TileH / (BuildingDef.CellTiles * BuildingDef.CellTiles);
                HouseCapacity[b] = math.max(1f, cells * 0.5f * (tier + 1));
            }

            FeatureRes = new int[content.Features.Count + 1];
            FeatureFood = new int[content.Features.Count + 1];
            FeatureRes[0] = FeatureFood[0] = -1;
            for (int f = 0; f < content.Features.Count; f++)
            {
                var def = content.Features[f];
                int first = -1, food = -1;
                if (def.Yields != null)
                    foreach (var kv in def.Yields)
                    {
                        int r = content.Resources.IdOrDefault(kv.Key);
                        if (r < 0) continue;
                        if (first < 0) first = r;
                        if (food < 0 && content.Resources[r].IsFood) food = r;
                    }
                FeatureRes[def.MapValue] = first;
                FeatureFood[def.MapValue] = food;
            }
            BonesFeature = content.Features.TryGet("feat.bones_pile", out var bones) ? bones.MapValue : (ushort)0;

            MaterialByRes = new int[ResCount];
            for (int r = 0; r < ResCount; r++)
            {
                string rid = content.Resources[r].Id;
                MaterialByRes[r] = content.Materials.IdOrDefault("mat." + rid.Substring(rid.IndexOf('.') + 1));
            }
        }

        public int CityCount => Cities.Count;
        public int KingdomOf(int city) => city >= 0 && city < Cities.Count ? Cities.Items[city].Kingdom : -1;
        public bool IsHall(int def)
        {
            for (int t = 1; t < Halls.Length; t++) if (Halls[t] == def) return true;
            return false;
        }
        public bool CityAlive(int c) => Cities.IsAlive(c);
        public ref CityData City(int c) => ref Cities.Data(c);

        public void RecordGoldIncome(int city, long amount)
        {
            if (amount > 0 && city >= 0 && city < CityGoldFlows.Count) CityGoldFlows[city].Income += amount;
        }

        public void RecordGoldExpense(int city, long amount)
        {
            if (amount > 0 && city >= 0 && city < CityGoldFlows.Count) CityGoldFlows[city].Expenses += amount;
        }

        public void RecordGoldTransferIn(int city, long amount)
        {
            if (amount > 0 && city >= 0 && city < CityGoldFlows.Count) CityGoldFlows[city].TransfersIn += amount;
        }

        public void RecordGoldTransferOut(int city, long amount)
        {
            if (amount > 0 && city >= 0 && city < CityGoldFlows.Count) CityGoldFlows[city].TransfersOut += amount;
        }

        public void RecordGoldRouteFee(int city, long amount)
        {
            if (amount > 0 && city >= 0 && city < CityGoldFlows.Count) CityGoldFlows[city].RouteFees += amount;
        }

        public void RecordGoldMaintenance(int city, long due, long paid)
        {
            if (city < 0 || city >= CityGoldFlows.Count || due <= 0) return;
            paid = Math.Max(0L, Math.Min(due, paid));
            var flow = CityGoldFlows[city];
            flow.MaintenanceDue += due;
            flow.MaintenancePaid += paid;
            flow.MaintenanceUnpaid += due - paid;
            flow.Expenses += paid;
        }

        // --- cities ---

        public int CreateCity(int2 center, int species, int subspecies, int style, long tick, long founderUid)
        {
            int c = Cities.Create(new CityData
            {
                Kingdom = -1,
                Culture = -1, Language = -1, Religion = -1,
                Center = center,
                CenterBuilding = -1,
                Leader = EntityId.None,
                FoundedYear = (int)(tick / SimConst.TicksPerYear),
                Style = (ushort)math.max(0, style),
                Species = (ushort)species,
                Subspecies = subspecies,
                NextPlanTick = tick + 30,
            }, founderUid, tick);
            Inventory.Resize((c + 1) * ResCount, NativeArrayOptions.ClearMemory);
            while (CityZones.Count <= c) { CityZones.Add(new List<int>()); CityBuildings.Add(new List<int>()); CityResidents.Add(new List<int>()); CityFoodFlows.Add(new CityFoodFlow()); CityGoldFlows.Add(new CityGoldFlow()); }
            while (_cityFloodCropLossUntil.Count <= c) _cityFloodCropLossUntil.Add(0);
            _cityFloodCropLossUntil[c] = 0;
            return c;
        }

        public void RecordFloodCropLoss(int x, int y, long restoreTick)
        {
            if (!Map.InBounds(x, y)) return;
            int city = CityAtTile(x, y);
            if (city < 0 || city >= Cities.Count || !CityAlive(city)) return;
            while (_cityFloodCropLossUntil.Count <= city) _cityFloodCropLossUntil.Add(0);
            long memoryTicks = (long)FloodCropMemoryMonths * SimConst.TicksPerMonth;
            _cityFloodCropLossUntil[city] = Math.Max(_cityFloodCropLossUntil[city], restoreTick + memoryTicks);
        }

        // 0 means "never recorded"; without the > 0 guard, tick 0 would match every city.
        public bool HasRecentFloodCropLoss(int city, long tick) =>
            city >= 0 && city < _cityFloodCropLossUntil.Count && _cityFloodCropLossUntil[city] > 0 && _cityFloodCropLossUntil[city] >= tick;

        public int Res(int city, int res) => res < 0 ? 0 : Inventory[city * ResCount + res];

        public int StoredTotal(int city)
        {
            int sum = 0;
            for (int r = 0; r < ResCount; r++) sum += Inventory[city * ResCount + r];
            return sum;
        }

        // Adds up to the free storage; returns what fit (Bölüm 5.5: excess is not collected).
        public int AddRes(int city, int res, int amount)
        {
            if (res < 0 || amount <= 0) return 0;
            int cap = City(city).StorageCap;
            int free = math.max(0, cap - StoredTotal(city));
            // 40% of the store is kept for food so ore and wood cannot starve the city (DECISIONS #53)
            if (!Content.Resources[res].IsFood) free = math.min(free, math.max(0, (int)(cap * NonFoodShare) - (StoredTotal(city) - FoodTotal(city))));
            int add = math.min(free, amount);
            StatLost += amount - add;
            if (Content.Resources[res].IsFood)
            {
                CityFoodFlows[city].AddedToStock += add;
                CityFoodFlows[city].Overflow += amount - add;
            }
            Inventory[city * ResCount + res] += add;
            if (Content.Resources[res].IsFood) { City(city).FoodProducedYear += add; StatFoodIn += add; }
            else if (res == ResWood) StatWoodIn += add;
            else if (res == ResStone) StatStoneIn += add;
            return add;
        }

        public bool TakeRes(int city, int res, int amount)
        {
            if (res < 0) return false;
            int k = city * ResCount + res;
            if (Inventory[k] < amount) return false;
            Inventory[k] -= amount;
            return true;
        }

        public bool CanAfford(int city, BuildingDef def)
        {
            foreach (var c in def.Costs) if (Res(city, c.Res) < c.Amount) return false;
            return true;
        }

        public void Pay(int city, BuildingDef def)
        {
            foreach (var c in def.Costs) Inventory[city * ResCount + c.Res] -= c.Amount;
        }

        // One unit of the best food the diet allows; returns its nutrition, -1 when the store has none.
        public int EatFromStore(UnitStore u, int i)
        {
            int city = u.City[i];
            if (city < 0 || !CityAlive(city)) return -1;
            var diet = u.SpeciesOf(i).Diet;
            for (int pass = 0; pass < 2; pass++)
                foreach (int r in FoodByNutrition)
                {
                    if (pass == 0 && !Edible(diet, r) || !TakeRes(city, r, 1)) continue;
                    int nutrition = CityNutrition(diet, r);
                    u.Saturation[i] = (byte)math.min(100, u.Saturation[i] + nutrition * 10);
                    CityFoodFlows[city].EatenFromStock++;
                    return nutrition;
                }
            return -1;
        }

        // In a city meat eaters take their own food first, then cooked crops at half nutrition (DECISIONS #59).
        int CityNutrition(Diet diet, int res) => math.max(1, Edible(diet, res) ? Content.Resources[res].Nutrition : Content.Resources[res].Nutrition / 2);

        // Workers hauling food eat from the load first (DECISIONS #53).
        public int EatFromCarry(UnitStore u, int i)
        {
            if (u.CarryAmount[i] == 0) return -1;
            int r = u.CarryRes[i];
            if (r >= ResCount || !Content.Resources[r].IsFood) return -1;
            u.CarryAmount[i]--;
            int city = u.City[i];
            if (city >= 0 && city < CityFoodFlows.Count) CityFoodFlows[city].EatenFromCarry++;
            int nutrition = CityNutrition(u.SpeciesOf(i).Diet, r);
            u.Saturation[i] = (byte)math.min(100, u.Saturation[i] + nutrition * 10);
            return nutrition;
        }

        public bool Edible(Diet diet, int res)
        {
            bool meat = res == ResMeat || res == ResFish;
            switch (diet)
            {
                case Diet.Herb: return !meat;
                case Diet.Carn: case Diet.Blood: return meat;
                default: return true;
            }
        }

        public int FoodTotal(int city)
        {
            int sum = 0;
            foreach (int r in FoodByNutrition) sum += Inventory[city * ResCount + r];
            return sum;
        }

        public int CountBuildings(int city, int def, bool completeOnly)
        {
            int n = 0;
            foreach (int b in CityBuildings[city])
            {
                var d = Buildings[b];
                if (d.Def == def && (d.Flags & (BuildingFlags.Ruin | BuildingFlags.Dead)) == 0 && (!completeOnly || d.BuildProgress >= 1f)) n++;
            }
            return n;
        }

        public int ActiveConstructions(int city)
        {
            int n = 0;
            foreach (int b in CityBuildings[city])
            {
                var d = Buildings[b];
                if (d.BuildProgress < 1f && (d.Flags & (BuildingFlags.Ruin | BuildingFlags.Dead)) == 0) n++;
            }
            return n;
        }

        // Nearest completed building of `def` (-1 = any drop point: center, storage, granary and the work buildings
        // that gather raw goods - farm shed, lumber camp, mine, fishing hut; DECISIONS #53) to a position.
        public int NearestBuilding(int city, int def, float2 pos)
        {
            int best = -1;
            float bestD = float.MaxValue;
            foreach (int b in CityBuildings[city])
            {
                var d = Buildings[b];
                if (!d.Complete) continue;
                bool match = def >= 0 ? d.Def == def : b == City(city).CenterBuilding || d.Def == BldStorage || d.Def == BldGranary ||
                                                       d.Def == BldFarmShed || d.Def == BldLumberCamp || d.Def == BldMine || d.Def == BldFishingHut;
                if (!match) continue;
                float dist = math.distancesq(pos, d.Door);
                if (dist < bestD) { bestD = dist; best = b; }
            }
            return best;
        }

        public int CityAtTile(int x, int y) => Map.InBounds(x, y) ? Map.Zones[Map.ZoneIndexOf(x, y)].OwnerCity : -1;

        public void ClaimZone(int city, int zone)
        {
            Map.SetZoneOwner(zone, city);
            InsertSorted(CityZones[city], zone); // index order, the same as a rebuilt cache after loading
            Version++;
        }

        static void InsertSorted(List<int> list, int value)
        {
            int at = list.BinarySearch(value);
            if (at < 0) list.Insert(~at, value);
        }

        public void ReleaseZone(int city, int zone)
        {
            if (Map.Zones[zone].OwnerCity == city) Map.SetZoneOwner(zone, -1);
            CityZones[city].Remove(zone);
            Version++;
        }

        public void ReleaseZones(int city)
        {
            foreach (int z in CityZones[city]) if (Map.Zones[z].OwnerCity == city) Map.SetZoneOwner(z, -1);
            CityZones[city].Clear();
            Version++;
        }

        // --- buildings ---

        // A square plaza around the center stays free so hall upgrades (up to 5x5 cells) fit in place (DECISIONS #51).
        public const int PlazaRadius = 9;

        // `clearOre`: metal veins under the footprint do not block (Stamp clears them) - halls and mines.
        // `ringBuildingsOk`: neighbours in the one-tile street ring do not block; buildings inside the footprint still do - halls.
        public bool FootprintFree(int2 origin, int w, int h, int city, int ignore = -1, bool plaza = true, bool clearOre = false, bool ringBuildingsOk = false)
        {
            var map = Map;
            if (plaza && city >= 0 && CityAlive(city))
            {
                int2 c = City(city).Center;
                if (origin.x - 1 <= c.x + PlazaRadius && origin.x + w >= c.x - PlazaRadius && origin.y - 1 <= c.y + PlazaRadius * 2 && origin.y + h >= c.y - 2)
                    return false;
            }
            // one free tile ring around the footprint keeps streets walkable; the tile under the door must be walkable land
            for (int y = origin.y - 1; y <= origin.y + h; y++)
                for (int x = origin.x - 1; x <= origin.x + w; x++)
                {
                    if (!map.InBounds(x, y)) return false;
                    int i = map.Index(x, y);
                    bool inside = x >= origin.x && x < origin.x + w && y >= origin.y && y < origin.y + h;
                    int other = map.Building[i];
                    if ((inside || !ringBuildingsOk) && other >= 0 && other != ignore && (Buildings[other].Flags & BuildingFlags.Ruin) == 0) return false;
                    if (!inside) continue;
                    ushort flags = map.Flags[i];
                    ushort blocked = other == ignore && ignore >= 0 ? (ushort)(TileFlags.Water | TileFlags.Burning | TileFlags.River) : (ushort)(TileFlags.Water | TileFlags.Burning | TileFlags.Reserved | TileFlags.River);
                    if ((map.Tables.TypeFlags[map.Ground[i]] & (ushort)TileFlags.Buildable) == 0 || (flags & blocked) != 0) return false;
                    if (city >= 0 && map.Zones[map.ZoneIndexOf(x, y)].OwnerCity != city) return false;
                    ushort f = map.Feature[i];
                    if (f != 0)
                    {
                        // trees, plants, bone piles and plain rocks are cleared; metal veins stay
                        if (!clearOre && map.Tables.FeatureKind[f] == FeatureDef.KindOre && f != BonesFeature && FeatureRes[f] != ResStone) return false;
                    }
                }
            var door = new int2(origin.x + w / 2, origin.y - 1);
            return map.IsWalkable(door.x, door.y) && !map.IsWater(door.x, door.y);
        }

        public int PlaceBuilding(int city, int def, int2 origin, float progress, long tick)
        {
            var d = Content.Buildings[def];
            var data = new BuildingData
            {
                Def = (ushort)def,
                Origin = origin,
                W = (byte)d.TileW,
                H = (byte)d.TileH,
                City = city,
                MaxHp = d.Hp,
                Hp = progress >= 1f ? d.Hp : math.max(1f, d.Hp * 0.1f),
                BuildProgress = progress,
                Style = city >= 0 ? City(city).Style : (ushort)0,
            };
            int b;
            if (FreeBuildings.Length > 0)
            {
                b = FreeBuildings[FreeBuildings.Length - 1];
                FreeBuildings.RemoveAt(FreeBuildings.Length - 1);
                Buildings[b] = data;
            }
            else
            {
                b = Buildings.Length;
                Buildings.Add(data);
            }
            Stamp(b, true);
            if (city >= 0) InsertSorted(CityBuildings[city], b);
            Version++;
            return b;
        }

        // Writes (or clears) the footprint: blocking tiles except the door; plants under it are cleared.
        void Stamp(int b, bool on)
        {
            var d = Buildings[b];
            bool blocking = (d.Flags & BuildingFlags.Ruin) == 0;
            int2 door = d.Door;
            for (int y = d.Origin.y; y < d.Origin.y + d.H; y++)
                for (int x = d.Origin.x; x < d.Origin.x + d.W; x++)
                {
                    if (!Map.InBounds(x, y)) continue;
                    if (on)
                    {
                        if (Map.Feature[Map.Index(x, y)] != 0) Map.SetFeature(x, y, 0, ChangeSource.Building);
                        Map.SetBuilding(x, y, b, blocking && !(x == door.x && y == door.y));
                    }
                    else if (Map.Building[Map.Index(x, y)] == b) Map.SetBuilding(x, y, -1, false);
                }
        }

        // Destroyed: becomes a walkable ruin (removed after 5 years); residents lose their home.
        public void Ruin(int b, long tick)
        {
            var d = Buildings[b];
            if ((d.Flags & (BuildingFlags.Ruin | BuildingFlags.Dead)) != 0) return;
            Stamp(b, false);
            d.Flags = BuildingFlags.Ruin;
            d.RuinTick = tick;
            d.Hp = 0f;
            d.Residents = 0;
            int city = d.City;
            Buildings[b] = d;
            Stamp(b, true);
            if (city >= 0 && CityAlive(city) && City(city).CenterBuilding == b) City(city).CenterBuilding = -1;
            Version++;
        }

        // Removed without a ruin (replaced center, expired ruin).
        public void Remove(int b)
        {
            var d = Buildings[b];
            if (!d.Alive) return;
            Stamp(b, false);
            if (d.City >= 0 && d.City < CityBuildings.Count) CityBuildings[d.City].Remove(b);
            d.Flags = BuildingFlags.Dead;
            Buildings[b] = d;
            FreeBuildings.Add(b);
            Version++;
        }

        // --- roads (Bölüm 5.4): A* from the door to the city center, road tiles cost 0.5, water is forbidden ---

        int[] _cost, _from, _stamp;
        int _stampId;
        readonly List<int> _heapNode = new List<int>(), _heapKey = new List<int>();

        public void BuildRoad(int2 from, int2 to, int maxRadius = 48)
        {
            var map = Map;
            if (!map.InBounds(from.x, from.y) || !map.InBounds(to.x, to.y)) return;
            int n = map.TileCount;
            if (_cost == null || _cost.Length != n) { _cost = new int[n]; _from = new int[n]; _stamp = new int[n]; _stampId = 0; }
            _stampId++;
            _heapNode.Clear();
            _heapKey.Clear();
            int start = map.Index(from.x, from.y), goal = map.Index(to.x, to.y);
            _cost[start] = 0; _from[start] = -1; _stamp[start] = _stampId;
            Push(start, 0);
            int w = map.Width;
            int expansions = 0;
            while (_heapNode.Count > 0 && expansions++ < 20000)
            {
                int cur = Pop();
                if (cur == goal) break;
                int cx = cur % w, cy = cur / w;
                for (int k = 0; k < 4; k++)
                {
                    int nx = cx + (k == 0 ? 1 : k == 1 ? -1 : 0), ny = cy + (k == 2 ? 1 : k == 3 ? -1 : 0);
                    if (!map.InBounds(nx, ny) || math.abs(nx - from.x) > maxRadius || math.abs(ny - from.y) > maxRadius) continue;
                    int ni = map.Index(nx, ny);
                    ushort f = map.Flags[ni];
                    if ((f & (ushort)TileFlags.Walkable) == 0 || (f & (ushort)TileFlags.Water) != 0) continue;
                    int step = (f & (ushort)TileFlags.Road) != 0 ? 1 : 2;
                    int c = _cost[cur] + step;
                    if (_stamp[ni] == _stampId && _cost[ni] <= c) continue;
                    _stamp[ni] = _stampId; _cost[ni] = c; _from[ni] = cur;
                    Push(ni, c + math.abs(nx - to.x) + math.abs(ny - to.y));
                }
            }
            if (_stamp[goal] != _stampId) return;
            for (int t = goal; t >= 0; t = _from[t])
            {
                if (map.Building[t] < 0) map.SetRoad(t % w, t / w, true);
                if (t == start) break;
            }
        }

        void Push(int node, int key)
        {
            _heapNode.Add(node);
            _heapKey.Add(key);
            int i = _heapNode.Count - 1;
            while (i > 0)
            {
                int p = (i - 1) >> 1;
                if (_heapKey[p] <= _heapKey[i]) break;
                Swap(i, p);
                i = p;
            }
        }

        int Pop()
        {
            int top = _heapNode[0];
            int last = _heapNode.Count - 1;
            Swap(0, last);
            _heapNode.RemoveAt(last);
            _heapKey.RemoveAt(last);
            int i = 0;
            while (true)
            {
                int l = 2 * i + 1, r = l + 1, m = i;
                if (l < _heapNode.Count && _heapKey[l] < _heapKey[m]) m = l;
                if (r < _heapNode.Count && _heapKey[r] < _heapKey[m]) m = r;
                if (m == i) break;
                Swap(i, m);
                i = m;
            }
            return top;
        }

        void Swap(int a, int b)
        {
            int n = _heapNode[a]; _heapNode[a] = _heapNode[b]; _heapNode[b] = n;
            int k = _heapKey[a]; _heapKey[a] = _heapKey[b]; _heapKey[b] = k;
        }

        // --- items (Bölüm 5.7) ---

        public int CreateItem(int type, int material, int quality, long tick, long makerUid, int city)
        {
            Items.Add(new ItemData
            {
                Uid = NextItemUid++, Type = (ushort)type, Material = (ushort)math.max(0, material), Quality = (ushort)quality,
                NameId = -1, CreatedTick = tick, MakerUid = makerUid, Owner = -1, City = city, DroppedTile = -1,
            });
            ItemDurability.Add(MaxItemDurability);
            return Items.Length - 1;
        }

        // --- caches ---

        public void RebuildCaches(UnitStore units)
        {
            for (int c = 0; c < Cities.Count; c++)
            {
                while (CityZones.Count <= c) { CityZones.Add(new List<int>()); CityBuildings.Add(new List<int>()); CityResidents.Add(new List<int>()); CityFoodFlows.Add(new CityFoodFlow()); CityGoldFlows.Add(new CityGoldFlow()); }
                while (_cityFloodCropLossUntil.Count <= c) _cityFloodCropLossUntil.Add(0);
                CityZones[c].Clear(); CityBuildings[c].Clear(); CityResidents[c].Clear();
            }
            for (int z = 0; z < Map.Zones.Length; z++)
            {
                int c = Map.Zones[z].OwnerCity;
                if (c >= 0 && c < Cities.Count) CityZones[c].Add(z);
            }
            for (int i = 0; i < Map.Building.Length; i++) Map.Building[i] = -1;
            for (int b = 0; b < Buildings.Length; b++)
            {
                var d = Buildings[b];
                if (!d.Alive) continue;
                if (d.City >= 0 && d.City < Cities.Count) CityBuildings[d.City].Add(b);
                for (int y = d.Origin.y; y < d.Origin.y + d.H; y++)
                    for (int x = d.Origin.x; x < d.Origin.x + d.W; x++)
                        if (Map.InBounds(x, y)) Map.Building[Map.Index(x, y)] = b; // flags are saved with the map
            }
            RecountResidents(units);
            Version++;
        }

        public void RecountResidents(UnitStore units)
        {
            for (int c = 0; c < CityResidents.Count; c++) CityResidents[c].Clear();
            for (int k = 0; k < units.Alive.Length; k++)
            {
                int i = units.Alive[k];
                int c = units.City[i];
                if (c >= 0 && c < CityResidents.Count && units.State[i] == UnitStore.StateAlive) CityResidents[c].Add(i);
            }
            for (int c = 0; c < Cities.Count; c++)
            {
                City(c).Population = CityResidents[c].Count;
                Cities.Header(c).MemberCount = CityResidents[c].Count;
            }
        }

        // --- save (section 11) ---

        public void Write(BinaryWriter w)
        {
            Cities.Write(w);
            WriteList(w, Buildings);
            WriteList(w, FreeBuildings);
            WriteList(w, Inventory);
            WriteList(w, Items);
            w.Write(NextItemUid);
            WriteList(w, Boats);
            w.Write(ItemDurabilityMagic);
            WriteList(w, ItemDurability);
            while (_cityFloodCropLossUntil.Count < Cities.Count) _cityFloodCropLossUntil.Add(0);
            w.Write(FloodCropMemoryMagic);
            w.Write(Cities.Count);
            for (int c = 0; c < Cities.Count; c++) w.Write(_cityFloodCropLossUntil[c]);
        }

        public void Read(BinaryReader r)
        {
            Cities.Read(r);
            ReadList(r, Buildings);
            ReadList(r, FreeBuildings);
            ReadList(r, Inventory);
            ReadList(r, Items);
            NextItemUid = r.ReadInt64();
            if (r.BaseStream.Position < r.BaseStream.Length) ReadList(r, Boats); // saves before boats
            else Boats.Clear();
            ItemDurability.Clear();
            for (int i = 0; i < Items.Length; i++) ItemDurability.Add(MaxItemDurability);
            if (r.BaseStream.Position < r.BaseStream.Length)
            {
                if (r.ReadInt32() != ItemDurabilityMagic) throw new InvalidDataException("Unknown item durability extension");
                int count = r.ReadInt32();
                if (count != Items.Length) throw new InvalidDataException("Item durability count does not match the item list");
                ItemDurability.Resize(count, NativeArrayOptions.ClearMemory);
                var bytes = ItemDurability.AsArray().AsSpan();
                if (r.Read(bytes) != bytes.Length) throw new EndOfStreamException("Truncated item durability extension");
            }
            _cityFloodCropLossUntil.Clear();
            for (int c = 0; c < Cities.Count; c++) _cityFloodCropLossUntil.Add(0);
            if (r.BaseStream.Position < r.BaseStream.Length)
            {
                if (r.ReadInt32() != FloodCropMemoryMagic) throw new InvalidDataException("Unknown flood crop memory extension");
                int count = r.ReadInt32();
                if (count != Cities.Count) throw new InvalidDataException("Flood crop memory count does not match the city list");
                for (int c = 0; c < count; c++) _cityFloodCropLossUntil[c] = r.ReadInt64();
            }
        }

        static void WriteList<T>(BinaryWriter w, NativeList<T> list) where T : unmanaged
        {
            w.Write(list.Length);
            w.Write(MemoryMarshal.AsBytes(list.AsArray().AsReadOnlySpan()));
        }

        static void ReadList<T>(BinaryReader r, NativeList<T> list) where T : unmanaged
        {
            list.Resize(r.ReadInt32(), NativeArrayOptions.ClearMemory);
            var bytes = MemoryMarshal.AsBytes(list.AsArray().AsSpan());
            if (r.Read(bytes) != bytes.Length) throw new EndOfStreamException("Truncated civ section");
        }

        public bool HasInvention(int kingdom, int invIndex) =>
            KingdomInventions.TryGetValue(kingdom, out var set) && set.Contains(invIndex);

        public bool HasInvention(int kingdom, string invId)
        {
            if (kingdom < 0 || !Content.Inventions.TryGet(invId, out var def)) return false;
            return HasInvention(kingdom, def.Index);
        }

        public void UnlockInvention(int kingdom, int invIndex)
        {
            if (!KingdomInventions.TryGetValue(kingdom, out var set))
            {
                set = new HashSet<int>();
                KingdomInventions[kingdom] = set;
            }
            set.Add(invIndex);
        }

        public void Dispose()
        {
            Cities.Dispose();
            if (Buildings.IsCreated) Buildings.Dispose();
            if (FreeBuildings.IsCreated) FreeBuildings.Dispose();
            if (Boats.IsCreated) Boats.Dispose();
            if (Vehicles.IsCreated) Vehicles.Dispose();
            if (Inventory.IsCreated) Inventory.Dispose();
            if (Items.IsCreated) Items.Dispose();
            if (ItemDurability.IsCreated) ItemDurability.Dispose();
        }
    }
}
