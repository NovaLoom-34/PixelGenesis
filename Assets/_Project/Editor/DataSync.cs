using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PG.EditorTools
{
    // Mirrors <repo>/Data/*.json into Assets/StreamingAssets/Data.
    // A copy instead of a symlink because symlinks are unreliable on Windows (EkB 3.3 step 7).
    [InitializeOnLoad]
    public sealed class DataSync : IPreprocessBuildWithReport
    {
        static DataSync()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report) => Sync();

        [MenuItem("PixelGenesis/Sync Data")]
        public static void Sync()
        {
            string root = Path.GetDirectoryName(Application.dataPath);
            int changed = Mirror(Path.Combine(root, "Data"), Path.Combine(Application.streamingAssetsPath, "Data"));
            changed += MirrorTree(Path.Combine(root, "Data", "_schemas"), Path.Combine(Application.streamingAssetsPath, "Data", "_schemas"), "*.json");
            changed += MirrorTree(Path.Combine(root, "Art", "Placeholder"), Path.Combine(Application.streamingAssetsPath, "Art", "Placeholder"), "*.png");
            changed += MirrorTree(Path.Combine(root, "Art", "Final"), Path.Combine(Application.streamingAssetsPath, "Art", "Final"), "*.png", IncludeRuntimeFinalPng);
            changed += MirrorTree(Path.Combine(root, "Art", "Final", "buildings"), Path.Combine(Application.streamingAssetsPath, "Art", "Final", "buildings"), "*.json");
            changed += MirrorTree(Path.Combine(root, "Art", "Final", "features"), Path.Combine(Application.streamingAssetsPath, "Art", "Final", "features"), "*.json");
            changed += MirrorTree(Path.Combine(root, "Art", "Final", "units"), Path.Combine(Application.streamingAssetsPath, "Art", "Final", "units"), "*.json", IncludeRuntimeUnitJson);
            // Bölüm 9.1 audio files and 9.6 in-game "What's new" (CHANGELOG.md)
            foreach (string ext in new[] { "*.wav", "*.ogg" })
                changed += MirrorTree(Path.Combine(root, "Audio"), Path.Combine(Application.streamingAssetsPath, "Audio"), ext);
            string log = Path.Combine(root, "CHANGELOG.md"), logDst = Path.Combine(Application.streamingAssetsPath, "CHANGELOG.md");
            if (File.Exists(log) && (!File.Exists(logDst) || File.ReadAllText(log) != File.ReadAllText(logDst))) { File.Copy(log, logDst, true); changed++; }
            // Web builds cannot enumerate StreamingAssets over HTTP: publish the full file list
            // so ContentFiles' virtual FS can resolve directories (Bölüm web dağıtımı).
            WriteManifest(Application.streamingAssetsPath);
            if (changed == 0) return;
            AssetDatabase.Refresh();
            Debug.Log($"[DataSync] {changed} file(s) updated in StreamingAssets/Data and Art");
        }

        // Hand-rolled JSON on purpose: the editor assembly has no Newtonsoft reference.
        static void WriteManifest(string streamingRoot)
        {
            var files = new System.Collections.Generic.List<string>();
            foreach (string p in Directory.GetFiles(streamingRoot, "*", SearchOption.AllDirectories))
            {
                string rel = Path.GetRelativePath(streamingRoot, p).Replace('\\', '/');
                if (rel == "manifest.json" || rel.EndsWith(".meta", StringComparison.Ordinal)) continue;
                files.Add(rel);
            }
            files.Sort(StringComparer.Ordinal);
            var sb = new System.Text.StringBuilder("{\"files\":[");
            for (int i = 0; i < files.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append('"').Append(files[i]).Append('"');
            }
            sb.Append("]}");
            File.WriteAllText(Path.Combine(streamingRoot, "manifest.json"), sb.ToString());
        }

        // Copies top-level content *.json from src to dst when content differs and deletes dst *.json missing in src.
        // Tooling subfolders are skipped here; runtime schemas are mirrored explicitly by Sync().
        // Returns the number of files copied or deleted.
        public static int Mirror(string src, string dst)
        {
            if (!Directory.Exists(src)) throw new DirectoryNotFoundException($"Data folder not found: {src}");
            Directory.CreateDirectory(dst);

            int changed = 0;
            foreach (string s in Directory.GetFiles(src, "*.json"))
            {
                string d = Path.Combine(dst, Path.GetFileName(s));
                if (File.Exists(d) && SameContent(s, d)) continue;
                File.Copy(s, d, true);
                changed++;
            }

            foreach (string d in Directory.GetFiles(dst, "*.json"))
            {
                if (File.Exists(Path.Combine(src, Path.GetFileName(d)))) continue;
                File.Delete(d);
                if (File.Exists(d + ".meta")) File.Delete(d + ".meta");
                changed++;
            }

            return changed;
        }

        // Recursive mirror for runtime art (sprites are loaded by id at runtime, Bölüm 2.4 / EkC). Missing src = nothing to do.
        public static int MirrorTree(string src, string dst, string pattern)
            => MirrorTree(src, dst, pattern, null);

        static int MirrorTree(string src, string dst, string pattern, Func<string, bool> includeRelativePath)
        {
            if (!Directory.Exists(src)) return 0;
            int changed = 0;
            foreach (string s in Directory.GetFiles(src, pattern, SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(src, s);
                if (includeRelativePath != null && !includeRelativePath(relative)) continue;
                string d = Path.Combine(dst, relative);
                if (File.Exists(d) && SameContent(s, d)) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(d));
                File.Copy(s, d, true);
                changed++;
            }
            if (!Directory.Exists(dst)) return changed;
            foreach (string d in Directory.GetFiles(dst, pattern, SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(dst, d);
                if ((includeRelativePath == null || includeRelativePath(relative)) && File.Exists(Path.Combine(src, relative))) continue;
                File.Delete(d);
                if (File.Exists(d + ".meta")) File.Delete(d + ".meta");
                changed++;
            }
            return changed;
        }

        static bool IncludeRuntimeFinalPng(string relativePath)
        {
            string normalized = relativePath.Replace('\\', '/');
            return !normalized.StartsWith("units/", StringComparison.OrdinalIgnoreCase) ||
                   !Path.GetFileName(normalized).EndsWith("_detail.png", StringComparison.OrdinalIgnoreCase);
        }

        static bool IncludeRuntimeUnitJson(string relativePath) =>
            !Path.GetFileName(relativePath).EndsWith("_detail.json", StringComparison.OrdinalIgnoreCase);

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode) Sync();
        }

        static bool SameContent(string a, string b)
        {
            if (new FileInfo(a).Length != new FileInfo(b).Length) return false;
            return File.ReadAllBytes(a).AsSpan().SequenceEqual(File.ReadAllBytes(b));
        }
    }
}
