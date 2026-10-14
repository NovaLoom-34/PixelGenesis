using System;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace PG.Governor
{
    public static class GovernorClientFactory
    {
        public static ILlmClient Create(GovernorSettings settings, bool web) =>
            web ? (ILlmClient)new WebGlLlmClient(settings) : new OpenAiCompatibleClient(settings);
    }

    // WebGL runs in the browser's single-threaded runtime; use UnityWebRequest, never HttpClient/Task.Run.
    public sealed class WebGlLlmClient : ILlmClient, IAsyncLlmClient, IJsonFormatClient
    {
        readonly GovernorSettings _settings;
        bool _jsonFormat;
        public WebGlLlmClient(GovernorSettings settings) { _settings = settings; _jsonFormat = settings.JsonFormatSupported == true; }
        public void SetJsonFormat(bool supported) => _jsonFormat = supported;
        public LlmResponse Complete(LlmRequest request) => throw new InvalidOperationException("WebGL AI calls must be asynchronous.");

        public async Task<LlmResponse> CompleteAsync(LlmRequest request)
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
            if (request.JsonFormat && _jsonFormat) body["response_format"] = new JObject { ["type"] = "json_object" };
            var upload = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(body.ToString(Newtonsoft.Json.Formatting.None)));
            upload.contentType = "application/json";
            var req = new UnityWebRequest(_settings.BaseUrl + "/chat/completions", UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = upload,
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = Math.Max(1, _settings.TimeoutSeconds),
            };
            req.SetRequestHeader("Accept", "application/json");
            if (!string.IsNullOrEmpty(_settings.ApiKey)) req.SetRequestHeader("Authorization", "Bearer " + _settings.ApiKey);
            try
            {
                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Yield();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    string reason = req.responseCode > 0 ? "LLM HTTP " + req.responseCode : "CORS veya ağ hatası; sağlayıcıda oyun adresine izin verin";
                    throw new InvalidOperationException(reason);
                }
                var obj = JObject.Parse(req.downloadHandler.text);
                return new LlmResponse
                {
                    Content = obj["choices"]?[0]?["message"]?["content"]?.Value<string>() ?? "",
                    TotalTokens = obj["usage"]?["total_tokens"]?.Value<int>() ?? 0,
                };
            }
            finally { req.Dispose(); }
        }
    }
}
