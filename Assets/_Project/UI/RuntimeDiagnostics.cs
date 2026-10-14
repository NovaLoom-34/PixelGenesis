using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using PG.Sim;
using UnityEngine;

namespace PG.UI
{
    // Local-only error capture. Diagnostic exports contain a deliberately small, sanitized settings subset.
    public static class RuntimeDiagnostics
    {
        const int RecentCapacity = 80;
        const int MaxEntryChars = 1800;
        const long MaxLogBytes = 512 * 1024;
        static readonly object Sync = new object();
        static readonly Queue<string> Recent = new Queue<string>(RecentCapacity);
        static string _logPath;
        static bool _initialized;

        // Patterns are lowercase and matched against a lowercase copy: Mono's (?i) misses some all-caps keys
        // (e.g. "API key: x"), so matching folds case and the output keeps the original text.
        static readonly Regex JsonSecretAssignment = new Regex(
            @"([""'](?:api[\s_-]?key|access[\s_-]?token|refresh[\s_-]?token|client[\s_-]?secret|authorization|password|secret(?:[\s_-]?key)?|token)[""']\s*:\s*[""'])([^""']*)([""'])",
            RegexOptions.Compiled);
        static readonly Regex SecretAssignment = new Regex(
            @"(api[\s_-]?key|access[\s_-]?token|refresh[\s_-]?token|client[\s_-]?secret|authorization|password|secret(?:[\s_-]?key)?|token|[?&]key)(\s*[:=]\s*|\s+)([^\s,;&]+)",
            RegexOptions.Compiled);
        static readonly Regex BearerToken = new Regex(@"(?i)\bBearer\s+[A-Za-z0-9._~+/=-]+", RegexOptions.Compiled);
        static readonly Regex ProviderKey = new Regex(@"\b(?:sk|AIza)[-_][A-Za-z0-9_-]{16,}\b", RegexOptions.Compiled);
        static readonly Regex UrlCredential = new Regex(@"(?i)(https?://)[^/@\s:]+:[^/@\s]+@", RegexOptions.Compiled);
        static readonly Regex Email = new Regex(@"\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        static readonly Regex WindowsUserPath = new Regex(@"(?i)([A-Z]:\\Users\\)[^\\]+", RegexOptions.Compiled);
        static readonly Regex UnixUserPath = new Regex(@"(?i)(/Users/|/home/)[^/\s]+", RegexOptions.Compiled);

        public static string LogPath => _logPath;

        public static void Initialize()
        {
            lock (Sync)
            {
                if (_initialized) return;
                _initialized = true;
                _logPath = Path.Combine(Application.persistentDataPath, "diagnostics.log");
                LoadLocal();
                Application.logMessageReceivedThreaded += OnUnityLog;
#if !UNITY_WEBGL || UNITY_EDITOR
                AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
#endif
                System.Threading.Tasks.TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            }
        }

        static void OnUnityLog(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Assert && type != LogType.Exception) return;
            Record(type.ToString().ToUpperInvariant(), condition, stackTrace);
        }

        static void OnUnhandledException(object sender, UnhandledExceptionEventArgs args)
        {
            Record("UNHANDLED", args.ExceptionObject?.ToString() ?? "Unknown unhandled exception", string.Empty);
        }

        static void OnUnobservedTaskException(object sender, System.Threading.Tasks.UnobservedTaskExceptionEventArgs args)
        {
            Record("UNOBSERVED_TASK", args.Exception?.ToString() ?? "Unknown task exception", string.Empty);
        }

        static void Record(string type, string message, string stackTrace)
        {
            string entry = DateTime.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture) +
                " [" + type + "] " + Sanitize(message) +
                (string.IsNullOrWhiteSpace(stackTrace) ? string.Empty : "\n" + Sanitize(stackTrace));
            if (entry.Length > MaxEntryChars) entry = entry.Substring(0, MaxEntryChars) + "…";

            lock (Sync)
            {
                if (Recent.Count == RecentCapacity) Recent.Dequeue();
                Recent.Enqueue(entry);
                AppendLocal(entry);
            }
        }

