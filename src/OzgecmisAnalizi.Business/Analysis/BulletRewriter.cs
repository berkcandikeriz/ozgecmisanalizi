using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace OzgecmisAnalizi.Business.Analysis;

/// <summary>Bir maddenin yeniden yazılmış hâli, yapılan düzeltmelerin açıklamaları ve daha güçlü yazım için ipucu.</summary>
public sealed record RewriteResult(string Text, IReadOnlyList<string> Notes, string? Tip);

/// <summary>
/// Yapay zeka olmadan, Türkçe CV maddelerini anlamını değiştirmeden daha net ve güçlü hâle getirir:
/// teknoloji adlarının standart yazımı, belirsiz "vs./ve s." sonları, gereksiz giriş kalıpları, zayıf ya da
/// sürerli geçmiş zaman fiiller, uzun birleşik cümleler, noktalama. Kişinin yazmadığı bilgi eklenmez.
/// </summary>
public static partial class BulletRewriter
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    /// <summary>Standart yazımlar. Uzun ifadeler önce denenir ("asp.net core" "asp.net"ten önce).</summary>
    private static readonly string[] Terms =
    [
        "ASP.NET Core", "ASP.NET MVC", "ASP.NET", ".NET Core", ".NET Framework", ".NET", "C#", "VB.NET", "Entity Framework Core",
        "Entity Framework", "EF Core", "LINQ", "REST API", "RESTful", "Web API", "API", "SOAP", "JSON", "XML", "HTML5", "HTML", "CSS3", "CSS",
        "SCSS", "JavaScript", "TypeScript", "jQuery", "Node.js", "Next.js", "Vue.js", "React", "Angular", "PrimeNG", "Bootstrap", "Tailwind",
        "SQL Server", "MSSQL", "T-SQL", "PL/SQL", "SQL", "MySQL", "PostgreSQL", "Oracle", "MongoDB", "Redis", "Elasticsearch", "RabbitMQ", "Kafka",
        "Docker", "Kubernetes", "Azure DevOps", "Azure", "AWS", "GCP", "CI/CD", "Git", "GitHub", "GitLab", "Bitbucket", "Jira", "Confluence",
        "Scrum", "Kanban", "Agile", "UI/UX", "UX", "UI", "MVC", "CRUD", "ERP", "CRM", "SAP", "Power BI", "Excel", "Python", "Java", "Spring Boot",
        "Flutter", "Dart", "Swift", "Kotlin", "Figma", "Photoshop", "Illustrator", "SEO", "Google Analytics", "DevExpress", "Blazor", "SignalR",
        "Swagger", "Postman", "Selenium", "xUnit", "NUnit", "OAuth", "JWT", "Linux", "Windows Server", "IIS", "Nginx", "WCF", "OOP", "SOLID"
    ];

    private static readonly (Regex Pattern, string Canonical)[] TermPatterns = Terms
        .OrderByDescending(t => t.Length)
        .Select(t => (new Regex(@"(?<![\p{L}\p{N}.#+])" + TermToPattern(t) + @"(?![\p{L}\p{N}#+]|\.[\p{L}])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), t))
        .ToArray();

    /// <summary>Cümle başındaki, bilgi taşımayan giriş kalıpları.</summary>
    private static readonly string[] Openings =
    [
        "staj sürecimde", "staj süresince", "staj dönemimde", "stajım süresince", "stajım boyunca", "bu süreçte", "bu süre zarfında",
        "çalıştığım süre boyunca", "görev aldığım süre boyunca", "bu pozisyonda", "bu rolde", "şirkette", "burada", "ayrıca",
        "bunun yanı sıra", "bunun yanında", "aynı zamanda", "ek olarak", "ben", "benim"
    ];

    /// <summary>Zayıf / edilgen kalıpların daha net karşılıkları (cümle sonunda).</summary>
    private static readonly (string Weak, string Strong)[] VerbUpgrades =
    [
        ("geliştirme yaptım", "geliştirdim"),
        ("geliştirmesini yaptım", "geliştirdim"),
        ("tasarımını yaptım", "tasarladım"),
        ("analizini yaptım", "analiz ettim"),
        ("testlerini yaptım", "test ettim"),
        ("kurulumunu yaptım", "kurdum"),
        ("raporlamasını yaptım", "raporladım"),
        ("yönetimini yaptım", "yönettim"),
        ("takibini yaptım", "takip ettim"),
        ("entegrasyonunu yaptım", "entegre ettim"),
        ("optimizasyonunu yaptım", "optimize ettim"),
        ("destek verdim", "destek sağladım"),
        ("yardımcı oldum", "destek sağladım"),
        ("görev aldım", "aktif rol aldım"),
        ("yer aldım", "aktif rol aldım"),
        ("sorumluydum", "sorumluluğunu üstlendim"),
        ("sorumlu oldum", "sorumluluğunu üstlendim"),
        ("katkıda bulundum", "katkı sağladım")
    ];

    [GeneratedRegex(@"\s*(?:,\s*)?(?:ve\s+s\.?|ve\s+saire|vs\.?|vb\.?|ve\s+benzeri|ve\s+diğerleri|v\.b\.|v\.s\.)\s*(?=[.;,)]|$)", RegexOptions.IgnoreCase)]
    private static partial Regex VagueTail();

    [GeneratedRegex(@"\s*(?:\.\.\.|…)\s*$")]
    private static partial Regex Ellipsis();

    [GeneratedRegex(@",(?=[^\s\d])")]
    private static partial Regex CommaNoSpace();

    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex MultiSpace();

    [GeneratedRegex(@"\s+([,.;:])")]
    private static partial Regex SpaceBeforePunct();

    /// <summary>"…teknolojilerini kullanarak" → "… ile" (anlam aynı, daha kısa).</summary>
    [GeneratedRegex(@"\s+(?:teknolojilerini|teknolojisini|teknolojileri ile|teknolojisi ile|araçlarını|aracını|kütüphanelerini|framework(?:'ünü|ünü|lerini)?)\s+kullanarak\b", RegexOptions.IgnoreCase)]
    private static partial Regex UsingTechnologies();

    /// <summary>Sürerli geçmiş zaman: "geliştiriyordum" → "geliştirdim", "yönetmekteydim" → "yönettim".</summary>
    [GeneratedRegex(@"\b(?<stem>[\p{L}]+?)(?<v>[ıiuü])yordum\b", RegexOptions.IgnoreCase)]
    private static partial Regex PastContinuous();

    [GeneratedRegex(@"\b(?<stem>[\p{L}]+?)(?:mekteydim|maktaydım)\b", RegexOptions.IgnoreCase)]
    private static partial Regex PastProgressive();

    /// <summary>Birinci tekil geçmiş zaman ya da şimdiki zaman ile biten bir fiil.</summary>
    [GeneratedRegex(@"(?:[dt][ıiuü]m|yorum|[ıiuü]m|[ae]rım|[ae]rim|[ae]bildim|[ae]bildım)$", RegexOptions.IgnoreCase)]
    private static partial Regex EndsWithFirstPersonVerb();

    /// <summary>Birleşik cümle: "…geliştirdim ve …" / "…geliştirdim, …" — ilk fiilden sonra bölünür.</summary>
    [GeneratedRegex(@"(?<=[\p{L}][dt][ıiuü]m)(?:,\s+|\s+ve\s+|;\s*)(?=\p{L})", RegexOptions.IgnoreCase)]
    private static partial Regex CompoundSplit();

    [GeneratedRegex(@"\d")]
    private static partial Regex Digit();

    public static RewriteResult Rewrite(string bullet, bool isCurrentRole)
    {
        var notes = new List<string>();
        var text = bullet.Trim().TrimStart('•', '-', '*', '–', '·', '▪', '►', ' ');

        // 1) Yarım kalmış "…" sonu ve belirsiz "ve s. / vs." kuyrukları
        var noEllipsis = Ellipsis().Replace(text, string.Empty);
        if (noEllipsis != text)
        {
            notes.Add("Yarım bırakılmış \"…\" sonu kaldırıldı; madde tam bir cümleyle bitmeli.");
            text = noEllipsis;
        }

        var noVague = VagueTail().Replace(text, string.Empty);
        if (noVague != text)
        {
            notes.Add("\"ve s. / vs.\" gibi belirsiz ifadeler çıkarıldı; ne yaptığınızı açıkça sayın.");
            text = noVague;
        }

        // 2) Bilgi taşımayan giriş kalıbı
        foreach (var opening in Openings)
        {
            if (text.StartsWith(opening + " ", true, Tr) || text.StartsWith(opening + ", ", true, Tr))
            {
                text = text[opening.Length..].TrimStart(',', ' ');
                notes.Add($"\"{Capitalize(opening)}\" girişi çıkarıldı; bağlam zaten pozisyon başlığında var, madde doğrudan işle başlamalı.");
                break;
            }
        }

        // 3) Teknoloji adlarının standart yazımı
        var renamed = new List<string>();
        foreach (var (pattern, canonical) in TermPatterns)
        {
            text = pattern.Replace(text, m =>
            {
                if (m.Value != canonical)
                {
                    renamed.Add($"{m.Value} → {canonical}");
                }

                return canonical;
            });
        }

        if (renamed.Count > 0)
        {
            notes.Add($"Teknoloji adları ilanlardaki standart yazımla eşleşecek şekilde düzeltildi ({string.Join(", ", renamed.Distinct().Take(5))}); ATS anahtar kelimeleri bu yazımla arar.");
        }

        // 4) "X teknolojilerini kullanarak" → "X ile"
        var shorter = UsingTechnologies().Replace(text, " ile");
        if (shorter != text)
        {
            notes.Add("\"…teknolojilerini kullanarak\" kalıbı \"… ile\" olarak kısaltıldı.");
            text = shorter;
        }

        // 5) Sürerli geçmiş zaman → geçmiş zaman (eski görevlerde)
        if (!isCurrentRole)
        {
            var simple = PastContinuous().Replace(text, m => SimplePast(m.Groups["stem"].Value, m.Groups["v"].Value[0]));
            simple = PastProgressive().Replace(simple, m => m.Groups["stem"].Value + Past(m.Groups["stem"].Value, LastVowelHarmony(m.Groups["stem"].Value)));
            if (simple != text)
            {
                notes.Add("\"…yordum / …mekteydim\" yerine sonucu vurgulayan geçmiş zaman kullanıldı (\"geliştiriyordum\" → \"geliştirdim\").");
                text = simple;
            }
        }

        // 6) Zayıf fiil kalıpları
        foreach (var (weak, strong) in VerbUpgrades)
        {
            var idx = text.LastIndexOf(weak, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0 && text[(idx + weak.Length)..].Trim(' ', '.', ';').Length == 0)
            {
                text = text[..idx] + strong + text[(idx + weak.Length)..];
                notes.Add($"\"{weak}\" yerine daha net \"{strong}\" kullanıldı.");
                break;
            }
        }

        // 7) Noktalama
        var punct = MultiSpace().Replace(SpaceBeforePunct().Replace(CommaNoSpace().Replace(text, ", "), "$1"), " ").Trim().TrimEnd(',', ';', ' ');
        if (punct != text)
        {
            if (CommaNoSpace().IsMatch(text))
            {
                notes.Add("Virgülden sonra boşluk eklendi.");
            }

            text = punct;
        }

        // 8) İlk harf büyük, sonda tek nokta yok (CV maddelerinde tutarlılık)
        text = Capitalize(text.TrimEnd('.'));

        // 9) Uzun birleşik cümleyi ikiye böl
        var parts = CompoundSplit().Split(text).Select(p => Capitalize(p.Trim())).Where(p => p.Length > 2).ToList();
        if (parts.Count > 1 && text.Split(' ').Length > 16)
        {
            text = string.Join("\n", parts);
            notes.Add("Birden fazla işi anlatan uzun cümle tek fikirli maddelere bölündü.");
        }

        // İpuçları (uygulanmaz, kullanıcıya gösterilir)
        var tips = new List<string>();
        var lastLine = text.Split('\n')[^1];
        var lastWord = lastLine.Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? string.Empty;
        if (!EndsWithFirstPersonVerb().IsMatch(lastWord.Trim('.', ')', '"')))
        {
            tips.Add("Madde bir eylemle bitmiyor. Ne yaptığınızı söyleyen bir fiille bitirin: geliştirdim, kurdum, yönettim, iyileştirdim.");
        }

        if (!Digit().IsMatch(text))
        {
            tips.Add($"Etkiyi ölçülebilir yazın. Örnek kalıp: \"{lastLine.TrimEnd('.')}; bu sayede [işlem süresi %30 kısaldı / 200 ürünlük stok tek ekrandan yönetildi].\" Köşeli parantezi kendi gerçek sonucunuzla değiştirin.");
        }

        return new RewriteResult(string.Join("\n", text.Split('\n').Select(l => "• " + l)), notes, tips.Count == 0 ? null : string.Join(" ", tips));
    }

    /// <summary>Özet gibi düz metinlerde yalnızca güvenli düzeltmeler: teknoloji yazımı, belirsiz kuyruklar, noktalama.</summary>
    public static (string Text, List<string> Notes) Polish(string text)
    {
        var notes = new List<string>();
        var result = VagueTail().Replace(text, string.Empty);
        if (result != text) notes.Add("\"vs. / ve s.\" gibi belirsiz ifadeler çıkarıldı.");

        var renamed = new List<string>();
        foreach (var (pattern, canonical) in TermPatterns)
        {
            result = pattern.Replace(result, m =>
            {
                if (m.Value != canonical) renamed.Add($"{m.Value} → {canonical}");
                return canonical;
            });
        }

        if (renamed.Count > 0) notes.Add($"Teknoloji adlarının yazımı düzeltildi ({string.Join(", ", renamed.Distinct().Take(5))}).");

        var punct = MultiSpace().Replace(SpaceBeforePunct().Replace(CommaNoSpace().Replace(result, ", "), "$1"), " ").Trim();
        if (punct != result) notes.Add("Noktalama ve boşluklar düzeltildi.");
        return (punct, notes);
    }

    /// <summary>Beceri adını standart yazıma çevirir; tanınmıyorsa olduğu gibi döner.</summary>
    public static string CanonicalTerm(string skill)
    {
        foreach (var (pattern, canonical) in TermPatterns)
        {
            var m = pattern.Match(skill);
            if (m.Success && m.Index == 0 && m.Length == skill.Trim().Length)
            {
                return canonical;
            }
        }

        return skill;
    }

    /// <summary>
    /// "-iyordum" gövdesinden geçmiş zaman: geliştir+iyordum → geliştirdim, yap+ıyordum → yaptım,
    /// sağl+ıyordum → sağladım (daralmış -la/-le), ed+iyordum → ettim (ünsüz yumuşaması).
    /// </summary>
    private static string SimplePast(string stem, char v)
    {
        var lower = stem.ToLower(Tr);
        const string vowels = "aeıioöuü";

        // "ed-/gid-" gibi yumuşamış kökler: et → ettim, git → gittim
        if (lower.Length <= 3 && lower.EndsWith('d'))
        {
            return stem[..^1] + "t" + "t" + char.ToLower(v, Tr) + "m";
        }

        // Daralmış -la/-le fiilleri: sondaki iki harf ünsüzse (sağl-, destekl-, düzenl-) a/e geri eklenir.
        if (lower.Length >= 2 && !vowels.Contains(lower[^1]) && !vowels.Contains(lower[^2]))
        {
            var back = v is 'ı' or 'u' or 'I' or 'U';
            return stem + (back ? "adım" : "edim");
        }

        return stem + Past(stem, v);
    }

    /// <summary>Geçmiş zaman eki: ünsüz benzeşmesi (fıstıkçı şahap → t) ve 4'lü ünlü uyumu.</summary>
    private static string Past(string stem, char vowel)
    {
        var last = char.ToLower(stem[^1], Tr);
        var d = "fstkçşhp".Contains(last) ? 't' : 'd';
        return $"{d}{char.ToLower(vowel, Tr)}m";
    }

    private static char LastVowelHarmony(string stem)
    {
        foreach (var ch in stem.ToLower(Tr).Reverse())
        {
            switch (ch)
            {
                case 'a' or 'ı': return 'ı';
                case 'e' or 'i': return 'i';
                case 'o' or 'u': return 'u';
                case 'ö' or 'ü': return 'ü';
            }
        }

        return 'i';
    }

    private static string Capitalize(string s) => s.Length == 0 ? s : char.ToUpper(s[0], Tr) + s[1..];

    /// <summary>Türkçe büyük/küçük harf farkını (İ/ı) da tolere eden kalıp.</summary>
    private static string TermToPattern(string term)
    {
        var sb = new StringBuilder();
        foreach (var ch in term)
        {
            sb.Append(ch switch
            {
                'i' or 'I' => "[iIıİ]",
                ' ' => @"\s+",
                _ => Regex.Escape(ch.ToString())
            });
        }

        return sb.ToString();
    }
}
