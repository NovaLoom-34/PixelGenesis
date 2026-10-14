using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using UnityEngine;

namespace PG.Governor
{
    // OpenAI-compatible endpoint so the same client drives a local server (Ollama, LM Studio)
    // or any compatible cloud provider: base URL + model + optional key.
    public sealed class GovernorSettings
    {
        public enum ScopeKind : byte { All, Controlled, TopN }

        public bool Enabled; // default off: the feature only calls out when explicitly turned on
        public ScopeKind Scope = ScopeKind.All;
        public int TopN = 5;
        public bool UseLocal;
        public string CloudBaseUrl = "https://api.z.ai/api/paas/v4";
        public string LocalBaseUrl = "http://localhost:11434/v1";
        public string CloudModel = "glm-4-flash";
        public string LocalModel = "llama3.1";
        [JsonIgnore] public string CloudApiKey = "", LocalApiKey = "";
        public bool CloudExternalKey = true, LocalExternalKey = true;
        [JsonIgnore] public string ApiKey => (UseLocal ? LocalExternalKey : CloudExternalKey)
            ? LoadApiKey() : (UseLocal ? LocalApiKey : CloudApiKey);
        public float Temperature = 0.4f;
        public int MaxTokens = 900;
        public int TimeoutSeconds = 25;
        public int MonthlyTokenBudget = 150000;
        public int MaxConcurrent = 2;        // total in-flight decisions (spec: 2)
        public int CooldownSeconds = 90;     // per kingdom, real time (spec: 90)
        public int MaxCallsPerHour = 120;    // model calls, all kingdoms (spec: 120)
        public bool? JsonFormatSupported;    // probed by the connection test; null = not probed yet

        public string BaseUrl => (UseLocal ? LocalBaseUrl : CloudBaseUrl)?.Trim().TrimEnd('/') ?? "";
        public string ModelName => UseLocal ? LocalModel : CloudModel;

        static string Path => System.IO.Path.Combine(Application.persistentDataPath, "governor.json");

        public bool ApplyConnection(bool local, string url, string model, string key, bool externalKey)
        {
            url = (url ?? "").Trim().TrimEnd('/');
            const string suffix = "/chat/completions";
            if (url.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) url = url.Substring(0, url.Length - suffix.Length);
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != "https" && uri.Scheme != "http") || string.IsNullOrEmpty(uri.Host) ||
                !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) ||
                string.IsNullOrWhiteSpace(model)) return false;
            if (local) { LocalBaseUrl = url; LocalModel = model.Trim(); LocalApiKey = (key ?? "").Trim(); LocalExternalKey = externalKey; }
            else { CloudBaseUrl = url; CloudModel = model.Trim(); CloudApiKey = (key ?? "").Trim(); CloudExternalKey = externalKey; }
            UseLocal = local;
            JsonFormatSupported = null;
            return true;
        }

        public static GovernorSettings Load()
        {
            try
            {
                if (System.IO.File.Exists(Path))
                    return JsonConvert.DeserializeObject<GovernorSettings>(System.IO.File.ReadAllText(Path)) ?? new GovernorSettings();
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[Governor] settings unreadable, defaults used: {e.Message}");
            }
            return new GovernorSettings();
        }

        public void Save()
        {
            try
            {
                System.IO.File.WriteAllText(Path, JsonConvert.SerializeObject(this, Formatting.Indented));
#if UNITY_WEBGL && !UNITY_EDITOR
                PGSyncGovernorSettings();
#endif
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[Governor] settings not saved: {e.Message}");
            }
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern void PGSyncGovernorSettings();
#endif

        // Key never lives in the settings file: env var first, then %USERPROFILE%\.pixelgenesis\governor_key.
        public static string LoadApiKey()
        {
            string env = Environment.GetEnvironmentVariable("PG_GOVERNOR_KEY");
            if (!string.IsNullOrEmpty(env)) return env.Trim();
            try
            {
                string file = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".pixelgenesis", "governor_key");
                if (System.IO.File.Exists(file)) return System.IO.File.ReadAllText(file).Trim();
            }
            catch { /* unreadable home dir: treat as no key */ }
            return "";
        }
    }
}
