using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PG.Content
{
    // name_sets.json (Bölüm 6.2): phonology sets keyed by language trait id, ruler titles by city count.
    public sealed class NameSets
    {
        public sealed class Set
        {
            public string Id;
            public string[] Onset, Vowel, Coda, Pattern;
        }

        public struct Title
        {
            public int MinCities;
            public string Text;
        }

        public Set[] Sets = Array.Empty<Set>();
        public Title[] RulerTitles = Array.Empty<Title>();

        public int IndexOf(string id)
        {
            for (int i = 0; i < Sets.Length; i++) if (Sets[i].Id == id) return i;
            return -1;
        }

        public static NameSets From(JObject o, List<string> errors)
        {
            var r = new NameSets();
            if (o == null) return r;
            var sets = new List<Set>();
            if (o["sets"] is JObject s)
                foreach (var p in s.Properties())
                {
                    if (!(p.Value is JObject v)) continue;
                    var set = new Set { Id = p.Name, Onset = Split(v, "onset"), Vowel = Split(v, "vowel"), Coda = Split(v, "coda"), Pattern = Split(v, "pattern") };
                    bool symbols = set.Pattern.Length == 1 && set.Pattern[0] == "SYMBOL"; // sign languages: symbol + number
                    if (!symbols && (set.Onset.Length == 0 || set.Vowel.Length == 0 || set.Pattern.Length == 0)) errors.Add($"name_sets: set '{p.Name}' needs onset, vowel and pattern");
                    else sets.Add(set);
                }
            r.Sets = sets.ToArray();
            var titles = new List<Title>();
            if (o["rulerTitles"] is JArray t)
                foreach (var e in t)
                    titles.Add(new Title { MinCities = e.Value<int?>("minCities") ?? 1, Text = e.Value<string>("title") ?? "" });
            titles.Sort((a, b) => a.MinCities.CompareTo(b.MinCities));
            r.RulerTitles = titles.ToArray();
            if (r.Sets.Length == 0) errors.Add("name_sets: no phonology sets");
            return r;
        }

        // "a,b," keeps the empty entry (an optional coda)
        static string[] Split(JObject o, string key)
        {
            string v = o.Value<string>(key);
            return string.IsNullOrEmpty(v) ? Array.Empty<string>() : v.Split(',');
        }
    }

    // kingdom_traits.json
    public sealed class KingdomTraitDef : ContentDef
    {
        public JObject Effects { get; set; }
        [JsonIgnore] public Dictionary<string, float> Values { get; private set; }

        internal override void Resolve(ContentDB db, List<string> errors)
        {
            Values = new Dictionary<string, float>();
            StatKeys.Parse(Effects?["add"] as JObject, Effects?["pct"] as JObject, Values); // all keys are behaviour keys ("pct.warChance")
        }

        public float Pct(string key) => Values.TryGetValue("pct." + key, out float v) ? v : 0f;
        public float Add(string key) => Values.TryGetValue("add." + key, out float v) ? v : 0f;
    }

    // war_types.json
    public sealed class WarTypeDef : ContentDef
    {
        public string[] Causes { get; set; }
        public string[] Goals { get; set; }

        internal override void Resolve(ContentDB db, List<string> errors)
        {
            Validate(Causes, AllowedCauses, "causes", errors);
            Validate(Goals, AllowedGoals, "goals", errors);
        }

        static readonly string[] AllowedCauses = { "border_dispute", "resource_shortage", "religious_difference", "cultural_difference", "revenge", "alliance_call", "rebellion_suppression", "succession_dispute" };
        static readonly string[] AllowedGoals = { "city_conquest", "tribute", "vassalize", "destruction", "plunder" };

        void Validate(string[] values, string[] allowed, string field, List<string> errors)
        {
            if (values == null || values.Length == 0) { errors.Add($"{Id}: war type needs at least one {field}"); return; }
            foreach (var value in values)
                if (Array.IndexOf(allowed, value) < 0) errors.Add($"{Id}: unknown war {field.TrimEnd('s')} '{value}'");
        }
    }

    // achievements.json (Bölüm 8.9): the condition text is shown in the UI, the check lives in PG.UI.AchievementTracker.
    public sealed class AchievementDef : ContentDef
    {
        public string Condition { get; set; }
        public string Unlocks { get; set; }
    }
}

