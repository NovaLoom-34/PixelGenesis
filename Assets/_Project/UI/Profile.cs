using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PG.UI
{
    // Bölüm 8.9 profile.json (outside any world): discoveries, achievements, counters, favourite powers, settings.
    [Serializable]
    public sealed class Profile
    {
        public List<string> Discovered = new List<string>();   // trait/gene/species/quality/plot ids
        public List<string> Achievements = new List<string>();
        public List<string> FavoritePowers = new List<string>();
        public List<string> RecentPowers = new List<string>();
        public List<string> WatchedKingdoms = new List<string>();
        public List<string> Counters = new List<string>();     // "key=value"
        public string Language = "tr";
        public float UiScale = 1f;
        public bool EdgePan = true;
        public bool TutorialDone;
        public List<string> MutedNotifications = new List<string>();
        public int HistoryNotificationThreshold = HistoryNotificationPolicy.DefaultThreshold;
        public bool DocumentaryCameraMode;
        public float[] Volumes = { 1f, 1f, 1f, 0.7f, 0.6f }; // master, sfx, ui, ambient, music (Bölüm 9.1)
        public int TargetFps = 60;
        public string PerformancePreset = "balanced";
        public bool DayNight = true; // Bölüm 11 G4: render-only day/night light cycle
        public bool DisableScreenShake; // accessibility: absent in older profiles means enabled
        public bool ColorBlindPalette;
        public bool HighContrast;
        public string LastSeenVersion = "";

        [NonSerialized] public bool Dirty;
        [NonSerialized] readonly HashSet<string> _discovered = new HashSet<string>();
        [NonSerialized] readonly HashSet<string> _achievements = new HashSet<string>();

        public static string PathOnDisk => System.IO.Path.Combine(Application.persistentDataPath, "profile.json");

        public static Profile Load()
        {
            // Bölüm 9.2: a broken profile.json falls back to profile.bak (the previous good file).
            var p = Read(PathOnDisk) ?? Read(BackupPath) ?? new Profile();
            // Older JSON profiles do not contain this preference and deserialize it as zero.
            if (p.HistoryNotificationThreshold == 0)
                p.HistoryNotificationThreshold = HistoryNotificationPolicy.DefaultThreshold;
            p.HistoryNotificationThreshold = HistoryNotificationPolicy.NormalizeThreshold(p.HistoryNotificationThreshold);
            if (p.PerformancePreset != "low" && p.PerformancePreset != "balanced" && p.PerformancePreset != "high")
                p.PerformancePreset = "balanced";
            if (p.MutedNotifications == null) p.MutedNotifications = new List<string>();
            if (p.Discovered == null) p.Discovered = new List<string>();
            if (p.Achievements == null) p.Achievements = new List<string>();
            if (p.FavoritePowers == null) p.FavoritePowers = new List<string>();
            if (p.RecentPowers == null) p.RecentPowers = new List<string>();
            if (p.WatchedKingdoms == null) p.WatchedKingdoms = new List<string>();
            foreach (var d in p.Discovered) p._discovered.Add(d);
            foreach (var a in p.Achievements) p._achievements.Add(a);
            return p;
        }

        static string BackupPath => System.IO.Path.ChangeExtension(PathOnDisk, ".bak");

        static Profile Read(string path)
        {
            try
            {
                return File.Exists(path) ? JsonUtility.FromJson<Profile>(File.ReadAllText(path)) : null;
            }
            catch (Exception e)
            {
                Debug.LogWarning(path + " unreadable: " + e.Message);
                return null;
            }
        }

        public void Save()
        {
            if (!Dirty) return;
            try
            {
                if (Read(PathOnDisk) != null) File.Copy(PathOnDisk, BackupPath, true); // never back up a broken file
                File.WriteAllText(PathOnDisk, JsonUtility.ToJson(this, true));
                Dirty = false;
            }
            catch (Exception e)
            {
                Debug.LogWarning("profile.json not saved: " + e.Message);
            }
        }

        public bool IsDiscovered(string id) => _discovered.Contains(id);

        public bool Discover(string id)
        {
            if (!_discovered.Add(id)) return false;
            Discovered.Add(id);
            Dirty = true;
            return true;
        }

        public bool HasAchievement(string id) => _achievements.Contains(id);

        public bool Earn(string id)
        {
            if (!_achievements.Add(id)) return false;
            Achievements.Add(id);
            Dirty = true;
            return true;
        }

        public int Counter(string key)
        {
            foreach (var c in Counters)
                if (c.StartsWith(key + "=", StringComparison.Ordinal) && int.TryParse(c.Substring(key.Length + 1), out int v)) return v;
            return 0;
        }

        public int AddCounter(string key, int amount)
        {
            int v = Counter(key) + amount;
            Counters.RemoveAll(c => c.StartsWith(key + "=", StringComparison.Ordinal));
            Counters.Add(key + "=" + v);
            Dirty = true;
            return v;
        }

        public bool ToggleFavorite(string power)
        {
            Dirty = true;
            if (FavoritePowers.Remove(power)) return false;
            if (FavoritePowers.Count >= 20) FavoritePowers.RemoveAt(0);
            FavoritePowers.Add(power);
            return true;
        }

        public bool RememberPower(string power)
        {
            if (string.IsNullOrWhiteSpace(power)) return false;
            if (RecentPowers.Count > 0 && RecentPowers[0] == power) return false;
            RecentPowers.Remove(power);
            RecentPowers.Insert(0, power);
            if (RecentPowers.Count > 6) RecentPowers.RemoveAt(RecentPowers.Count - 1);
            Dirty = true;
            return true;
        }

        public const int MaxWatchedKingdoms = 20;

        static string KingdomWatchKey(ulong seed, int width, int height, long kingdomUid) =>
            string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:X16}:{1}x{2}:{3}", seed, width, height, kingdomUid);

        public bool IsKingdomWatched(ulong seed, int width, int height, long kingdomUid) =>
            kingdomUid != 0 && WatchedKingdoms.Contains(KingdomWatchKey(seed, width, height, kingdomUid));

        public bool WatchesKingdomEvent(ulong seed, int width, int height, long a, long b, long c) =>
            IsKingdomWatched(seed, width, height, a) || IsKingdomWatched(seed, width, height, b)
            || IsKingdomWatched(seed, width, height, c);

        public bool ToggleKingdomWatch(ulong seed, int width, int height, long kingdomUid)
        {
            if (kingdomUid == 0) return false;
            string key = KingdomWatchKey(seed, width, height, kingdomUid);
            if (WatchedKingdoms.Remove(key))
            {
                Dirty = true;
                return false;
            }
            if (WatchedKingdoms.Count >= MaxWatchedKingdoms) WatchedKingdoms.RemoveAt(0);
            WatchedKingdoms.Add(key);
            Dirty = true;
            return true;
        }

        public void ResetSettings()
        {
            Language = "tr";
            UiScale = 1f;
            EdgePan = true;
            TutorialDone = false;
            if (MutedNotifications == null) MutedNotifications = new List<string>();
            else MutedNotifications.Clear();
            HistoryNotificationThreshold = HistoryNotificationPolicy.DefaultThreshold;
            DocumentaryCameraMode = false;
            Volumes = new[] { 1f, 1f, 1f, 0.7f, 0.6f };
            TargetFps = 60;
            PerformancePreset = "balanced";
            DayNight = true;
            DisableScreenShake = false;
            ColorBlindPalette = false;
            HighContrast = false;
            Dirty = true;
        }
    }
}
