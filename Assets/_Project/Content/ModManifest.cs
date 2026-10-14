using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace PG.Content
{
    // Bölüm 9.3 Mods/<id>/manifest.json. A folder without a manifest loads under its folder name with no dependencies.
    public sealed class ModManifest
    {
        public const int CurrentContentSchemaVersion = 1;

        public string Id { get; set; }
        public string Name { get; set; }
        public string Version { get; set; } = "1.0.0";
        // Missing value means v1 for compatibility with existing manifests.
        public int ContentSchemaVersion { get; set; } = CurrentContentSchemaVersion;
        public string GameVersion { get; set; }
        public List<string> Dependencies { get; set; } = new List<string>(); // "author.lib>=1.0"
        public List<string> LoadAfter { get; set; } = new List<string>();
        public string Description { get; set; }
        public bool HasCode { get; set; }

        [JsonIgnore] public string Folder { get; private set; }

        public static ModManifest Read(string folder, List<string> errors, string schemaJson, bool schemaSupported)
        {
            string path = Path.Combine(folder, "manifest.json");
            ModManifest m = null;
            if (ContentFiles.Exists(path))
            {
                string json = ContentFiles.ReadAllText(path);
                if (!schemaSupported)
                {
                    Warnings.Add($"{path}: bundled mod manifest schema is unavailable or unsupported; mod skipped");
                    return null;
                }
                if (!ModManifestSchema.TryValidate(json, schemaJson, out var schemaErrors))
                {
                    Warnings.Add($"{path}: manifest does not match the supported schema: {string.Join("; ", schemaErrors)}; mod skipped");
                    return null;
                }
                try { m = JsonConvert.DeserializeObject<ModManifest>(json); }
                catch (JsonException e) { errors.Add($"{path}: {e.Message}"); return null; }
            }
            m ??= new ModManifest();
            if (string.IsNullOrEmpty(m.Id)) m.Id = Path.GetFileName(folder);
            m.Dependencies ??= new List<string>();
            m.LoadAfter ??= new List<string>();
            m.Folder = folder;
            return m;
        }

        // Topological order over dependencies + loadAfter, ties by id. A mod whose dependency is missing, too old or in a
        // cycle is skipped with a warning (not fatal: the base game still starts). Code mods are not loaded (DECISIONS #82).
        public static List<ModManifest> Order(IReadOnlyList<string> folders, List<string> errors, string schemaJson = null)
        {
            var warnings = Warnings;
            warnings.Clear();
            bool schemaSupported = false;
            string schemaError = null;
            if (!string.IsNullOrWhiteSpace(schemaJson))
                schemaSupported = ModManifestSchema.IsSchemaSupported(schemaJson, out schemaError);
            if (!schemaSupported && !string.IsNullOrWhiteSpace(schemaJson))
                warnings.Add("Bundled mod manifest schema is invalid or uses unsupported keywords: " + schemaError);
            var all = new SortedDictionary<string, ModManifest>(StringComparer.Ordinal);
            foreach (var f in folders)
            {
                var m = Read(f, errors, schemaJson, schemaSupported);
                if (m == null) continue;
                if (m.ContentSchemaVersion != CurrentContentSchemaVersion)
                {
                    warnings.Add($"{m.Id}: content schema {m.ContentSchemaVersion} is unsupported; expected {CurrentContentSchemaVersion}, mod skipped");
                    continue;
                }
                if (all.ContainsKey(m.Id)) { warnings.Add($"{m.Id}: duplicate mod id, {f} skipped"); continue; }
                if (m.HasCode) warnings.Add($"{m.Id}: code mods are not supported; loading its data only");
                all.Add(m.Id, m);
            }
            bool changed = true;
            while (changed)
            {
                changed = false;
                foreach (var m in new List<ModManifest>(all.Values))
                    foreach (var dep in m.Dependencies)
                    {
                        Split(dep, out string id, out string min);
                        if (all.TryGetValue(id, out var d) && (min == null || Compare(d.Version, min) >= 0)) continue;
                        warnings.Add($"{m.Id}: needs {dep}; mod skipped");
                        all.Remove(m.Id);
                        changed = true;
                        break;
                    }
            }
            var cycleIds = FindLoadOrderCycles(all);
            if (cycleIds.Count > 0)
            {
                var skipped = new List<string>();
                foreach (var m in all.Values)
                    if (cycleIds.Contains(m.Id))
                    {
                        warnings.Add($"{m.Id}: load order cycle, mod skipped");
                        skipped.Add(m.Id);
                    }
                foreach (var id in skipped) all.Remove(id);

                // A dependent cannot load after its required mod was skipped from a cycle.
                changed = true;
                while (changed)
                {
                    changed = false;
                    foreach (var m in new List<ModManifest>(all.Values))
                        foreach (var dep in m.Dependencies)
                        {
                            Split(dep, out string id, out string min);
                            if (all.TryGetValue(id, out var d) && (min == null || Compare(d.Version, min) >= 0)) continue;
                            warnings.Add($"{m.Id}: needs {dep}; mod skipped");
                            all.Remove(m.Id);
                            changed = true;
                            break;
                        }
                }
            }
            var result = new List<ModManifest>();
            var state = new Dictionary<string, int>(); // 1 visiting, 2 done
            foreach (var m in all.Values) Visit(m, all, state, result, warnings);
            return result;
        }

        public static readonly List<string> Warnings = new List<string>();

        static void Visit(ModManifest m, SortedDictionary<string, ModManifest> all, Dictionary<string, int> state, List<ModManifest> result, List<string> warnings)
        {
            if (state.TryGetValue(m.Id, out int s)) { if (s == 1) warnings.Add($"{m.Id}: load order cycle"); return; }
            state[m.Id] = 1;
            foreach (var dep in m.Dependencies) { Split(dep, out string id, out _); if (all.TryGetValue(id, out var d)) Visit(d, all, state, result, warnings); }
            foreach (var id in m.LoadAfter) if (all.TryGetValue(id, out var d)) Visit(d, all, state, result, warnings);
            state[m.Id] = 2;
            result.Add(m);
        }

        static HashSet<string> FindLoadOrderCycles(SortedDictionary<string, ModManifest> all)
        {
            var cycles = new HashSet<string>(StringComparer.Ordinal);
            var state = new Dictionary<string, int>(StringComparer.Ordinal);
            var path = new List<string>();
            foreach (var mod in all.Values)
                if (!state.ContainsKey(mod.Id))
                    VisitForCycles(mod, all, state, path, cycles);
            return cycles;
        }

        static void VisitForCycles(ModManifest mod, SortedDictionary<string, ModManifest> all,
            Dictionary<string, int> state, List<string> path, HashSet<string> cycles)
        {
            state[mod.Id] = 1;
            path.Add(mod.Id);
            var prerequisites = new List<ModManifest>();
            foreach (var dep in mod.Dependencies)
            {
                Split(dep, out string id, out _);
                if (all.TryGetValue(id, out var dependency)) prerequisites.Add(dependency);
            }
            foreach (var id in mod.LoadAfter)
                if (all.TryGetValue(id, out var prior)) prerequisites.Add(prior);

            foreach (var prerequisite in prerequisites)
            {
                if (state.TryGetValue(prerequisite.Id, out int mark))
                {
                    if (mark == 1)
                    {
                        int first = path.LastIndexOf(prerequisite.Id);
                        for (int i = first; i < path.Count; i++) cycles.Add(path[i]);
                    }
                    continue;
                }
                VisitForCycles(prerequisite, all, state, path, cycles);
            }
            path.RemoveAt(path.Count - 1);
            state[mod.Id] = 2;
        }

        static void Split(string dep, out string id, out string min)
        {
            int at = dep.IndexOf(">=", StringComparison.Ordinal);
            id = (at < 0 ? dep : dep.Substring(0, at)).Trim();
            min = at < 0 ? null : dep.Substring(at + 2).Trim();
        }

        // Dotted numeric versions: "1.10.0" > "1.9".
        public static int Compare(string a, string b)
        {
            var pa = (a ?? "0").Split('.');
            var pb = (b ?? "0").Split('.');
            for (int i = 0; i < Math.Max(pa.Length, pb.Length); i++)
            {
                int x = i < pa.Length && int.TryParse(pa[i], out int vx) ? vx : 0;
                int y = i < pb.Length && int.TryParse(pb[i], out int vy) ? vy : 0;
                if (x != y) return x.CompareTo(y);
            }
            return 0;
        }
    }
}
