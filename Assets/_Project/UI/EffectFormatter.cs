using System.Text;
using Newtonsoft.Json.Linq;

namespace PG.UI
{
    // Bölüm 8.15 tooltip: JSON açıklama + hesaplanmış efekt listesi. Mirrors Data/_generator/catalog.py's fmt() so a
    // trait/gene/power's tooltip reads the same as its Ek A catalog entry ("hız +20%", "→ hero", "neuron:pack_hunt").
    public static class EffectFormatter
    {
        public static string Format(JObject effects)
        {
            if (effects == null || effects.Count == 0) return "";
            var sb = new StringBuilder();
            foreach (var prop in effects.Properties())
            {
                string key = prop.Name;
                var value = prop.Value;
                if (key == "add" && value is JObject add)
                    foreach (var m in add.Properties()) Line(sb, StatName(m.Name) + " " + Signed((float)m.Value));
                else if (key == "pct" && value is JObject pct)
                    foreach (var m in pct.Properties()) Line(sb, StatName(m.Name) + " " + Signed((float)m.Value) + "%");
                else if (value is JArray arr)
                {
                    string prefix = key == "grants" ? "→ " : key == "immunes" ? Loc.T("ui.immune") + " " : key == "spells" ? "✦ " :
                                    key == "flags" || key == "tags" || key == "neurons" ? "" : key + ":";
                    foreach (var x in arr) Line(sb, prefix + ShortId((string)x));
                }
                // other nested objects (e.g. onHit) aren't formatted; the description already covers them in prose
            }
            return sb.ToString();
        }

        // Trait/power/gene description followed by a blank line and the formatted effect list, if any.
        public static string WithEffects(string description, JObject effects)
        {
            string list = Format(effects);
            return string.IsNullOrEmpty(list) ? description ?? "" :
                   string.IsNullOrEmpty(description) ? list : description + "\n\n" + list;
        }

        static void Line(StringBuilder sb, string text)
        {
            if (sb.Length > 0) sb.Append('\n');
            sb.Append(text);
        }

        static string Signed(float v) => (v > 0 ? "+" : "") + v.ToString("0.##");

        static string StatName(string key) => Loc.T("stat." + key.ToLowerInvariant());

        // "tr.mage_blood" -> "mage blood" (matches catalog.py's fmt(): strip the dotted prefix, no further name lookup).
        static string ShortId(string id)
        {
            if (string.IsNullOrEmpty(id)) return "";
            int dot = id.IndexOf('.');
            return (dot >= 0 ? id.Substring(dot + 1) : id).Replace('_', ' ');
        }
    }
}