        static void AppendLocal(string entry)
        {
            if (string.IsNullOrEmpty(_logPath)) return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_logPath));
                string oneLine = entry.Replace("\r", " ").Replace("\n", " | ");
                int appendBytes = Encoding.UTF8.GetByteCount(oneLine) + Encoding.UTF8.GetByteCount(Environment.NewLine);
                if (File.Exists(_logPath) && new FileInfo(_logPath).Length + appendBytes > MaxLogBytes)
                {
                    int tailLength;
                    byte[] tail;
                    using (var stream = File.OpenRead(_logPath))
                    {
                        long tailStart = Math.Max(0, stream.Length - MaxLogBytes);
                        stream.Position = tailStart;
                        tailLength = (int)(stream.Length - tailStart);
                        tail = new byte[tailLength];
                        int read = 0;
                        while (read < tailLength)
                        {
                            int count = stream.Read(tail, read, tailLength - read);
                            if (count <= 0) break;
                            read += count;
                        }
                        tailLength = read;
                    }

                    int keepBudget = (int)Math.Max(0, MaxLogBytes - appendBytes);
                    int start = Math.Max(0, tailLength - keepBudget);
                    while (start < tailLength && tail[start] != (byte)'\n') start++;
                    if (start < tailLength) start++;
                    else start = tailLength;

                    var retained = new byte[tailLength - start];
                    if (retained.Length > 0) Buffer.BlockCopy(tail, start, retained, 0, retained.Length);
                    File.WriteAllBytes(_logPath, retained);
                }
                File.AppendAllText(_logPath, oneLine + Environment.NewLine, new UTF8Encoding(false));
            }
            catch
            {
                // Logging must never throw back into Unity's log callback.
            }
        }

        static void LoadLocal()
        {
            try
            {
                if (!File.Exists(_logPath)) return;
                string[] lines = File.ReadAllLines(_logPath);
                int first = Math.Max(0, lines.Length - RecentCapacity);
                for (int i = first; i < lines.Length; i++)
                {
                    if (string.IsNullOrWhiteSpace(lines[i])) continue;
                    if (Recent.Count == RecentCapacity) Recent.Dequeue();
                    Recent.Enqueue(Sanitize(lines[i]));
                }
            }
            catch
            {
                // A missing or unreadable diagnostic file must not block game startup.
            }
        }

        public static string BuildBundle(SimWorld sim, Profile profile)
        {
            if (sim == null) throw new ArgumentNullException(nameof(sim));
            var text = new StringBuilder(4096);
            text.AppendLine("PixelGenesis local diagnostics");
            text.Append("App version: ").AppendLine(Application.version);
            text.Append("Unity version: ").AppendLine(Application.unityVersion);
            text.Append("Platform: ").AppendLine(Application.platform.ToString());
            text.Append("World seed: ").AppendLine(sim.Seed.ToString(System.Globalization.CultureInfo.InvariantCulture));
            text.Append("Year: ").AppendLine((sim.Clock.Year + 1).ToString(System.Globalization.CultureInfo.InvariantCulture));
            text.Append("Population: ").AppendLine(sim.Population.ToString(System.Globalization.CultureInfo.InvariantCulture));
            text.AppendLine("Telemetry: disabled; this package is copied locally only.");
            text.AppendLine("Settings:");
            text.Append("- language: ").AppendLine(Safe(profile?.Language, "tr", "en"));
            text.Append("- performance preset: ").AppendLine(Safe(profile?.PerformancePreset, "balanced", "low", "high"));
            text.Append("- target fps: ").AppendLine(profile == null ? "60" : Mathf.Clamp(profile.TargetFps, 30, 120).ToString(System.Globalization.CultureInfo.InvariantCulture));
            text.Append("- UI scale: ").AppendLine(profile == null ? "1" : Mathf.Clamp(profile.UiScale, 0.75f, 2f).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
            text.Append("- high contrast: ").AppendLine(profile != null && profile.HighContrast ? "on" : "off");
            text.Append("- day/night visuals: ").AppendLine(profile != null && profile.DayNight ? "on" : "off");
            text.AppendLine("Recent errors (latest first):");

            lock (Sync)
            {
                if (Recent.Count == 0) text.AppendLine("(none in this session)");
                else
                {
                    var items = Recent.ToArray();
                    for (int i = items.Length - 1; i >= 0; i--) text.AppendLine(Sanitize(items[i]));
                }
            }
            return text.ToString();
        }

        static string Safe(string value, params string[] allowed)
        {
            if (!string.IsNullOrEmpty(value))
                for (int i = 0; i < allowed.Length; i++)
                    if (string.Equals(value, allowed[i], StringComparison.Ordinal)) return value;
            return allowed.Length == 0 ? "unknown" : allowed[0];
        }

        static string Keep(string source, Group group) => source.Substring(group.Index, group.Length);

        static string ReplaceFolded(Regex pattern, string value, Func<Match, string, string> render)
        {
            string folded = value.ToLowerInvariant();
            StringBuilder sb = null;
            int last = 0;
            for (Match m = pattern.Match(folded); m.Success; m = m.NextMatch())
            {
                if (sb == null) sb = new StringBuilder(value.Length);
                sb.Append(value, last, m.Index - last);
                sb.Append(render(m, value));
                last = m.Index + m.Length;
            }
            if (sb == null) return value;
            sb.Append(value, last, value.Length - last);
            return sb.ToString();
        }

        public static string Sanitize(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            string clean = ReplaceFolded(JsonSecretAssignment, value,
                (m, v) => Keep(v, m.Groups[1]) + "[REDACTED]" + Keep(v, m.Groups[3]));
            clean = ReplaceFolded(SecretAssignment, clean, (m, v) => Keep(v, m.Groups[1]) + "=[REDACTED]");
            clean = BearerToken.Replace(clean, "Bearer [REDACTED]");
            clean = ProviderKey.Replace(clean, "[REDACTED_KEY]");
            clean = UrlCredential.Replace(clean, "$1[REDACTED]@");
            clean = Email.Replace(clean, "[EMAIL]");
            clean = WindowsUserPath.Replace(clean, "$1[USER]");
            clean = UnixUserPath.Replace(clean, "$1[USER]");
            return clean;
        }
    }
}
