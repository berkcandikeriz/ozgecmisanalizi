using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OzgecmisAnalizi.Business.Options;

namespace OzgecmisAnalizi.Business.Ai;

/// <summary>Google Gemini generateContent API'si. Ücretsiz katman için anahtar: https://aistudio.google.com/apikey</summary>
public sealed class GeminiProvider(HttpClient http, IOptions<AiOptions> options, ILogger<GeminiProvider> logger) : IAiProvider
{
    private readonly AiOptions _o = options.Value;

    public bool IsEnabled => !string.IsNullOrWhiteSpace(_o.ApiKey);
    public string Name => $"Yapay zeka ({_o.Model})";

    public async Task<string?> CompleteAsync(string systemPrompt, string userPrompt, bool jsonOutput, CancellationToken ct = default)
    {
        if (!IsEnabled)
        {
            return null;
        }

        try
        {
            var generationConfig = new JsonObject { ["temperature"] = 0.3 };
            if (jsonOutput)
            {
                generationConfig["responseMimeType"] = "application/json";
            }

            var body = new JsonObject
            {
                ["systemInstruction"] = new JsonObject { ["parts"] = new JsonArray { new JsonObject { ["text"] = systemPrompt } } },
                ["contents"] = new JsonArray
                {
                    new JsonObject { ["role"] = "user", ["parts"] = new JsonArray { new JsonObject { ["text"] = userPrompt } } }
                },
                ["generationConfig"] = generationConfig
            };

            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(_o.Model)}:generateContent";
            using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
            request.Headers.Add("x-goog-api-key", _o.ApiKey);

            using var response = await http.SendAsync(request, ct);
            var raw = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Gemini isteği başarısız: {Status} {Body}", (int)response.StatusCode, raw.Length > 500 ? raw[..500] : raw);
                return null;
            }

            using var doc = JsonDocument.Parse(raw);
            return doc.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            logger.LogWarning(ex, "Gemini'ye ulaşılamadı.");
            return null;
        }
    }
}
