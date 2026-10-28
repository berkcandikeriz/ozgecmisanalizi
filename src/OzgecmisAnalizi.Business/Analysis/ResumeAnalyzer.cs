using System.Text.RegularExpressions;
using OzgecmisAnalizi.Business.Parsing;
using OzgecmisAnalizi.Core.Extensions;
using OzgecmisAnalizi.Entities.Content;
using OzgecmisAnalizi.Entities.Enums;

namespace OzgecmisAnalizi.Business.Analysis;

public sealed class AnalysisOutcome
{
    public int Overall { get; init; }
    public int Comprehensiveness { get; init; }
    public int Structure { get; init; }
    public int Clarity { get; init; }
    public int Ats { get; init; }
    public int SkillMatch { get; init; }
    public string Label { get; init; } = string.Empty;
    public int TopPercent { get; init; }
    public CatalogProfession? Profession { get; init; }
    public ExperienceLevel Level { get; init; }
    public AnalysisDetails Details { get; init; } = new();
}

public interface IResumeAnalyzer
{
    AnalysisOutcome Analyze(ResumeContent content, string text, IReadOnlyList<CatalogProfession> catalog, Guid? professionId, string? city);
}

/// <summary>
/// Kural tabanlı CV değerlendirme motoru. Yapay zeka olmadan da tutarlı ve açıklanabilir puan üretir:
/// kapsam, yapı, açıklık, ATS okunabilirliği ve meslek beceri eşleşmesi.
/// </summary>
public sealed partial class ResumeAnalyzer(IResumeTextParser parser, ISalaryEstimator salaryEstimator) : IResumeAnalyzer
{
    private static readonly string[] ActionVerbs =
    [
        "gelistirdim", "gelistirdi", "yonettim", "yonetti", "tasarladim", "olusturdum", "artirdim", "azalttim", "sagladim",
        "koordine ettim", "uyguladim", "kurdum", "iyilestirdim", "optimize ettim", "hazirladim", "yuruttum", "gerceklestirdim",
        "analiz ettim", "raporladim", "egittim", "basardim", "tamamladim", "liderlik ettim", "otomatiklestirdim", "devreye aldim",
        "hayata gecirdim", "planladim", "kazandirdim", "dusurdum", "yukselttim", "entegre ettim", "tasidim", "modernize ettim",
        "developed", "led", "managed", "designed", "built", "implemented", "improved", "increased", "reduced", "delivered",
        "launched", "created", "optimized", "automated", "achieved", "coordinated", "migrated", "negotiated", "trained"
    ];

    private static readonly string[] Cliches =
    [
        "takim oyuncusu", "takim calismasina yatkin", "motivasyonu yuksek", "dinamik", "sorumluluk sahibi", "ogrenmeye acik",
        "detayci", "cok calisan", "hard working", "team player", "self motivated", "detail oriented", "problem cozme yetenegi yuksek"
    ];

    [GeneratedRegex(@"\d+([.,]\d+)?\s*(%|yüzde|kat|x\b|kişi|kisi|müşteri|musteri|proje|milyon|bin|tl|₺|\$|€|saat|gün|gun|ay\b|users|clients|projects)|%\s*\d+", RegexOptions.IgnoreCase)]
    private static partial Regex QuantifiedRegex();

    [GeneratedRegex(@"[.!?]+\s+|\n")]
    private static partial Regex SentenceSplit();

    [GeneratedRegex(@"[^\p{L}\p{N}\p{P}\p{Zs}\n\r\t+#$€₺%]")]
    private static partial Regex OddChars();

