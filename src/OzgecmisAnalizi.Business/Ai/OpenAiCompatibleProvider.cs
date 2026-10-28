using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OzgecmisAnalizi.Business.Options;

namespace OzgecmisAnalizi.Business.Ai;

/// <summary>
/// OpenAI Chat Completions uyumlu her sağlayıcıyla çalışır: OpenAI, Groq, OpenRouter, Together,
/// yerel Ollama (http://localhost:11434/v1) veya LM Studio.
/// </summary>
public sealed class OpenAiCompatibleProvider(HttpClient http, IOptions<AiOptions> options, ILogger<OpenAiCompatibleProvider> logger) : IAiProvider
{
    private readonly AiOptions _o = options.Value;

    public bool IsEnabled => true;
    public string Name => $"Yapay zeka ({_o.Model})";

    public async Task<string?> CompleteAsync(string systemPrompt, string userPrompt, bool jsonOutput, CancellationToken ct = default)
    {
        try
        {
            var body = new JsonObject
            {
                ["model"] = _o.Model,
                ["temperature"] = 0.3,
                ["messages"] = new JsonArray
                {
                    new JsonObject { ["role"] = "system", ["content"] = systemPrompt },
                    new JsonObject { ["role"] = "user", ["content"] = userPrompt }
                }
            };

            if (jsonOutput)
            {
                body["response_format"] = new JsonObject { ["type"] = "json_object" };
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, _o.BaseUrl.TrimEnd('/') + "/chat/completions")
            {
                Content = JsonContent.Create(body)
            };

            if (!string.IsNullOrWhiteSpace(_o.ApiKey))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _o.ApiKey);
            }

            using var response = await http.SendAsync(request, ct);
            var raw = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Yapay zeka isteği başarısız: {Status} {Body}", (int)response.StatusCode, raw.Length > 500 ? raw[..500] : raw);
                return null;
            }

            using var doc = JsonDocument.Parse(raw);
            return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            logger.LogWarning(ex, "Yapay zeka sağlayıcısına ulaşılamadı.");
            return null;
        }
    }
}
