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

/// <summary>
/// CV çevirisi. Sıra: yapay zeka (Ai bölümü) → LibreTranslate (ücretsiz, kendi sunucunuzda çalışabilir) → yerleşik sözlük.
/// Hiçbiri yoksa yalnızca başlıklar, dil seviyeleri ve sözlükteki unvan/beceri adları çevrilir.
/// </summary>
public sealed class TranslationOptions
{
    public const string SectionName = "Translation";

    /// <summary>LibreTranslate adresi, örn. http://localhost:5000 (docker run -p 5000:5000 libretranslate/libretranslate). Boşsa kapalı.</summary>
    public string? LibreTranslateUrl { get; set; }

    /// <summary>LibreTranslate sunucusu anahtar istiyorsa.</summary>
    public string? LibreTranslateApiKey { get; set; }

    /// <summary>Aynı CV içeriği ve dil için çeviri sonucunun bellekte tutulacağı süre.</summary>
    public int CacheMinutes { get; set; } = 120;

    /// <summary>Yapay zekaya tek istekte gönderilecek en fazla karakter; uzun CV'ler parçalara bölünür.</summary>
    public int AiChunkChars { get; set; } = 6000;
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

    /// <summary>Türkiye'den başvurulabilecek uzaktan iş bölgeleri (ilan konumunda geçmesi yeterli).</summary>
    public List<string> RemoteRegions { get; set; } =
        ["Worldwide", "Anywhere", "Global", "Europe", "EMEA", "EU", "Turkey", "Türkiye", "CET", "UTC"];

    /// <summary>Uzaktan iş sağlayıcıları (Remotive, Jobicy, Arbeitnow) sonuçlara varsayılan olarak eklensin mi.</summary>
    public bool IncludeRemoteByDefault { get; set; } = true;

    public JoobleOptions Jooble { get; set; } = new();
    public JSearchOptions JSearch { get; set; } = new();
    public CareerjetOptions Careerjet { get; set; } = new();
    public RemotiveOptions Remotive { get; set; } = new();
    public JobicyOptions Jobicy { get; set; } = new();
    public ArbeitnowOptions Arbeitnow { get; set; } = new();
}

/// <summary>
/// Careerjet v4 — Türkiye (tr_TR) ilanları. Ücretsiz yayıncı hesabı: https://www.careerjet.com/partners/
/// Anahtar girilmezse kapalıdır.
/// </summary>
public sealed class CareerjetOptions
{
    public string? ApiKey { get; set; }
    public string BaseUrl { get; set; } = "https://search.api.careerjet.net/v4/query";
    public string LocaleCode { get; set; } = "tr_TR";
    public int PageSize { get; set; } = 20;
    public int CacheMinutes { get; set; } = 30;

    /// <summary>Yayıncı hesabında kayıtlı site adresi; Referer başlığı olarak gönderilir.</summary>
    public string? Referer { get; set; }

    /// <summary>Kullanıcı IP'si alınamazsa gönderilecek değer.</summary>
    public string FallbackUserIp { get; set; } = "127.0.0.1";

    public bool Enabled => !string.IsNullOrWhiteSpace(ApiKey);
}

/// <summary>Remotive herkese açık API'si — anahtar gerekmez. Koşullar gereği günde birkaç istek: besleme önbelleğe alınır.</summary>
public sealed class RemotiveOptions
{
    public bool Enabled { get; set; } = true;
    public string BaseUrl { get; set; } = "https://remotive.com/api/remote-jobs";
    public int CacheHours { get; set; } = 6;
    public int MaxResults { get; set; } = 15;
}

/// <summary>Jobicy herkese açık API'si (v2) — anahtar gerekmez. Saatte birden sık sorgulanmaz.</summary>
public sealed class JobicyOptions
{
    public bool Enabled { get; set; } = true;
    public string BaseUrl { get; set; } = "https://jobicy.com/api/v2/remote-jobs";
    public int Count { get; set; } = 50;
    public int CacheHours { get; set; } = 2;
    public int MaxResults { get; set; } = 15;
}

/// <summary>Arbeitnow herkese açık API'si — anahtar gerekmez. Yalnızca uzaktan ilanlar alınır.</summary>
public sealed class ArbeitnowOptions
{
    public bool Enabled { get; set; } = true;
    public string BaseUrl { get; set; } = "https://www.arbeitnow.com/api/job-board-api";
    public int Pages { get; set; } = 3;
    public int CacheHours { get; set; } = 2;
    public int MaxResults { get; set; } = 10;
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