    public AnalysisOutcome Analyze(ResumeContent content, string text, IReadOnlyList<CatalogProfession> catalog, Guid? professionId, string? city)
    {
        var sections = parser.SplitSections(text);
        var prepared = SkillMatcher.PrepareText(text);
        var words = text.WordCount();
        var details = new AnalysisDetails();

        // --- Deneyim süresi ve seviye
        var years = ComputeExperienceYears(content.Experiences);
        details.ExperienceYears = Math.Round(years, 1);
        var level = CareerPathBuilder.LevelFromYears(years, content.Experiences.Count > 0);

        // --- Meslek eşleşmesi
        var titleText = SkillMatcher.PrepareText(string.Join(" ", new[] { content.Personal.Title }
            .Concat(content.Experiences.Select(e => e.Position))
            .Where(x => !string.IsNullOrWhiteSpace(x))));

        var matches = catalog
            .Select(p => (Profession: p, Percent: ProfessionMatchPercent(p, prepared, titleText)))
            .OrderByDescending(x => x.Percent)
            .Take(5)
            .ToList();

        details.ProfessionMatches = matches.Take(3)
            .Select(m => new ProfessionMatch { ProfessionId = m.Profession.Id, Name = m.Profession.Name, MatchPercent = m.Percent })
            .ToList();

        var profession = professionId is { } pid
            ? catalog.FirstOrDefault(p => p.Id == pid)
            : matches.FirstOrDefault().Percent >= 15 ? matches.First().Profession : null;

        // --- Beceriler
        var detected = new List<string>();
        var missing = new List<CatalogSkill>();
        var skillScore = 0;
        if (profession is not null)
        {
            double total = 0, found = 0;
            foreach (var skill in profession.Skills)
            {
                var weight = (int)skill.Importance;
                total += weight;
                if (SkillMatcher.ContainsAny(prepared, skill.Keys))
                {
                    found += weight;
                    detected.Add(skill.Name);
                }
                else
                {
                    missing.Add(skill);
                }
            }

            skillScore = total > 0 ? (int)Math.Round(found / total * 100) : 0;
            details.MissingSkills = missing
                .OrderByDescending(m => m.Importance)
                .Select(m => new SkillGap { Skill = m.Name, Importance = ImportanceName(m.Importance), HowToLearn = m.Hint })
                .ToList();
            details.CareerPath = CareerPathBuilder.Build(profession, level, missing);
            details.Salary = salaryEstimator.Estimate(profession, level, city ?? content.Personal.City);
        }

        // Meslek dışı ama katalogda olan beceriler de görünsün.
        var others = catalog.Where(p => p != profession).SelectMany(p => p.Skills)
            .Where(s => SkillMatcher.ContainsAny(prepared, s.Keys))
            .Select(s => s.Name);
        details.DetectedSkills = detected.Concat(others).Concat(content.Skills).DistinctBySearchKey().Take(40).ToList();

        // --- Alt puanlar
        var sectionFeedback = new List<SectionFeedback>();
        var comprehensiveness = ScoreComprehensiveness(content, words, sectionFeedback);
        var structure = ScoreStructure(content, sections, words, sectionFeedback);
        var clarity = ScoreClarity(content, text, sectionFeedback);
        var ats = ScoreAts(content, text, sections, words, profession, prepared, details.AtsChecks);

        AddSkillsFeedback(content, profession, missing, detected, sectionFeedback);
        details.Sections = sectionFeedback.OrderBy(s => s.Score).ToList();

        var overall = profession is not null
            ? 0.20 * comprehensiveness + 0.15 * structure + 0.20 * clarity + 0.20 * ats + 0.25 * skillScore
            : (0.20 * comprehensiveness + 0.15 * structure + 0.20 * clarity + 0.20 * ats) / 0.75;
        var overallInt = Math.Clamp((int)Math.Round(overall), 0, 100);

        details.Strengths = BuildStrengths(content, details, profession, detected, years, ats, clarity);
        details.Improvements = BuildImprovements(details, missing);

        return new AnalysisOutcome
        {
            Overall = overallInt,
            Comprehensiveness = comprehensiveness,
            Structure = structure,
            Clarity = clarity,
            Ats = ats,
            SkillMatch = skillScore,
            Label = LabelFor(overallInt),
            TopPercent = Math.Clamp(100 - overallInt + 2, 1, 95),
            Profession = profession,
            Level = level,
            Details = details
        };
    }

