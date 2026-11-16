using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using OzgecmisAnalizi.Business.Ai;
using OzgecmisAnalizi.Business.Options;
using OzgecmisAnalizi.Core.Extensions;
using OzgecmisAnalizi.Entities.Content;

namespace OzgecmisAnalizi.Business.Translation;

/// <param name="Content">Çevrilmiş içerik (kaynağın kopyası; kaynak değişmez).</param>
/// <param name="Complete">Tüm metin parçaları bir çeviri motorundan ya da sözlükten geçtiyse true.</param>
public sealed record TranslationOutcome(ResumeContent Content, string Language, string Engine, bool Complete, int TranslatedCount, int TotalCount);

public interface IResumeTranslator
{
    /// <summary>Özet, deneyim maddeleri gibi serbest metinler de çevrilebiliyor mu (yapay zeka veya LibreTranslate tanımlı mı).</summary>
    bool FullTranslationAvailable { get; }

    /// <summary>Etkin motorların görünen adı.</summary>
    string EngineName { get; }

    Task<TranslationOutcome> TranslateAsync(ResumeContent content, string language, CancellationToken ct = default);
}

/// <summary>
/// CV içeriğini hedef dile çevirir. Kişisel ad, e-posta, telefon, bağlantılar, şirket adları ve tarihler çevrilmez.
/// Sıra: yapay zeka → LibreTranslate → yerleşik sözlük (yalnızca İngilizce, kısa ifadeler).
/// Sonuç, içerik özeti ve dile göre bellekte önbelleğe alınır; aynı CV'nin PDF'i tekrar indirilirken model yeniden çağrılmaz.
/// </summary>
public sealed class ResumeTranslator(
    IAiProvider ai,
    LibreTranslateClient libre,
    IMemoryCache cache,
    IOptions<TranslationOptions> options) : IResumeTranslator
{
    private readonly TranslationOptions _o = options.Value;

    // Türkçe karakterler \uXXXX'e kaçırılmasın; modele okunabilir metin gider.
    private static readonly JsonSerializerOptions PromptJson = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public bool FullTranslationAvailable => ai.IsEnabled || libre.IsEnabled;

    public string EngineName => string.Join(" + ", new[]
    {
        ai.IsEnabled ? ai.Name : null,
        libre.IsEnabled ? "LibreTranslate" : null
    }.Where(x => x is not null).DefaultIfEmpty("Yerleşik sözlük"));

    public async Task<TranslationOutcome> TranslateAsync(ResumeContent content, string language, CancellationToken ct = default)
    {
        var target = CvLanguages.Normalize(language);
        var copy = Clone(content);
        if (target == CvLanguages.Normalize(content.Language))
        {
            return new TranslationOutcome(copy, target, "Çeviri yok", true, 0, 0);
        }

        copy.Language = target == CvLanguages.Default ? null : target;

        var cacheKey = $"cv-translate:{target}:{Hash(content)}";
        if (cache.TryGetValue(cacheKey, out TranslationOutcome? cached) && cached is not null)
        {
            return cached with { Content = Clone(cached.Content) };
        }

        var segments = Collect(copy);
        var unique = segments.Select(s => s.Text).Distinct(StringComparer.Ordinal).ToList();
        var translations = new Dictionary<string, string>(StringComparer.Ordinal);
        var engines = new List<string>();

        if (ai.IsEnabled && unique.Count > 0)
        {
            var before = translations.Count;
            await TranslateWithAiAsync(unique, target, translations, ct);
            if (translations.Count > before) engines.Add(ai.Name);
        }

        var remaining = unique.Where(t => !translations.ContainsKey(t)).ToList();
        if (libre.IsEnabled && remaining.Count > 0)
        {
            var result = await libre.TranslateAsync(remaining, target, ct);
            if (result is not null)
            {
                for (var i = 0; i < remaining.Count; i++)
                {
                    if (result[i].HasValue()) translations[remaining[i]] = result[i].Trim();
                }

                engines.Add("LibreTranslate");
            }
        }

        remaining = unique.Where(t => !translations.ContainsKey(t)).ToList();
        if (target == "en" && remaining.Count > 0)
        {
            var hits = 0;
            foreach (var text in remaining)
            {
                if (CvGlossary.TryTranslateToEnglish(text, out var en))
                {
                    translations[text] = en;
                    hits++;
                }
            }

            if (hits > 0) engines.Add("Yerleşik sözlük");
        }

        var translatedCount = 0;
        foreach (var segment in segments)
        {
            if (translations.TryGetValue(segment.Text, out var value))
            {
                segment.Set(value);
                translatedCount++;
            }
        }

        var outcome = new TranslationOutcome(
            copy,
            target,
            engines.Count == 0 ? "Yalnızca başlıklar" : string.Join(" + ", engines),
            translatedCount == segments.Count,
            translatedCount,
            segments.Count);

        // Yalnızca motor başarılı olduysa önbelleğe al; geçici bir hata sonraki denemeyi engellemesin.
        if (outcome.Complete || !FullTranslationAvailable)
        {
            cache.Set(cacheKey, outcome with { Content = Clone(copy) }, TimeSpan.FromMinutes(Math.Max(1, _o.CacheMinutes)));
        }

        return outcome;
    }

    private async Task TranslateWithAiAsync(List<string> texts, string target, Dictionary<string, string> into, CancellationToken ct)
    {
        var chunkLimit = Math.Max(1000, _o.AiChunkChars);
        var chunk = new List<string>();
        var size = 0;

        async Task FlushAsync()
        {
            if (chunk.Count == 0) return;
            var items = chunk.Select((t, i) => (Key: (i + 1).ToString(), Text: t)).ToList();
            var payload = JsonSerializer.Serialize(items.ToDictionary(x => x.Key, x => x.Text), PromptJson);
            var reply = await ai.CompleteAsync(TranslationPrompts.System, TranslationPrompts.Build(payload, CvLanguages.EnglishName(target)), jsonOutput: true, ct);
            var parsed = ParseItems(reply);
            if (parsed is not null)
            {
                foreach (var (key, text) in items)
                {
                    if (parsed.TryGetValue(key, out var value) && value.HasValue())
                    {
                        into[text] = value.Trim();
                    }
                }
            }

            chunk.Clear();
            size = 0;
        }

        foreach (var text in texts)
        {
            if (size + text.Length > chunkLimit && chunk.Count > 0)
            {
                await FlushAsync();
            }

            chunk.Add(text);
            size += text.Length;
        }

        await FlushAsync();
    }

    /// <summary>{"items":{"1":"..."}} ya da doğrudan {"1":"..."} biçimini kabul eder.</summary>
    private static Dictionary<string, string>? ParseItems(string? reply)
    {
        var response = AiJson.TryParse<AiTranslationResponse>(reply);
        if (response?.Items is { Count: > 0 } items)
        {
            return items;
        }

        var flat = AiJson.TryParse<Dictionary<string, string>>(reply);
        return flat is { Count: > 0 } ? flat : null;
    }

    private sealed record Segment(string Text, Action<string> Set);

    /// <summary>Çevrilecek alanları toplar. Ad, iletişim, şirket, bağlantı, sertifika veren kurum ve tarihler bilinçli olarak dışarıda.</summary>
    private static List<Segment> Collect(ResumeContent c)
    {
        var list = new List<Segment>();

        void Add(string? value, Action<string> set)
        {
            if (value.HasValue())
            {
                list.Add(new Segment(value!.Trim(), set));
            }
        }

        Add(c.Personal.Title, v => c.Personal.Title = v);
        Add(c.Personal.City, v => c.Personal.City = v);
        Add(c.Summary, v => c.Summary = v);

        foreach (var e in c.Experiences)
        {
            Add(e.Position, v => e.Position = v);
            Add(e.Location, v => e.Location = v);
            AddLines(list, e.Description, v => e.Description = v);
        }

        foreach (var e in c.Educations)
        {
            Add(e.School, v => e.School = v);
            Add(e.Degree, v => e.Degree = v);
            Add(e.Field, v => e.Field = v);
            Add(e.Grade, v => e.Grade = v);
        }

        for (var i = 0; i < c.Skills.Count; i++)
        {
            var index = i;
            Add(c.Skills[i], v => c.Skills[index] = v);
        }

        foreach (var l in c.Languages)
        {
            Add(l.Name, v => l.Name = v);
        }

        foreach (var cert in c.Certificates)
        {
            Add(cert.Name, v => cert.Name = v);
        }

        foreach (var p in c.Projects)
        {
            AddLines(list, p.Description, v => p.Description = v);
        }

        return list;
    }

    /// <summary>Çok satırlı metni (deneyim maddeleri) satır satır çevirir; madde yapısı korunur.</summary>
    private static void AddLines(List<Segment> list, string? text, Action<string> setAll)
    {
        if (!text.HasValue())
        {
            return;
        }

        var lines = text!.Replace("\r\n", "\n").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var raw = lines[i];
            var body = raw.TrimStart('•', '-', '*', ' ', '\t');
            if (!body.HasValue())
            {
                continue;
            }

            var prefix = raw[..(raw.Length - body.Length)];
            var index = i;
            list.Add(new Segment(body.Trim(), v =>
            {
                lines[index] = prefix + v;
                setAll(string.Join("\n", lines));
            }));
        }
    }

    private static ResumeContent Clone(ResumeContent content) =>
        JsonSerializer.Deserialize<ResumeContent>(JsonSerializer.Serialize(content)) ?? new ResumeContent();

    private static string Hash(ResumeContent content) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(content))))[..32];
}

