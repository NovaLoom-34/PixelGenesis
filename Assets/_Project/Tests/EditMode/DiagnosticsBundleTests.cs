using System.IO;
using System.Reflection;
using NUnit.Framework;
using PG.Content;
using PG.EditorTools;
using PG.Sim;
using PG.UI;
using PG.World;
using UnityEngine;

namespace PG.Tests.EditMode
{
    public sealed class DiagnosticsBundleTests
    {
        ContentDB _db;

        [OneTimeSetUp]
        public void LoadContent()
        {
            DataSync.Sync();
            _db = ContentDB.LoadAll(Path.Combine(Application.streamingAssetsPath, "Data"), null);
        }

        [Test]
        public void SanitizerRedactsCredentialsContactsAndUserHomePaths()
        {
            string input = "api_key=sk-example-secret API key: spaced-key-marker Bearer eyJhbGciOiJtest sk-proj-1234567890123456 user@example.com C:\\Users\\alice\\game\\save {\"apiKey\":\"json-key-marker\",\"api key\":\"json-spaced-key-marker\",\"client secret\":\"json-client-secret-marker\",\"password\":\"json-password-marker\"}";
            string clean = RuntimeDiagnostics.Sanitize(input);

            StringAssert.DoesNotContain("sk-example-secret", clean);
            StringAssert.DoesNotContain("spaced-key-marker", clean);
            StringAssert.DoesNotContain("eyJhbGciOiJtest", clean);
            StringAssert.DoesNotContain("sk-proj-1234567890123456", clean);
            StringAssert.DoesNotContain("user@example.com", clean);
            StringAssert.DoesNotContain("C:\\Users\\alice", clean);
            StringAssert.DoesNotContain("json-key-marker", clean);
            StringAssert.DoesNotContain("json-spaced-key-marker", clean);
            StringAssert.DoesNotContain("json-client-secret-marker", clean);
            StringAssert.DoesNotContain("json-password-marker", clean);
            StringAssert.Contains("\"apiKey\":\"[REDACTED]\"", clean);
            StringAssert.Contains("\"password\":\"[REDACTED]\"", clean);
            StringAssert.Contains("[REDACTED]", clean);
            StringAssert.Contains("[EMAIL]", clean);
            StringAssert.Contains("[USER]", clean);
        }

        [Test]
        public void BundleExportsOnlyAllowlistedProfileSettings()
        {
            using (var sim = new SimWorld(_db, new WorldMap(64, 64, _db), new PG.Core.SimRandomProvider(62)))
            {
                var profile = new Profile
                {
                    Language = "private-language-value",
                    PerformancePreset = "private-preset-value",
                    TargetFps = 999,
                    UiScale = 9f,
                };
                profile.Discovered.Add("sk_private_discovery_marker");
                profile.Achievements.Add("C:\\Users\\alice\\private-achievement");
                profile.FavoritePowers.Add("private-favorite-marker");
                profile.RecentPowers.Add("private-recent-marker");
                profile.WatchedKingdoms.Add("private-kingdom-marker");
                profile.Counters.Add("private-counter-marker=123");

                string bundle = RuntimeDiagnostics.BuildBundle(sim, profile);

                StringAssert.Contains("- language: tr", bundle);
                StringAssert.Contains("- performance preset: balanced", bundle);
                StringAssert.Contains("- target fps: 120", bundle);
                StringAssert.Contains("- UI scale: 2.00", bundle);
                StringAssert.DoesNotContain("private-language-value", bundle);
                StringAssert.DoesNotContain("private-preset-value", bundle);
                StringAssert.DoesNotContain("sk_private_discovery_marker", bundle);
                StringAssert.DoesNotContain("private-achievement", bundle);
                StringAssert.DoesNotContain("private-favorite-marker", bundle);
                StringAssert.DoesNotContain("private-recent-marker", bundle);
                StringAssert.DoesNotContain("private-kingdom-marker", bundle);
                StringAssert.DoesNotContain("private-counter-marker", bundle);
                StringAssert.DoesNotContain("C:\\Users\\alice", bundle);
            }
        }

        [Test]
        public void DiagnosticLogRolloverKeepsTheUtf8FileWithinItsByteLimit()
        {
            const long maxBytes = 512 * 1024;
            var path = Path.Combine(Path.GetTempPath(), System.Guid.NewGuid().ToString("N") + ".diagnostics.log");
            var pathField = typeof(RuntimeDiagnostics).GetField("_logPath", BindingFlags.NonPublic | BindingFlags.Static);
            var append = typeof(RuntimeDiagnostics).GetMethod("AppendLocal", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(pathField, Is.Not.Null);
            Assert.That(append, Is.Not.Null);
            string previousPath = (string)pathField.GetValue(null);
            pathField.SetValue(null, path);
            try
            {
                for (int i = 0; i < 120; i++)
                    append.Invoke(null, new object[] { new string('界', 1800) + " tail-marker-" + i });

                Assert.That(new FileInfo(path).Length, Is.LessThanOrEqualTo(maxBytes));
                string[] lines = File.ReadAllLines(path, System.Text.Encoding.UTF8);
                Assert.That(lines.Length, Is.GreaterThan(0));
                StringAssert.Contains("tail-marker-119", lines[lines.Length - 1]);
                StringAssert.Contains("界", lines[lines.Length - 1]);
            }
            finally
            {
                pathField.SetValue(null, previousPath);
                if (File.Exists(path)) File.Delete(path);
            }
        }
    }
}