    public static string LabelFor(int score) => score switch
    {
        >= 85 => "Mükemmel",
        >= 72 => "Çok iyi",
        >= 58 => "İyi",
        >= 40 => "Geliştirilmeli",
        _ => "Zayıf"
    };

    public static string ImportanceName(SkillImportance importance) => importance switch
    {
        SkillImportance.Core => "Olmazsa olmaz",
        SkillImportance.Important => "Önemli",
        _ => "Artı değer"
    };

    private static int ProfessionMatchPercent(CatalogProfession p, string prepared, string titleText)
    {
        double total = 0, found = 0;
        foreach (var s in p.Skills)
        {
            var w = (int)s.Importance;
            total += w;
            if (SkillMatcher.ContainsAny(prepared, s.Keys))
            {
                found += w;
            }
        }

        var coverage = total > 0 ? found / total * 100 : 0;
        var titleKeys = p.NameKeys.Concat(p.SearchKeywords.Select(SkillMatcher.NormalizeKey));
        var titleBonus = titleKeys.Any(k => k.Length > 2 && titleText.Contains(k, StringComparison.Ordinal)) ? 30 : 0;
        return (int)Math.Clamp(Math.Round(coverage * 0.8 + titleBonus), 0, 100);
    }

    private static double ComputeExperienceYears(List<ExperienceItem> experiences)
    {
        var total = experiences.Sum(e => ResumeDates.YearsBetween(e.StartDate, e.EndDate, e.IsCurrent));
        var earliest = experiences.Select(e => ResumeDates.Parse(e.StartDate)).Where(d => d is not null).Min();
        if (earliest is { } first)
        {
            var span = (DateTime.UtcNow - first.ToDateTime(TimeOnly.MinValue)).TotalDays / 365.25;
            total = Math.Min(total, span); // Örtüşen işler toplamı şişirmesin.
        }

        return Math.Max(0, total);
    }

    private static int ScoreComprehensiveness(ResumeContent c, int words, List<SectionFeedback> feedback)
    {
        var score = 0;
        var personalTips = new List<string>();
        var p = c.Personal;
        if (p.FullName.HasValue()) score += 5; else personalTips.Add("Ad soyadınızı en üste, büyük puntoyla yazın.");
        if (p.Email.HasValue()) score += 8; else personalTips.Add("Profesyonel bir e-posta adresi ekleyin.");
        if (p.Phone.HasValue()) score += 7; else personalTips.Add("Telefon numaranızı ekleyin (+90 5xx xxx xx xx).");
        if (p.City.HasValue()) score += 2; else personalTips.Add("Bulunduğunuz şehri yazın; işverenler konuma göre filtreler.");
        if (p.LinkedIn.HasValue()) score += 3; else personalTips.Add("LinkedIn profil bağlantınızı ekleyin.");
        if (p.Title.HasValue()) score += 3; else personalTips.Add("Adınızın altına hedeflediğiniz unvanı yazın (örn. \"Backend Geliştirici\").");
        feedback.Add(new SectionFeedback { Section = "Kişisel bilgiler", Score = Math.Min(100, (int)(score / 28.0 * 100)), Tips = personalTips });

        var summaryWords = c.Summary.WordCount();
        var summaryTips = new List<string>();
        int summaryScore;
        if (summaryWords == 0)
        {
            summaryScore = 0;
            summaryTips.Add("3-4 cümlelik bir profesyonel özet ekleyin: kim olduğunuz, kaç yıllık deneyim, en güçlü 2-3 beceri ve hedefiniz.");
        }
        else if (summaryWords < 25)
        {
            summaryScore = 55;
            summaryTips.Add("Özetiniz çok kısa. Deneyim yılınızı ve somut bir başarınızı ekleyin.");
        }
        else if (summaryWords > 120)
        {
            summaryScore = 65;
            summaryTips.Add("Özetiniz uzun. 60-90 kelimeye indirip en güçlü noktaları öne alın.");
        }
        else
        {
            summaryScore = 100;
        }

        score += summaryScore * 15 / 100;
        feedback.Add(new SectionFeedback { Section = "Özet", Score = summaryScore, Tips = summaryTips });

        if (c.Experiences.Count > 0)
        {
            score += 18;
            var described = c.Experiences.Count(e => e.Description.WordCount() >= 12);
            score += (int)Math.Round(7.0 * described / c.Experiences.Count);
        }

        if (c.Educations.Count > 0) score += 13;
        score += Math.Min(10, c.Skills.Count);
        if (c.Languages.Count > 0) score += 5;
        if (c.Certificates.Count > 0 || c.Projects.Count > 0) score += 4;

        if (words is >= 250 and <= 1100) score += 0; else score -= 5;
        return Math.Clamp(score, 0, 100);
    }