public sealed class AiTranslationResponse
{
    public Dictionary<string, string>? Items { get; set; }
}

public static class TranslationPrompts
{
    public const string System =
        "You are a professional translator specialised in CVs/resumes and recruitment terminology. " +
        "You translate faithfully, never add, remove or embellish information, and always answer with valid JSON only.";

    public static string Build(string itemsJson, string targetLanguage) =>
        $$"""
        Translate every value of the JSON object below into {{targetLanguage}}. The texts come from a CV (most likely written in Turkish).

        RULES:
        - Keep the keys exactly as they are; return one translation per key.
        - Use natural, concise CV language of {{targetLanguage}} (e.g. bullet points start with a past/present tense action verb, no first-person pronouns).
        - Do NOT translate or alter: technology/tool/framework names (C#, .NET, SAP, Excel...), product and brand names, company names, numbers, percentages, currencies, dates, e-mail addresses, URLs.
        - University and school names: use the institution's official name in {{targetLanguage}} only if it is well known (e.g. "Orta Doğu Teknik Üniversitesi" → "Middle East Technical University"); otherwise keep the original.
        - Certificate names: keep official certificate titles as issued; translate only descriptive words.
        - Turkish degree terms: Lisans = Bachelor's degree, Yüksek Lisans = Master's degree, Doktora = PhD, Ön Lisans = Associate degree.
        - Grades such as "3,25/4" or "GNO 3.2" keep their numbers; translate the label (e.g. "GPA 3.25/4").
        - If a value is already in {{targetLanguage}} or cannot be translated (a proper name), return it unchanged.

        Answer ONLY with this JSON shape:
        { "items": { "1": "...", "2": "..." } }

        INPUT:
        {{itemsJson}}
        """;
}
