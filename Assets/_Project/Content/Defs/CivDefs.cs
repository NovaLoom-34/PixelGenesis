using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PG.Content
{
    public struct ResAmount
    {
        public ushort Res;
        public int Amount;
    }

    public enum ResKind : byte { Material, Ore, Currency, Magic, Food, Medicine, Trade }

    // resources.json (Bölüm 5.5)
    public sealed class ResourceDef : ContentDef
    {
        public string Kind { get; set; }
        public int Nutrition { get; set; }
        public int Value { get; set; }
        public string Source { get; set; }

        [JsonIgnore] public ResKind KindCode { get; private set; }
        [JsonIgnore] public bool IsFood => KindCode == ResKind.Food;

        internal override void Resolve(ContentDB db, List<string> errors)
        {
            if (Enum.TryParse(Kind, true, out ResKind k)) KindCode = k;
            else errors.Add($"{Id}: unknown kind '{Kind}'");
        }
    }

    public enum BuildingCategory : byte { Center, House, Economy, Social, Military, Naval, Culture, Monument, Ruin }

    public enum BuildingReq : byte { Pop, Building, Language, Religion, Coast, Hills, Forest, Livestock, Flowers, Herbs, Capital, WarVictory, BeastCiv, HiveMind }

    public struct BuildingRequirement
    {
        public BuildingReq Kind;
        public int Value; // population or building index
    }

    // buildings.json (Bölüm 5.4). size is in cells; 1 cell = 3x3 tiles.
    public sealed class BuildingDef : ContentDef
    {
        public const int CellTiles = 3;

        public string Category { get; set; }
        public string Size { get; set; }
        public int Hp { get; set; }
        public Dictionary<string, int> Cost { get; set; }
        public string Requires { get; set; }
        public string Function { get; set; }
        public int MaxPerCity { get; set; }
        public int JobSlots { get; set; }
        public int GoldUpkeepPerYear { get; set; }

        [JsonIgnore] public BuildingCategory CategoryCode { get; private set; }
        [JsonIgnore] public int TileW { get; private set; }
        [JsonIgnore] public int TileH { get; private set; }
        [JsonIgnore] public ResAmount[] Costs { get; private set; } = Array.Empty<ResAmount>();
        [JsonIgnore] public BuildingRequirement[] Reqs { get; private set; } = Array.Empty<BuildingRequirement>();
        [JsonIgnore] public int CostTotal { get; private set; }
        [JsonIgnore] public string ShortId => Id.StartsWith("bld.") ? Id.Substring(4) : Id;

        internal override void Resolve(ContentDB db, List<string> errors)
        {
            if (GoldUpkeepPerYear < 0) errors.Add($"{Id}: annual gold upkeep cannot be negative");
            if (Enum.TryParse(Category, true, out BuildingCategory c)) CategoryCode = c;
            else errors.Add($"{Id}: unknown category '{Category}'");

            if (Size != "*")
            {
                var parts = (Size ?? "").Split('x');
                if (parts.Length == 2 && int.TryParse(parts[0], out int w) && int.TryParse(parts[1], out int h))
                {
                    TileW = w * CellTiles;
                    TileH = h * CellTiles;
                }
                else errors.Add($"{Id}: bad size '{Size}'");
            }

            var costs = new List<ResAmount>();
            int total = 0;
            if (Cost != null)
                foreach (var kv in Cost)
                {
                    int r = db.Resources.IdOrDefault(kv.Key);
                    if (r < 0) { errors.Add($"{Id}: unknown resource '{kv.Key}'"); continue; }
                    costs.Add(new ResAmount { Res = (ushort)r, Amount = kv.Value });
                    total += kv.Value;
                }
            Costs = costs.ToArray();
            CostTotal = total;

            var reqs = new List<BuildingRequirement>();
            if (!string.IsNullOrEmpty(Requires))
                foreach (var raw in Requires.Split('|'))
                {
                    string token = raw.Trim();
                    if (token.Length == 0) continue;
                    if (token.StartsWith("pop:") && int.TryParse(token.Substring(4), out int pop))
                    {
                        reqs.Add(new BuildingRequirement { Kind = BuildingReq.Pop, Value = pop });
                        continue;
                    }
                    if (TryCode(token, out var code))
                    {
                        reqs.Add(new BuildingRequirement { Kind = code });
                        continue;
                    }
                    string bid = token == "farm" ? "bld.farm_shed" : "bld." + token;
                    int b = db.Buildings.IdOrDefault(bid);
                    if (b >= 0) reqs.Add(new BuildingRequirement { Kind = BuildingReq.Building, Value = b });
                    else errors.Add($"{Id}: unknown requirement '{token}'");
                }
            Reqs = reqs.ToArray();
        }

        // requirement tokens used by buildings.json (Bölüm 5.4 planner table)
        static bool TryCode(string token, out BuildingReq code)
        {
            switch (token)
            {
                case "dil": code = BuildingReq.Language; return true;
                case "din": code = BuildingReq.Religion; return true;
                case "kıyı": code = BuildingReq.Coast; return true;
                case "hills/mountain yakın": code = BuildingReq.Hills; return true;
                case "orman yakın": code = BuildingReq.Forest; return true;
                case "evcil hayvan": code = BuildingReq.Livestock; return true;
                case "çiçek yakın": code = BuildingReq.Flowers; return true;
                case "herbs": code = BuildingReq.Herbs; return true;
                case "başkent": code = BuildingReq.Capital; return true;
                case "savaş zaferi": code = BuildingReq.WarVictory; return true;
                case "hayvan kökenli uygarlık": code = BuildingReq.BeastCiv; return true;
                case "kovan zihni": code = BuildingReq.HiveMind; return true;
                default: code = default; return false;
            }
        }
    }

    // building_styles.json
    public sealed class BuildingStyleDef : ContentDef
    {
        public string Description { get; set; }
        public string Accent { get; set; }
        public string Wall { get; set; }  // material colour, gray indices 1-4 (EkC 4.1)
        public string Trim { get; set; }  // door/window frame, index 7
        public float RoofMix { get; set; } = 0.3f; // share of Accent in the roof; the rest is the owner kingdom's colour
        [JsonIgnore] public string ShortId => Id.StartsWith("style.") ? Id.Substring(6) : Id;
        [JsonIgnore] public UnityEngine.Color32[] ParsedColors { get; private set; } // accent, wall, trim

        internal override void Resolve(ContentDB db, List<string> errors)
        {
            var hex = new[] { Accent, Wall, Trim };
            ParsedColors = new UnityEngine.Color32[hex.Length];
            for (int i = 0; i < hex.Length; i++)
                if (!HexColor.TryParse(hex[i], out ParsedColors[i])) errors.Add($"{Id}: invalid color '{hex[i]}'");
        }
    }

    // jobs.json (Bölüm 5.6)
    public sealed class JobDef : ContentDef
    {
        public string Task { get; set; }
        public string QuotaRule { get; set; }
        [JsonProperty("requiresBuilding")] public string RequiresBuildingId { get; set; }
        [JsonIgnore] public int RequiredBuilding { get; private set; } = -1;

        internal override void Resolve(ContentDB db, List<string> errors)
        {
            if (string.IsNullOrEmpty(RequiresBuildingId)) return;
            RequiredBuilding = db.Buildings.IdOrDefault(RequiresBuildingId);
            if (RequiredBuilding < 0) errors.Add($"{Id}: unknown building '{RequiresBuildingId}'");
        }
    }

    public enum EquipSlot : byte { Weapon, Helmet, Armor, Shield, Boots, Ring, Amulet, Count }

    // equipment_types.json (Bölüm 5.7)
    public sealed class EquipmentTypeDef : ContentDef
    {
        public string Slot { get; set; }
        public JObject BaseEffects { get; set; }
        public bool UsesMaterial { get; set; }

        [JsonIgnore] public EquipSlot SlotCode { get; private set; }
        [JsonIgnore] public StatMod[] Mods { get; private set; }
        [JsonIgnore] public Dictionary<string, float> Behavior { get; private set; }

        internal override void Resolve(ContentDB db, List<string> errors)
        {
            if (Enum.TryParse(Slot, true, out EquipSlot s) && s != EquipSlot.Count) SlotCode = s;
            else errors.Add($"{Id}: unknown slot '{Slot}'");
            Behavior = new Dictionary<string, float>();
            Mods = StatKeys.Parse(BaseEffects?["add"] as JObject, BaseEffects?["pct"] as JObject, Behavior);
        }
    }

    // materials.json
    public sealed class MaterialDef : ContentDef
    {
        public float Multiplier { get; set; }
        public int Tier { get; set; }
        public string Special { get; set; }
    }

    // item_qualities.json
    public sealed class ItemQualityDef : ContentDef
    {
        public float Multiplier { get; set; }
        public float BaseChance { get; set; }
    }

    // happiness_events.json (Bölüm 5.8)
    public sealed class HappinessEventDef : ContentDef
    {
        public int Value { get; set; }
    }

    // inventions.json (Bölüm 5.12)
    public sealed class InventionDef : ContentDef
    {
        public string Category { get; set; }
        public int Cost { get; set; }
        public string[] Reqs { get; set; } = Array.Empty<string>();
        public JObject Effects { get; set; }
        public string Description { get; set; }

        [JsonIgnore] public int[] ReqIndices { get; private set; } = Array.Empty<int>();
        [JsonIgnore] public StatMod[] Mods { get; private set; }
        [JsonIgnore] public Dictionary<string, float> Behavior { get; private set; }

        internal override void Resolve(ContentDB db, List<string> errors)
        {
            Behavior = new Dictionary<string, float>();
            Mods = StatKeys.Parse(Effects?["add"] as JObject, Effects?["pct"] as JObject, Behavior);
            if (Reqs != null && Reqs.Length > 0)
            {
                var reqList = new List<int>();
                foreach (var req in Reqs)
                {
                    if (db.Inventions.TryGet(req, out var inv)) reqList.Add(inv.Index);
                    else errors.Add($"{Id}: unknown required invention '{req}'");
                }
                ReqIndices = reqList.ToArray();
            }
        }
    }
}