    private static int ScoreStructure(ResumeContent c, IReadOnlyDictionary<ResumeSection, List<string>> sections, int words, List<SectionFeedback> feedback)
    {
        var score = 0;
        var core = new[] { ResumeSection.Experience, ResumeSection.Education, ResumeSection.Skills, ResumeSection.Summary, ResumeSection.Languages };
        var found = core.Count(sections.ContainsKey);
        // Elle oluşturulan CV'lerde başlıklar zaten standarttır.
        if (found == 0 && (c.Experiences.Count > 0 || c.Educations.Count > 0))
        {
            found = new[] { c.Experiences.Count > 0, c.Educations.Count > 0, c.Skills.Count > 0, c.Summary.HasValue(), c.Languages.Count > 0 }.Count(x => x);
        }

        score += found * 8;

        var expTips = new List<string>();
        var expScore = 0;
        if (c.Experiences.Count == 0)
        {
            expTips.Add("İş deneyimi bölümü bulunamadı. Staj, gönüllülük veya proje deneyimlerinizi \"İş Deneyimi\" başlığı altında listeleyin.");
        }
        else
        {
            var dated = c.Experiences.Count(e => e.StartDate.HasValue());
            var ratio = dated / (double)c.Experiences.Count;
            score += (int)Math.Round(25 * ratio);
            expScore += (int)Math.Round(40 * ratio);
            if (ratio < 1)
            {
                expTips.Add("Her pozisyon için başlangıç ve bitiş tarihini (AA/YYYY) yazın.");
            }

            var starts = c.Experiences.Select(e => ResumeDates.Parse(e.StartDate)).Where(d => d is not null).Select(d => d!.Value).ToList();
            var reverse = starts.Count < 2 || starts.Zip(starts.Skip(1)).All(pair => pair.First >= pair.Second);
            if (reverse)
            {
                score += 15;
                expScore += 20;
            }
            else
            {
                expTips.Add("Deneyimleri en yeniden en eskiye doğru sıralayın.");
            }

            var withCompany = c.Experiences.Count(e => e.Company.HasValue());
            expScore += (int)Math.Round(20.0 * withCompany / c.Experiences.Count);
            if (withCompany < c.Experiences.Count)
            {
                expTips.Add("Her pozisyonda şirket adını ayrı ve net yazın.");
            }

            var described = c.Experiences.Count(e => e.Description.WordCount() >= 12);
            expScore += (int)Math.Round(20.0 * described / c.Experiences.Count);
            if (described < c.Experiences.Count)
            {
                expTips.Add("Her pozisyonun altına 3-5 madde ile sorumluluk ve başarılarınızı yazın.");
            }
        }

        feedback.Add(new SectionFeedback { Section = "İş deneyimi", Score = Math.Clamp(expScore, 0, 100), Tips = expTips });

        var eduTips = new List<string>();
        var eduScore = 0;
        if (c.Educations.Count == 0)
        {
            eduTips.Add("Eğitim bilgilerinizi (okul, bölüm, mezuniyet yılı) ekleyin.");
        }
        else
        {
            eduScore = 60;
            if (c.Educations.All(e => e.EndDate.HasValue() || e.StartDate.HasValue())) eduScore += 25; else eduTips.Add("Mezuniyet yılınızı ekleyin.");
            if (c.Educations.All(e => e.Degree.HasValue() || e.Field.HasValue())) eduScore += 15; else eduTips.Add("Derece ve bölüm adını belirtin (örn. Lisans, Bilgisayar Mühendisliği).");
        }

        feedback.Add(new SectionFeedback { Section = "Eğitim", Score = eduScore, Tips = eduTips });

        // Uzunluk: 1-2 sayfa (~350-900 kelime) idealdir.
        score += words switch
        {
            >= 300 and <= 950 => 20,
            >= 200 and <= 1300 => 12,
            _ => 4
        };

        return Math.Clamp(score, 0, 100);
    }

