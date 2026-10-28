namespace OzgecmisAnalizi.Business.Options;

public enum AiProviderKind
{
    /// <summary>Yapay zeka kapalı; yalnızca kural motoru çalışır.</summary>
    None,
    /// <summary>OpenAI uyumlu /chat/completions (OpenAI, Groq, OpenRouter, Ollama, LM Studio...).</summary>
    OpenAICompatible,
    /// <summary>Google Gemini (ücretsiz katmanı vardır).</summary>
    Gemini
}

public sealed class AiOptions
{
    public const string SectionName = "Ai";

    public AiProviderKind Provider { get; set; } = AiProviderKind.None;

    /// <summary>OpenAI uyumlu sağlayıcı için taban adres, örn. https://api.openai.com/v1 veya http://localhost:11434/v1 (Ollama).</summary>
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";

    public string? ApiKey { get; set; }
    public string Model { get; set; } = "gpt-4o-mini";
    public int TimeoutSeconds { get; set; } = 60;
    public int MaxInputChars { get; set; } = 12000;
}

public sealed class SalaryOptions
{
    public const string SectionName = "Salary";

    /// <summary>Aylık net asgari ücret (TL). 2026 için 28.075,50 TL. Her yıl güncellenmelidir.</summary>
    public decimal MinimumNetWage { get; set; } = 28075.50m;

    public int ReferenceYear { get; set; } = 2026;

    /// <summary>Şehir katsayıları; listede olmayan şehirler için <see cref="DefaultCityFactor"/> kullanılır.</summary>
    public Dictionary<string, decimal> CityFactors { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["İstanbul"] = 1.15m,
        ["Ankara"] = 1.00m,
        ["İzmir"] = 1.00m,
        ["Kocaeli"] = 1.05m,
        ["Bursa"] = 0.98m,
        ["Antalya"] = 0.95m,
        ["Uzaktan"] = 1.05m
    };

    public decimal DefaultCityFactor { get; set; } = 0.92m;
}

public sealed class JobSearchOptions
{
    public const string SectionName = "JobSearch";

    public string DefaultLocation { get; set; } = "Türkiye";
    public int TimeoutSeconds { get; set; } = 15;

    /// <summary>
    /// Site arama bağlantı şablonları. Yer tutucular: {q} {l} (URL kodlu), {qslug} {lslug} (kısa ad).
    /// API anahtarı gerektirmez; kullanıcıyı ilgili sitenin kendi arama sonucuna götürür.
    /// </summary>
    public Dictionary<string, string> SiteLinks { get; set; } = new()
    {
        ["LinkedIn"] = "https://www.linkedin.com/jobs/search/?keywords={q}&location={l}",
        ["Indeed"] = "https://tr.indeed.com/jobs?q={q}&l={l}",
        ["Kariyer.net"] = "https://www.kariyer.net/is-ilanlari/{qslug}",
        ["Yenibiris"] = "https://www.yenibiris.com/is-ilanlari?q={q}",
        ["Eleman.net"] = "https://www.eleman.net/is-ilanlari?aranan={q}"
    };

    public JoobleOptions Jooble { get; set; } = new();
    public JSearchOptions JSearch { get; set; } = new();
}

/// <summary>Jooble ücretsiz API'si (Türkiye ilanlarını kapsar). Anahtar: https://jooble.org/api/about</summary>
public sealed class JoobleOptions
{
    public string? ApiKey { get; set; }
    public string BaseUrl { get; set; } = "https://tr.jooble.org/api/";
    public bool Enabled => !string.IsNullOrWhiteSpace(ApiKey);
}

/// <summary>JSearch (RapidAPI) — Google for Jobs üzerinden LinkedIn, Indeed vb. ilanları toplar.</summary>
public sealed class JSearchOptions
{
    public string? ApiKey { get; set; }
    public string Host { get; set; } = "jsearch.p.rapidapi.com";
    public string Country { get; set; } = "tr";
    public bool Enabled => !string.IsNullOrWhiteSpace(ApiKey);
}
