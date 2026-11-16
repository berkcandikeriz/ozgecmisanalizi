using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OzgecmisAnalizi.Business.Options;
using OzgecmisAnalizi.Core.Extensions;

namespace OzgecmisAnalizi.Business.Translation;

/// <summary>
/// LibreTranslate (açık kaynak, ücretsiz) istemcisi: https://github.com/LibreTranslate/LibreTranslate
/// POST /translate, q dizi olarak gönderilir; yanıt translatedText dizisidir.
/// </summary>
public sealed class LibreTranslateClient(HttpClient http, IOptions<TranslationOptions> options, ILogger<LibreTranslateClient> logger)
{
    private readonly TranslationOptions _o = options.Value;

    public bool IsEnabled => _o.LibreTranslateUrl.HasValue();

    /// <summary>Başarısızlıkta null döner; çağıran bir sonraki motora geçer.</summary>
    public async Task<IReadOnlyList<string>?> TranslateAsync(IReadOnlyList<string> texts, string target, CancellationToken ct = default)
    {
        if (!IsEnabled || texts.Count == 0)
        {
            return null;
        }

        try
        {
            var url = _o.LibreTranslateUrl!.TrimEnd('/') + "/translate";
            var body = new Dictionary<string, object?>
            {
                ["q"] = texts,
                ["source"] = "auto",
                ["target"] = target,
                ["format"] = "text"
            };
            if (_o.LibreTranslateApiKey.HasValue())
            {
                body["api_key"] = _o.LibreTranslateApiKey;
            }

            using var response = await http.PostAsJsonAsync(url, body, ct);
            var raw = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("LibreTranslate isteği başarısız: {Status} {Body}", (int)response.StatusCode, raw.Truncate(500));
                return null;
            }

            using var doc = JsonDocument.Parse(raw);
            var translated = doc.RootElement.GetProperty("translatedText");
            if (translated.ValueKind != JsonValueKind.Array || translated.GetArrayLength() != texts.Count)
            {
                return null;
            }

            return translated.EnumerateArray().Select(e => e.GetString() ?? string.Empty).ToList();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            logger.LogWarning(ex, "LibreTranslate'e ulaşılamadı.");
            return null;
        }
    }
}