    private static int ScoreClarity(ResumeContent c, string text, List<SectionFeedback> feedback)
    {
        var bulletLines = c.Experiences
            .SelectMany(e => (e.Description ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(l => l.Length > 3)
            .ToList();

        var tips = new List<string>();
        double score = 0;

        if (bulletLines.Count == 0)
        {
            score += 20;
            tips.Add("Görev tanımlarınızı madde madde yazın; her madde bir eylem fiiliyle başlasın.");
        }
        else
        {
            var actionRatio = bulletLines.Count(l => ActionVerbs.Any(v => l.ToSearchKey().Contains(v))) / (double)bulletLines.Count;
            score += 35 * Math.Min(1, actionRatio / 0.6);
            if (actionRatio < 0.5)
            {
                tips.Add("Maddeleri \"geliştirdim, yönettim, %20 artırdım\" gibi eylem fiilleriyle başlatın.");
            }

            var quantRatio = bulletLines.Count(l => QuantifiedRegex().IsMatch(l)) / (double)bulletLines.Count;
            score += 25 * Math.Min(1, quantRatio / 0.35);
            if (quantRatio < 0.3)
            {
                tips.Add("Başarılarınızı sayılarla gösterin: süre, yüzde, bütçe, kişi sayısı (örn. \"işlem süresini %35 kısalttım\").");
            }
        }

        var sentences = SentenceSplit().Split(string.Join("\n", new[] { c.Summary ?? string.Empty }.Concat(bulletLines)))
            .Select(s => s.WordCount())
            .Where(n => n > 0)
            .ToList();
        var avg = sentences.Count > 0 ? sentences.Average() : 0;
        score += avg switch
        {
            0 => 5,
            >= 6 and <= 24 => 20,
            > 24 and <= 32 => 12,
            _ => 8
        };
        if (avg > 24)
        {
            tips.Add("Cümleleriniz uzun. Her maddeyi tek bir fikirle 1-2 satırda tutun.");
        }

        var key = text.ToSearchKey();
        var clicheHits = Cliches.Where(key.Contains).ToList();
        score += clicheHits.Count == 0 ? 10 : Math.Max(0, 10 - clicheHits.Count * 3);
        if (clicheHits.Count > 0)
        {
            tips.Add($"Kalıp ifadeler yerine kanıt sunun: \"{string.Join("\", \"", clicheHits.Take(3))}\" yerine bunu gösteren bir başarı yazın.");
        }

        var firstPerson = Regex.Matches(" " + key + " ", @"\s(ben|benim|bana|i|my|me)\s").Count;
        score += firstPerson <= 2 ? 10 : 4;
        if (firstPerson > 2)
        {
            tips.Add("\"Ben/benim\" gibi birinci tekil şahıs zamirlerini azaltın; CV dilinde özne gizlidir.");
        }

        var result = Math.Clamp((int)Math.Round(score), 0, 100);
        feedback.Add(new SectionFeedback { Section = "Dil ve anlatım", Score = result, Tips = tips });
        return result;
    }

