using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using PG.Governor;
using UnityEngine;

namespace PG.Tests.EditMode
{
    public sealed class GovernorSettingsInputTests
    {
        [Test]
        public void CustomProfileValuesAreAppliedButSessionKeyIsNeverSerialized()
        {
            var settings = new GovernorSettings();
            Assert.IsFalse(settings.ApplyConnection(false, "not a URL", "new-model", "secret-test-value", false));
            Assert.AreEqual("https://api.z.ai/api/paas/v4", settings.CloudBaseUrl, "Invalid edits must not partially apply");
            Assert.IsTrue(settings.ApplyConnection(false, "https://example.test/v1/chat/completions", "chosen-model", "secret-test-value", false));
            Assert.AreEqual("https://example.test/v1", settings.BaseUrl);
            Assert.AreEqual("chosen-model", settings.ModelName);
            Assert.AreEqual("secret-test-value", settings.ApiKey);
            string saved = Newtonsoft.Json.JsonConvert.SerializeObject(settings);
            StringAssert.DoesNotContain("secret-test-value", saved);
            Assert.IsTrue(settings.ApplyConnection(true, "http://localhost:11434/v1", "local-model", "local-secret", false));
            Assert.AreEqual("local-secret", settings.ApiKey);
            StringAssert.DoesNotContain("local-secret", Newtonsoft.Json.JsonConvert.SerializeObject(settings));
        }
    }

    // Fake local HTTP endpoint tests (Bölüm 10 verification): the OpenAI-compatible client
    // against a local listener — success, broken JSON, timeout and access error. The governor
    // treats every failure the same way: the decision fails, the game keeps flowing.
    public sealed class GovernorHttpTests
    {
        HttpListener _listener;
        string _baseUrl;
        Action<HttpListenerContext> _respond;

        [SetUp]
        public void StartListener()
        {
            int port = UnityEngine.Random.Range(21000, 29000);
            _baseUrl = $"http://localhost:{port}/v1";
            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://localhost:{port}/");
                _listener.Start();
            }
            catch (Exception e)
            {
                Assert.Ignore($"HttpListener unavailable on this machine ({e.Message})");
            }
            Task.Run(ListenLoop);
        }

        [TearDown]
        public void StopListener()
        {
            try { _listener?.Stop(); _listener?.Close(); } catch { /* already gone */ }
            _listener = null;
        }

        async Task ListenLoop()
        {
            while (_listener != null && _listener.IsListening)
            {
                HttpListenerContext ctx;
                try { ctx = await _listener.GetContextAsync(); }
                catch { return; }
                try { _respond?.Invoke(ctx); }
                catch { try { ctx.Response.StatusCode = 500; ctx.Response.Close(); } catch { /* closed */ } }
            }
        }

        GovernorSettings Settings() => new GovernorSettings
        {
            Enabled = true,
            UseLocal = false,
            CloudBaseUrl = _baseUrl,
            CloudModel = "fake-model",
            CloudExternalKey = false,
            TimeoutSeconds = 3,
        };

        static void Reply(HttpListenerContext ctx, int status, string body)
        {
            var bytes = Encoding.UTF8.GetBytes(body);
            ctx.Response.StatusCode = status;
            ctx.Response.ContentType = "application/json";
            ctx.Response.ContentLength64 = bytes.Length;
            ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
            ctx.Response.OutputStream.Close();
        }

        static string Completion(string content) => new Newtonsoft.Json.Linq.JObject
        {
            ["choices"] = new Newtonsoft.Json.Linq.JArray
            {
                new Newtonsoft.Json.Linq.JObject { ["message"] = new Newtonsoft.Json.Linq.JObject { ["content"] = content } },
            },
            ["usage"] = new Newtonsoft.Json.Linq.JObject { ["total_tokens"] = 42 },
        }.ToString();

        [Test]
        public void SuccessResponseIsParsed()
        {
            _respond = ctx => Reply(ctx, 200, Completion("{\"thought\":\"ok\",\"actions\":[{\"kind\":\"Wait\"}]}"));
            var brain = new GovernorBrain(new OpenAiCompatibleClient(Settings()), Settings());
            var decision = brain.Decide(new KingdomSnapshot { KingdomIndex = 1, MaxAdvisors = 3 });
            Assert.That(decision.Thought, Is.EqualTo("ok"));
            Assert.That(decision.Actions.Count, Is.EqualTo(1));
            Assert.That(brain.TokensUsed, Is.EqualTo(42));
        }

        [Test]
        public void BrokenJsonBodyFailsAndIsRecoverable()
        {
            _respond = ctx => Reply(ctx, 200, "this is not json at all");
            var client = new OpenAiCompatibleClient(Settings());
            Assert.Catch<Exception>(() => client.Complete(new LlmRequest { MaxTokens = 16 }),
                "an unreadable body must surface as a failed call, never as a crash");
        }

        [Test]
        public void TimeoutSurfacesAsFailure()
        {
            _respond = ctx =>
            {
                Thread.Sleep(5000); // longer than the 3s client timeout
                Reply(ctx, 200, "{}");
            };
            var client = new OpenAiCompatibleClient(Settings());
            var start = DateTime.UtcNow;
            Assert.Catch<Exception>(() => client.Complete(new LlmRequest { MaxTokens = 16 }));
            Assert.That((DateTime.UtcNow - start).TotalSeconds, Is.LessThan(8), "the timeout must cut the wait short");
        }

        [Test]
        public void AccessDeniedSurfacesAsFailure()
        {
            _respond = ctx => Reply(ctx, 401, "{\"error\":\"bad key\"}");
            var client = new OpenAiCompatibleClient(Settings());
            var ex = Assert.Catch<Exception>(() => client.Complete(new LlmRequest { MaxTokens = 16 }));
            Assert.That(ex.Message, Does.Contain("401"));
        }

        [Test]
        public void FailingEndpointKeepsTheGameFlowing()
        {
            // The brain always fails; the runner records consecutive failures and never blocks.
            _respond = ctx => Reply(ctx, 500, "boom");
            var settings = Settings();
            settings.JsonFormatSupported = false; // skip the probe path
            var brain = new GovernorBrain(new OpenAiCompatibleClient(settings), settings);
            var runner = new GovernorRunner(settings, brain);
            var db = GovernorTestsDb.Load();
            var sim = new PG.Sim.SimWorld(db, new PG.World.WorldMap(64, 64, db), new PG.Core.SimRandomProvider(5));
            try
            {
                runner.Bind(sim);
                runner.ScheduleYear();
                var queue = new PG.Powers.EditCommandQueue();
                for (int i = 0; i < 400 && runner.InFlight > 0; i++) runner.Update(queue);
                runner.Update(queue);
                Assert.That(runner.Log.Entries.Count, Is.EqualTo(0), "failed decisions are not logged as applied");
                Assert.That(runner.Status, Is.EqualTo(GovernorConnectionStatus.Unknown), "the decision failure is not a connection verdict");
            }
            finally
            {
                sim.Dispose();
            }
        }
    }

    // Shared one-shot content db for the HTTP tests (listener tests do not need kingdoms).
    public static class GovernorTestsDb
    {
        static PG.Content.ContentDB _db;
        public static PG.Content.ContentDB Load()
        {
            if (_db == null)
            {
                PG.EditorTools.DataSync.Sync();
                _db = PG.Content.ContentDB.LoadAll(System.IO.Path.Combine(Application.streamingAssetsPath, "Data"), null);
            }
            return _db;
        }
    }
}
