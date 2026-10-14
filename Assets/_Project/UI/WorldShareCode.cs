using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using PG.Content;
using PG.Sim;
using PG.WorldGen;
using UnityEngine;

namespace PG.UI
{
    public static class WorldShareCode
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void PG_CopyText(string text);
        [DllImport("__Internal")] static extern void PG_DownloadText(string filename, string text, string mimeType);
        [DllImport("__Internal")] static extern void PG_DownloadWorldSummary(string title, string details);
#endif

        public static string Encode(WorldGenSettings source)
        {
            if (source == null || source.Seed == 0 || source.Template == "wgt.custom_image") return null;
            var settings = source.Clone();
            settings.ImagePath = null;
            settings.PreviewDivisor = 1;
            string json = JsonUtility.ToJson(settings);
            string payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            return "PGW1." + Checksum(payload).ToString("x8") + "." + payload;
        }

        public static bool TryDecode(string code, out WorldGenSettings settings)
        {
            settings = null;
            if (string.IsNullOrWhiteSpace(code)) return false;
            string[] parts = code.Trim().Split('.');
            if (parts.Length != 3 || parts[0] != "PGW1" || !uint.TryParse(parts[1], System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out uint expected) || Checksum(parts[2]) != expected) return false;
            try
            {
                string base64 = parts[2].Replace('-', '+').Replace('_', '/');
                base64 += new string('=', (4 - base64.Length % 4) % 4);
                var parsed = JsonUtility.FromJson<WorldGenSettings>(Encoding.UTF8.GetString(Convert.FromBase64String(base64)));
                if (parsed == null || parsed.Seed == 0 || string.IsNullOrWhiteSpace(parsed.Template) || parsed.Template == "wgt.custom_image") return false;
                parsed.ImagePath = null;
                parsed.PreviewDivisor = 1;
                parsed.Clamp();
                settings = parsed;
                return true;
            }
            catch { return false; }
        }

        static uint Checksum(string text)
        {
            unchecked
            {
                uint hash = 2166136261;
                for (int i = 0; i < text.Length; i++) { hash ^= text[i]; hash *= 16777619; }
                return hash;
            }
        }

        public static void CopyCode(WorldGenSettings settings, Action<string> toast)
        {
            string code = Encode(settings);
            if (string.IsNullOrEmpty(code)) { toast?.Invoke(Loc.T("ui.world_share_unavailable")); return; }
            CopyText(code);
            toast?.Invoke(Loc.T("ui.world_share_copied"));
        }

        public static void CopyText(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
#if UNITY_WEBGL && !UNITY_EDITOR
            PG_CopyText(text);
#else
            GUIUtility.systemCopyBuffer = text;
#endif
        }

        public static void ExportChronicle(SimWorld sim, Action<string> toast)
        {
            var text = new StringBuilder(Math.Max(1024, sim.Meta.History.Length * 90));
            text.AppendLine("# " + sim.WorldName);
            text.AppendLine();
            text.AppendLine("- " + Loc.F("ui.year_fmt", sim.Clock.Year + 1));
            text.AppendLine("- " + Loc.F("ui.world_seed_fmt", sim.Rng.WorldSeed));
            text.AppendLine();
            text.AppendLine("## " + Loc.T("ui.tab_history"));
            for (int i = 0; i < sim.Meta.History.Length; i++)
                text.Append("- ").AppendLine(HistoryChronicle.Describe(sim, sim.Meta.History[i], i));
            string filename = "world-chronicle-" + sim.Rng.WorldSeed + ".md";
#if UNITY_WEBGL && !UNITY_EDITOR
            PG_DownloadText(filename, text.ToString(), "text/markdown;charset=utf-8");
            toast?.Invoke(Loc.T("ui.world_export_started"));
#else
            string path = Path.Combine(Application.persistentDataPath, filename);
            File.WriteAllText(path, text.ToString(), new UTF8Encoding(true));
            toast?.Invoke(Loc.F("ui.world_export_saved_fmt", path));
#endif
        }