    private static int ScoreAts(ResumeContent c, string text, IReadOnlyDictionary<ResumeSection, List<string>> sections, int words,
        CatalogProfession? profession, string prepared, List<AtsCheck> checks)
    {
        void Add(string name, bool passed, string detail) => checks.Add(new AtsCheck { Name = name, Passed = passed, Detail = detail });

        Add("Metin okunabilir", text.Length >= 300,
            text.Length >= 300 ? "Dosyadaki metin sorunsuz çıkarıldı." : "Çok az metin çıkarıldı; görsel tabanlı PDF ATS'de okunamaz.");
        Add("Ad soyad", c.Personal.FullName.HasValue(), c.Personal.FullName.HasValue() ? c.Personal.FullName! : "Ad soyad tespit edilemedi; en üst satıra yazın.");
        Add("E-posta", c.Personal.Email.HasValue(), c.Personal.Email ?? "E-posta adresi bulunamadı.");
        Add("Telefon", c.Personal.Phone.HasValue(), c.Personal.Phone ?? "Telefon numarası bulunamadı.");

        var headingCount = new[] { ResumeSection.Experience, ResumeSection.Education, ResumeSection.Skills, ResumeSection.Summary }.Count(sections.ContainsKey);
        var manual = headingCount == 0 && c.Experiences.Count > 0;
        Add("Standart bölüm başlıkları", headingCount >= 3 || manual,
            headingCount >= 3 || manual ? "Deneyim, eğitim ve beceri başlıkları tanındı." : "\"İş Deneyimi\", \"Eğitim\", \"Beceriler\" gibi standart başlıklar kullanın; yaratıcı başlıklar ATS'yi şaşırtır.");

        var datedRatio = c.Experiences.Count == 0 ? 0 : c.Experiences.Count(e => e.StartDate.HasValue()) / (double)c.Experiences.Count;
        Add("Tarihler algılanabilir", datedRatio >= 0.7,
            datedRatio >= 0.7 ? "Pozisyon tarihleri okunabiliyor." : "Tarihleri \"03/2021 - 05/2024\" veya \"Mart 2021 - Günümüz\" biçiminde yazın.");

        Add("Uygun uzunluk", words is >= 200 and <= 1300,
            words < 200 ? $"{words} kelime: içerik az." : words > 1300 ? $"{words} kelime: 2 sayfayı geçmemeye çalışın." : $"{words} kelime, ideal aralıkta.");

        var odd = OddChars().Matches(text).Count;
        var oddRatio = text.Length == 0 ? 0 : odd / (double)text.Length;
        Add("Simge ve özel karakter", oddRatio < 0.02,
            oddRatio < 0.02 ? "Metin temiz; simge kullanımı düşük." : "Çok sayıda simge/ikon var; ATS bunları bozuk karakter olarak okuyabilir.");

        Add("LinkedIn profili", c.Personal.LinkedIn.HasValue(), c.Personal.LinkedIn ?? "LinkedIn bağlantısı eklemeniz önerilir.");

        if (profession is not null)
        {
            var coreSkills = profession.Skills.Where(s => s.Importance == SkillImportance.Core).ToList();
            var coreFound = coreSkills.Count(s => SkillMatcher.ContainsAny(prepared, s.Keys));
            var ok = coreSkills.Count == 0 || coreFound * 2 >= coreSkills.Count;
            Add("Anahtar kelimeler", ok, $"{profession.Name} için {coreSkills.Count} temel beceriden {coreFound} tanesi CV'de geçiyor.");
        }

        var weights = new Dictionary<string, int>
        {
            ["Metin okunabilir"] = 20, ["Ad soyad"] = 8, ["E-posta"] = 12, ["Telefon"] = 10, ["Standart bölüm başlıkları"] = 15,
            ["Tarihler algılanabilir"] = 10, ["Uygun uzunluk"] = 6, ["Simge ve özel karakter"] = 7, ["LinkedIn profili"] = 2, ["Anahtar kelimeler"] = 10
        };

        var total = checks.Sum(x => weights.GetValueOrDefault(x.Name, 5));
        var passed = checks.Where(x => x.Passed).Sum(x => weights.GetValueOrDefault(x.Name, 5));
        return total == 0 ? 0 : (int)Math.Round(passed * 100.0 / total);
    }

