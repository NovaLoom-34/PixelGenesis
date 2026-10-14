using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace PG.Governor
{
    // Provider-neutral chat completion: the same shape works for local servers (Ollama, LM Studio)
    // and compatible cloud providers. Pure System.Net — safe off the main thread.
    public sealed class LlmRequest
    {
        public string System, User;
        public float Temperature;
        public int MaxTokens;
        public bool JsonFormat; // response_format: json_object (only when the endpoint supports it)
    }

    public sealed class LlmResponse
    {
        public string Content;
        public int TotalTokens; // 0 when the provider omits usage
    }

    public interface ILlmClient
    {
        LlmResponse Complete(LlmRequest request);
    }

    public interface IAsyncLlmClient
    {
        System.Threading.Tasks.Task<LlmResponse> CompleteAsync(LlmRequest request);
    }

    public interface IJsonFormatClient
    {
        void SetJsonFormat(bool supported);
    }

    public sealed class OpenAiCompatibleClient : ILlmClient, IAsyncLlmClient, IJsonFormatClient
    {
        static readonly System.Net.Http.HttpClient Shared = new System.Net.Http.HttpClient();
        readonly GovernorSettings _settings;
        volatile bool _jsonFormat; // set by the connection test

        public OpenAiCompatibleClient(GovernorSettings settings)
        {
            _settings = settings;
            _jsonFormat = settings.JsonFormatSupported == true;
        }

        // The connection test verdict feeds back into regular calls.
        public void SetJsonFormat(bool supported) => _jsonFormat = supported;

        public LlmResponse Complete(LlmRequest request)
            => CompleteAsync(request).GetAwaiter().GetResult();

        public async System.Threading.Tasks.Task<LlmResponse> CompleteAsync(LlmRequest request)
        {
            var body = new JObject
            {
                ["model"] = _settings.ModelName,
                ["temperature"] = request.Temperature,
                ["max_tokens"] = request.MaxTokens,
                ["messages"] = new JArray
                {
                    new JObject { ["role"] = "system", ["content"] = request.System ?? "" },
                    new JObject { ["role"] = "user", ["content"] = request.User ?? "" },
                },
            };
            if (request.JsonFormat && _jsonFormat)
                body["response_format"] = new JObject { ["type"] = "json_object" };

            using var content = new System.Net.Http.StringContent(body.ToString(Newtonsoft.Json.Formatting.None), Encoding.UTF8, "application/json");
            using var msg = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Post, _settings.BaseUrl + "/chat/completions") { Content = content };
            string key = _settings.ApiKey;
            if (!string.IsNullOrEmpty(key)) msg.Headers.TryAddWithoutValidation("Authorization", "Bearer " + key);
            msg.Headers.TryAddWithoutValidation("Accept", "application/json");

            using var timeout = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(Math.Max(1, _settings.TimeoutSeconds)));
            using var resp = await Shared.SendAsync(msg, timeout.Token).ConfigureAwait(false);
            string text = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException($"LLM HTTP {(int)resp.StatusCode}");

            var obj = JObject.Parse(text);
            string contentText = obj["choices"]?[0]?["message"]?["content"]?.Value<string>() ?? "";
            int tokens = obj["usage"]?["total_tokens"]?.Value<int>() ?? 0;
            return new LlmResponse { Content = contentText, TotalTokens = tokens };
        }

        static string Truncate(string s, int max) => string.IsNullOrEmpty(s) || s.Length <= max ? s : s.Substring(0, max) + "…";
    }

    public sealed class ConnectionResult
    {
        public bool Ok;
        public bool JsonFormatSupported;
        public string Message = "";
    }

    // First-contact probe (Bölüm 10): /v1/chat/completions reachability + whether the endpoint
    // honors response_format: json_object. Unsupported formats fall back to prompt-only JSON,
    // which DecisionJson validates anyway.
    public static class ConnectionTester
    {
        public static ConnectionResult Test(GovernorSettings settings)
        {
                return Task.Run(() => TestAsync(settings, false)).GetAwaiter().GetResult();
        }

        public static async Task<ConnectionResult> TestAsync(GovernorSettings settings, bool web)
        {
            var probe = GovernorClientFactory.Create(settings, web);
            if (probe is IJsonFormatClient format) format.SetJsonFormat(true);
            var req = new LlmRequest
            {
                System = "You are a connection test. Respond only with JSON.",
                User = "Return {\"ok\":true}",
                Temperature = 0f,
                MaxTokens = 32,
                JsonFormat = true,
            };
            try
            {
                await Complete(probe, req);
                if (probe is IJsonFormatClient capable) capable.SetJsonFormat(true);
                return new ConnectionResult { Ok = true, JsonFormatSupported = true, Message = "json_object destekli" };
            }
            catch (Exception e)
            {
                string msg = e.Message;
                // A 4xx that is not auth usually means the optional field was rejected: retry without it.
                if (!msg.Contains("401") && !msg.Contains("403") && msg.Contains("HTTP 4"))
                {
                    try
                    {
                        var plain = GovernorClientFactory.Create(settings, web);
                        if (plain is IJsonFormatClient noJson) noJson.SetJsonFormat(false);
                        await Complete(plain, new LlmRequest { System = req.System, User = req.User, Temperature = 0f, MaxTokens = 32, JsonFormat = false });
                        return new ConnectionResult { Ok = true, JsonFormatSupported = false, Message = "bağlantı var; json_object desteklenmiyor, şemasız mod" };
                    }
                    catch (Exception e2)
                    {
                        msg = e2.Message;
                    }
                }
                return new ConnectionResult { Ok = false, Message = msg };
            }
        }

        static Task<LlmResponse> Complete(ILlmClient client, LlmRequest request) =>
            client is IAsyncLlmClient asyncClient ? asyncClient.CompleteAsync(request) : Task.Run(() => client.Complete(request));
    }
}