        public static void ExportSummary(SimWorld sim, Action<string> toast)
        {
            string title = sim.WorldName;
            string details = SummaryDetails(sim);
#if UNITY_WEBGL && !UNITY_EDITOR
            PG_DownloadWorldSummary(title, details);
            toast?.Invoke(Loc.T("ui.world_export_started"));
#else
            try
            {
                var image = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
                image.Apply(false);
                string path = Path.Combine(Application.persistentDataPath, "world-summary-" + sim.Rng.WorldSeed + ".png");
                File.WriteAllBytes(path, image.EncodeToPNG());
                UnityEngine.Object.Destroy(image);
                string notePath = Path.ChangeExtension(path, ".md");
                File.WriteAllText(notePath, "# " + title + "\n\n" + details.Replace("\n", "\n- "), new UTF8Encoding(true));
                toast?.Invoke(Loc.F("ui.world_export_saved_fmt", path));
            }
            catch (Exception e) { toast?.Invoke(Loc.F("ui.world_export_failed_fmt", e.Message)); }
#endif
        }

        public static string AchievementMarkdown(ContentDB content, Profile profile)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            if (profile == null) throw new ArgumentNullException(nameof(profile));

            int earned = 0;
            foreach (var achievement in content.Achievements.All)
                if (profile.HasAchievement(achievement.Id)) earned++;

            var text = new StringBuilder(Math.Max(512, content.Achievements.Count * 96));
            text.Append("# ").AppendLine(Loc.T("ui.achievements"));
            text.AppendLine();
            text.AppendLine(Loc.F("ui.achievements_fmt", earned, content.Achievements.Count));
            text.AppendLine();
            foreach (var achievement in content.Achievements.All)
            {
                bool has = profile.HasAchievement(achievement.Id);
                text.Append(has ? "- [x] " : "- [ ] ")
                    .Append(Loc.Name(achievement))
                    .Append(" — ")
                    .AppendLine(achievement.Condition ?? string.Empty);
            }
            return text.ToString();
        }

        public static void ExportAchievements(ContentDB content, Profile profile, Action<string> toast)
        {
            try
            {
                string markdown = AchievementMarkdown(content, profile);
                const string filename = "pixelgenesis-achievements.md";
#if UNITY_WEBGL && !UNITY_EDITOR
                PG_DownloadText(filename, markdown, "text/markdown;charset=utf-8");
                toast?.Invoke(Loc.T("ui.world_export_started"));
#else
                string path = Path.Combine(Application.persistentDataPath, filename);
                File.WriteAllText(path, markdown, new UTF8Encoding(true));
                toast?.Invoke(Loc.F("ui.world_export_saved_fmt", path));
#endif
            }
            catch (Exception e)
            {
                toast?.Invoke(Loc.F("ui.world_export_failed_fmt", e.Message));
            }
        }

        public static string SummaryDetails(SimWorld sim)
        {
            int cities = 0, kingdoms = 0;
            for (int i = 0; i < sim.Civ.CityCount; i++) if (sim.Civ.CityAlive(i)) cities++;
            for (int i = 0; i < sim.Meta.Kingdoms.Count; i++) if (sim.Meta.KingdomAlive(i)) kingdoms++;
            return Loc.F("ui.year_fmt", sim.Clock.Year + 1) + "\n" +
                Loc.F("ui.population_fmt", sim.Units.Store.Count) + "\n" +
                Loc.F("ui.cities") + ": " + cities + "\n" +
                Loc.F("ui.kingdoms_fmt", kingdoms, sim.Meta.Kingdoms.Count - kingdoms) + "\n" +
                Loc.F("ui.wars_total_fmt", sim.Meta.Wars.Count) + "\n" +
                Loc.F("ui.world_seed_fmt", sim.Rng.WorldSeed);
        }
    }
}