    private static void AddSkillsFeedback(ResumeContent c, CatalogProfession? profession, List<CatalogSkill> missing, List<string> detected, List<SectionFeedback> feedback)
    {
        var tips = new List<string>();
        var score = Math.Min(60, c.Skills.Count * 6);
        if (c.Skills.Count < 6)
        {
            tips.Add("Beceriler bölümünde en az 8-12 somut beceri listeleyin (araç, teknoloji, yöntem).");
        }
        else if (c.Skills.Count > 40)
        {
            tips.Add("Çok fazla beceri var; ilana en uygun 15-20 tanesini öne çıkarın.");
        }

        if (profession is not null)
        {
            var coreMissing = missing.Where(m => m.Importance == SkillImportance.Core).Select(m => m.Name).Take(5).ToList();
            score += coreMissing.Count == 0 ? 40 : Math.Max(0, 40 - coreMissing.Count * 10);
            if (coreMissing.Count > 0)
            {
                tips.Add($"{profession.Name} ilanlarında aranan şu temel becerileri biliyorsanız mutlaka ekleyin: {string.Join(", ", coreMissing)}.");
            }

            if (detected.Count > 0 && c.Skills.Count > 0)
            {
                var inSkillSection = SkillMatcher.PrepareText(string.Join(", ", c.Skills));
                var onlyInText = profession.Skills.Where(s => detected.Contains(s.Name) && !SkillMatcher.ContainsAny(inSkillSection, s.Keys)).Select(s => s.Name).Take(4).ToList();
                if (onlyInText.Count > 0)
                {
                    tips.Add($"Deneyimlerde geçen ama beceriler listesinde olmayanlar: {string.Join(", ", onlyInText)}. Listeye de ekleyin.");
                }
            }
        }
        else
        {
            score += 20;
        }

        if (c.Languages.Count == 0)
        {
            tips.Add("Yabancı dil bilginizi seviyesiyle ekleyin (örn. İngilizce - B2).");
        }

        feedback.Add(new SectionFeedback { Section = "Beceriler ve diller", Score = Math.Clamp(score, 0, 100), Tips = tips });
    }

    private static List<string> BuildStrengths(ResumeContent c, AnalysisDetails d, CatalogProfession? profession, List<string> detected, double years, int ats, int clarity)
    {
        var list = new List<string>();
        if (years >= 1)
        {
            list.Add($"Toplam yaklaşık {years:0.#} yıllık iş deneyimi.");
        }

        if (profession is not null && detected.Count > 0)
        {
            list.Add($"{profession.Name} için {profession.Skills.Count} beceriden {detected.Count} tanesi CV'nizde mevcut.");
        }

        if (ats >= 80)
        {
            list.Add("ATS (aday takip sistemi) tarafından rahatça okunabilir bir yapı.");
        }

        if (clarity >= 75)
        {
            list.Add("Eylem odaklı ve ölçülebilir anlatım.");
        }

        if (c.Certificates.Count > 0)
        {
            list.Add($"{c.Certificates.Count} sertifika/kurs ile sürekli öğrenme gösteriliyor.");
        }

        if (c.Languages.Any(l => l.Level >= LanguageLevel.Iyi && l.Name != "Türkçe"))
        {
            list.Add("İyi seviyede yabancı dil bilgisi.");
        }

        if (d.Sections.Count(s => s.Score >= 85) >= 3)
        {
            list.Add("Birçok bölüm eksiksiz ve iyi düzenlenmiş.");
        }

        return list;
    }

    private static List<string> BuildImprovements(AnalysisDetails d, List<CatalogSkill> missing)
    {
        var list = d.Sections.Where(s => s.Score < 85).SelectMany(s => s.Tips.Take(2)).Take(6).ToList();
        foreach (var check in d.AtsChecks.Where(a => !a.Passed && a.Name != "LinkedIn profili").Take(2))
        {
            if (check.Detail is not null && !list.Contains(check.Detail))
            {
                list.Add(check.Detail);
            }
        }

        var core = missing.Where(m => m.Importance == SkillImportance.Core).Take(3).Select(m => m.Name).ToList();
        if (core.Count > 0)
        {
            list.Add($"Öncelikli öğrenme listesi: {string.Join(", ", core)}.");
        }

        return list.Distinct().Take(8).ToList();
    }
}