namespace PG.Content
{
    // culture_traits / language_traits / religion_traits / clan_traits (Bölüm 6.7-6.10): stat mods for members,
    // behaviour keys ("pct.farmYield") for the systems that read them, plus the string keys below.
    public sealed class MetaTraitDef : ContentDef
    {
        public string Group { get; set; }
        public JObject Effects { get; set; }

        [JsonIgnore] public StatMod[] Mods { get; private set; }
        [JsonIgnore] public Dictionary<string, float> Values { get; private set; }
        [JsonIgnore] public string Leader { get; private set; }       // succession rule (culture)
        [JsonIgnore] public int[] SpellIds { get; private set; }      // religion spells
        [JsonIgnore] public string[] Flags { get; private set; }
        [JsonIgnore] public string[] Immunes { get; private set; }
        [JsonIgnore] public UnitFlags FlagBits { get; private set; }

        internal override void Resolve(ContentDB db, List<string> errors)
        {
            var e = Effects ?? new JObject();
            Values = new Dictionary<string, float>();
            Mods = StatKeys.Parse(e["add"] as JObject, e["pct"] as JObject, Values);
            Leader = e.Value<string>("leader");
            Flags = Strings(e, "flags");
            foreach (var f in Flags) FlagBits |= StatKeys.Flag(f);
            Immunes = Strings(e, "immunes");
            var spells = new List<int>();
            foreach (var s in Strings(e, "spells"))
            {
                int sp = db.Spells.IdOrDefault("spell." + s);
                if (sp >= 0) spells.Add(sp);
                else errors.Add($"{Id}: unknown spell 'spell.{s}'");
            }
            SpellIds = spells.ToArray();
        }

        public float Pct(string key) => Values.TryGetValue("pct." + key, out float v) ? v : 0f;
        public float Add(string key) => Values.TryGetValue("add." + key, out float v) ? v : 0f;
        public bool HasFlag(string flag) => Array.IndexOf(Flags, flag) >= 0;

        static string[] Strings(JObject e, string key)
        {
            if (!(e[key] is JArray arr)) return Array.Empty<string>();
            var result = new string[arr.Count];
            for (int i = 0; i < arr.Count; i++) result[i] = arr[i].Value<string>();
            return result;
        }
    }

    // plots.json (Bölüm 6.11)
    public sealed class PlotDef : ContentDef
    {
        public string Initiator { get; set; }
        public string[] Conditions { get; set; }
        public int MinParticipants { get; set; }
        public int DurationMonths { get; set; }
        public string Outcome { get; set; }
    }

    // book_types.json (Bölüm 6.12): "intel:+1" reader bonuses
    public sealed class BookTypeDef : ContentDef
    {
        public string[] ReaderBonus { get; set; }

        [JsonIgnore] public StatMod[] Mods { get; private set; }
        [JsonIgnore] public float Happiness { get; private set; }

        internal override void Resolve(ContentDB db, List<string> errors)
        {
            var mods = new List<StatMod>();
            foreach (var b in ReaderBonus ?? Array.Empty<string>())
            {
                var parts = b.Split(':');
                bool pct = parts.Length == 2 && parts[1].EndsWith("%");
                if (parts.Length != 2 || !float.TryParse(parts[1].Replace("+", "").Replace("%", ""), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v)) { errors.Add($"{Id}: bad bonus '{b}'"); continue; }
                if (parts[0] == "happiness") Happiness += v;
                else if (StatKeys.TryGet(parts[0], out var stat)) mods.Add(pct ? new StatMod { Stat = stat, Pct = v } : new StatMod { Stat = stat, Add = v });
                // other keys (faith) wait for their systems
            }
            Mods = mods.ToArray();
        }
    }
}
