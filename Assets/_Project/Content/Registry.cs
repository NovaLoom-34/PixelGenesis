using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PG.Content
{
    public sealed class Registry<T> where T : ContentDef
    {
        readonly List<T> _items = new List<T>();
        readonly Dictionary<string, int> _byKey = new Dictionary<string, int>();

        public Registry(string category)
        {
            Category = category;
        }

        public string Category { get; }
        public int Count => _items.Count;
        public IReadOnlyList<T> All => _items;
        public T this[int id] => _items[id];

        public int IdOf(string key)
        {
            if (key != null && _byKey.TryGetValue(key, out int id)) return id;
            throw new KeyNotFoundException($"{Category}: unknown id '{key}'");
        }

        public bool TryGet(string key, out T def)
        {
            if (key != null && _byKey.TryGetValue(key, out int id))
            {
                def = _items[id];
                return true;
            }
            def = null;
            return false;
        }

        public int IdOrDefault(string key, int fallback = -1) =>
            key != null && _byKey.TryGetValue(key, out int id) ? id : fallback;

        // New key -> appended with the next numeric id. Existing key (mod) -> fields are patched in place.
        internal void AddOrPatch(JObject json, JsonSerializer serializer, string source, List<string> errors)
        {
            string id = (string)json["id"];
            if (string.IsNullOrEmpty(id))
            {
                errors.Add($"{source}: record without 'id' in {Category}");
                return;
            }

            try
            {
                if (_byKey.TryGetValue(id, out int existing))
                {
                    ApplyListPatches(json, JObject.FromObject(_items[existing], serializer));
                    using (var reader = json.CreateReader()) serializer.Populate(reader, _items[existing]);
                    return;
                }
                ApplyListPatches(json, new JObject());

                var def = json.ToObject<T>(serializer);
                def.Index = _items.Count;
                _items.Add(def);
                _byKey.Add(id, def.Index);
            }
            catch (JsonException e)
            {
                errors.Add($"{source}: {id}: {e.Message}");
            }
        }

        // Bölüm 9.3 patch syntax: "+trees": [...] appends, "-trees": [...] removes. Comma-separated string lists work too.
        static void ApplyListPatches(JObject json, JObject current)
        {
            var patches = new List<JProperty>();
            foreach (var p in json.Properties()) if (p.Name.Length > 1 && (p.Name[0] == '+' || p.Name[0] == '-')) patches.Add(p);
            foreach (var p in patches)
            {
                string key = p.Name.Substring(1);
                bool add = p.Name[0] == '+';
                var existing = json[key] ?? Find(current, key);
                bool csv = existing != null && existing.Type == JTokenType.String;
                var list = new List<JToken>();
                if (csv) foreach (var part in ((string)existing).Split(',')) { var t = part.Trim(); if (t.Length > 0) list.Add(t); }
                else if (existing is JArray arr) foreach (var t in arr) list.Add(t);
                var items = p.Value is JArray changes ? (IEnumerable<JToken>)changes : new[] { p.Value };
                foreach (var change in items)
                {
                    int at = list.FindIndex(t => JToken.DeepEquals(t, change));
                    if (add && at < 0) list.Add(change);
                    else if (!add && at >= 0) list.RemoveAt(at);
                }
                json.Remove(p.Name);
                var name = Find(current, key)?.Parent is JProperty prop ? prop.Name : key;
                if (csv)
                {
                    var parts = new List<string>();
                    foreach (var t in list) parts.Add((string)t);
                    json[name] = string.Join(", ", parts);
                }
                else json[name] = new JArray(list);
            }
        }

        static JToken Find(JObject obj, string key) =>
            obj.TryGetValue(key, System.StringComparison.OrdinalIgnoreCase, out var v) && v.Type != JTokenType.Null ? v : null;
    }
}
