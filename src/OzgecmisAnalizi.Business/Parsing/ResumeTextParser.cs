using System.Text.RegularExpressions;
using OzgecmisAnalizi.Core.Extensions;
using OzgecmisAnalizi.Entities.Content;
using OzgecmisAnalizi.Entities.Enums;

namespace OzgecmisAnalizi.Business.Parsing;

public enum ResumeSection
{
    Header,
    Summary,
    Experience,
    Education,
    Skills,
    Languages,
    Certificates,
    Projects,
    Ignored
}

public interface IResumeTextParser
{
    ResumeContent Parse(string text);

    /// <summary>Metindeki bölüm başlıklarını ve satırlarını döndürür (analiz için de kullanılır).</summary>
    IReadOnlyDictionary<ResumeSection, List<string>> SplitSections(string text);
}

/// <summary>
/// Sezgisel CV ayrıştırıcı: iletişim bilgileri, bölüm başlıkları, tarih aralıkları ve madde işaretleri üzerinden
/// düz metni düzenlenebilir <see cref="ResumeContent"/> yapısına çevirir. Sonuç kullanıcı tarafından düzeltilebilir.
/// </summary>
public sealed partial class ResumeTextParser : IResumeTextParser
{
    private static readonly (ResumeSection Section, string[] Keys)[] Headings =
    [
        (ResumeSection.Summary, ["ozet", "hakkimda", "profil", "profesyonel ozet", "kariyer hedefi", "kariyer ozeti", "summary", "about me", "about", "profile", "professional summary", "objective"]),
        (ResumeSection.Experience, ["is deneyimi", "is deneyimleri", "deneyim", "deneyimler", "is tecrubesi", "tecrube", "tecrubeler", "calisma gecmisi", "mesleki deneyim", "experience", "work experience", "professional experience", "employment history", "employment"]),
        (ResumeSection.Education, ["egitim", "egitim bilgileri", "egitim durumu", "ogrenim", "education", "academic background"]),
        (ResumeSection.Skills, ["yetenekler", "beceriler", "yetkinlikler", "teknik beceriler", "teknik yetkinlikler", "bilgisayar bilgisi", "uzmanlik alanlari", "skills", "technical skills", "core competencies", "competencies", "expertise"]),
        (ResumeSection.Languages, ["yabanci dil", "yabanci diller", "diller", "dil bilgisi", "languages", "language skills"]),
        (ResumeSection.Certificates, ["sertifikalar", "sertifika", "kurslar", "kurs ve sertifikalar", "egitim ve sertifikalar", "sertifika ve kurslar", "certificates", "certifications", "courses", "licenses"]),
        (ResumeSection.Projects, ["projeler", "projelerim", "projects", "portfolio", "portfolyo"]),
        (ResumeSection.Ignored, ["referanslar", "referans", "hobiler", "ilgi alanlari", "kisisel bilgiler", "references", "hobbies", "interests", "personal information", "gonullu calismalar", "volunteering"])
    ];

    [GeneratedRegex(@"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}")]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"(?:\+?90[\s\-]?)?\(?0?5\d{2}\)?[\s\-]?\d{3}[\s\-]?\d{2}[\s\-]?\d{2}|\+\d{1,3}[\s\-]?\d{2,4}[\s\-]?\d{3}[\s\-]?\d{2,4}")]
    private static partial Regex PhoneRegex();

    [GeneratedRegex(@"(?:https?://)?(?:[a-z]{2,3}\.)?linkedin\.com/in/[A-Za-z0-9\-_%]+/?", RegexOptions.IgnoreCase)]
    private static partial Regex LinkedInRegex();

    [GeneratedRegex(@"(?:https?://)?(?:www\.)?(?:github\.com/[A-Za-z0-9\-_]+|[a-z0-9\-]+\.(?:dev|io|me|com\.tr|com)(?:/[^\s]*)?)", RegexOptions.IgnoreCase)]
    private static partial Regex WebsiteRegex();

    [GeneratedRegex(@"^[\s•\-\*–·▪►✓o]+\s*")]
    private static partial Regex BulletPrefix();

    [GeneratedRegex(@"^[\p{L}][\p{L}'’\.\-]+(?:\s+[\p{L}][\p{L}'’\.\-]+){1,3}$")]
    private static partial Regex NameLike();

    public IReadOnlyDictionary<ResumeSection, List<string>> SplitSections(string text)
    {
        var result = new Dictionary<ResumeSection, List<string>> { [ResumeSection.Header] = [] };
        var current = ResumeSection.Header;

        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            var heading = DetectHeading(line);
            if (heading is not null)
            {
                current = heading.Value;
                if (!result.ContainsKey(current))
                {
                    result[current] = [];
                }

                continue;
            }

            result[current].Add(line);
        }

        return result;
    }

    public ResumeContent Parse(string text)
    {
        var sections = SplitSections(text);
        var content = new ResumeContent();

        ParseContact(text, sections[ResumeSection.Header], content.Personal);

        if (sections.TryGetValue(ResumeSection.Summary, out var summary))
        {
            content.Summary = string.Join(" ", summary.Select(StripBullet)).Truncate(1500);
        }
        else
        {
            // Başlık bölümündeki uzun paragraf genellikle özettir.
            var paragraph = sections[ResumeSection.Header].FirstOrDefault(l => l.Length > 120 && !EmailRegex().IsMatch(l));
            content.Summary = paragraph;
        }

        if (sections.TryGetValue(ResumeSection.Experience, out var exp))
        {
            content.Experiences = ParseExperiences(exp);
        }

        if (sections.TryGetValue(ResumeSection.Education, out var edu))
        {
            content.Educations = ParseEducations(edu);
        }

        if (sections.TryGetValue(ResumeSection.Skills, out var skills))
        {
            content.Skills = ParseSkills(skills);
        }

        if (sections.TryGetValue(ResumeSection.Languages, out var langs))
        {
            content.Languages = ParseLanguages(langs);
        }

        if (sections.TryGetValue(ResumeSection.Certificates, out var certs))
        {
            content.Certificates = certs.Select(StripBullet)
                .Where(l => l.Length is > 2 and < 200)
                .Take(30)
                .Select(l => new CertificateItem { Name = l, Date = ResumeDates.RangeRegex().Match(l).Success ? null : ExtractYear(l) })
                .ToList();
        }

        if (sections.TryGetValue(ResumeSection.Projects, out var projects))
        {
            content.Projects = ParseProjects(projects);
        }

        return content;
    }

    private static ResumeSection? DetectHeading(string line)
    {
        if (line.Length > 45)
        {
            return null;
        }

        var key = line.TrimEnd(':', '.', ' ').ToSearchKey();
        foreach (var (section, keys) in Headings)
        {
            if (keys.Any(k => key == k))
            {
                return section;
            }
        }

        return null;
    }

    private static void ParseContact(string fullText, List<string> headerLines, PersonalInfo personal)
    {
        personal.Email = EmailRegex().Match(fullText).Value.NullIfEmpty();
        personal.Phone = PhoneRegex().Match(fullText).Value.Trim().NullIfEmpty();
        var linkedin = LinkedInRegex().Match(fullText).Value;
        personal.LinkedIn = linkedin.NullIfEmpty();

        foreach (Match m in WebsiteRegex().Matches(fullText))
        {
            if (!m.Value.Contains("linkedin", StringComparison.OrdinalIgnoreCase) && !m.Value.Contains('@')
                && (personal.Email is null || !personal.Email.Contains(m.Value, StringComparison.OrdinalIgnoreCase)))
            {
                personal.Website = m.Value;
                break;
            }
        }

        var candidates = headerLines.Take(8).ToList();
        var nameIndex = candidates.FindIndex(l => NameLike().IsMatch(l) && DetectHeading(l) is null && l.Length <= 40);
        if (nameIndex >= 0)
        {
            personal.FullName = candidates[nameIndex].ToTitleCaseTr();
            var titleLine = candidates.Skip(nameIndex + 1)
                .FirstOrDefault(l => l.Length is > 3 and < 70 && !EmailRegex().IsMatch(l) && !PhoneRegex().IsMatch(l) && !l.Contains("http"));
            personal.Title = titleLine;
        }

        var cityLine = candidates.FirstOrDefault(l => TurkishCities.Any(c => l.ToSearchKey().Contains(c.ToSearchKey())));
        if (cityLine is not null)
        {
            personal.City = TurkishCities.First(c => cityLine.ToSearchKey().Contains(c.ToSearchKey()));
        }
    }

    private static List<ExperienceItem> ParseExperiences(List<string> lines)
    {
        var items = new List<ExperienceItem>();
        ExperienceItem? current = null;
        var headers = new List<string>();
        var description = new List<string>();

        void Flush()
        {
            if (current is null)
            {
                return;
            }

            ApplyHeaders(current, headers);
            current.Description = description.Count > 0 ? string.Join("\n", description.Select(d => "• " + d)) : null;
            if (current.Position.HasValue() || current.Company.HasValue())
            {
                items.Add(current);
            }

            current = null;
            headers = [];
            description = [];
        }

        foreach (var raw in lines)
        {
            var range = ResumeDates.RangeRegex().Match(raw);
            if (range.Success)
            {
                var remainder = raw.Remove(range.Index, range.Length).Trim(' ', '|', '-', '–', ',', '(', ')');
                if (current is not null && current.StartDate is null && description.Count == 0)
                {
                    SetDates(current, range);
                    if (remainder.Length > 0)
                    {
                        headers.Add(remainder);
                    }

                    continue;
                }

                // Önceki girdinin son kısa açıklama satırları aslında yeni girdinin başlığıdır.
                var carried = new List<string>();
                while (description.Count > 0 && carried.Count < 2 && LooksLikeHeader(description[^1]))
                {
                    carried.Insert(0, description[^1]);
                    description.RemoveAt(description.Count - 1);
                }

                Flush();
                current = new ExperienceItem();
                SetDates(current, range);
                headers.AddRange(carried);
                if (remainder.Length > 0)
                {
                    headers.Add(remainder);
                }

                continue;
            }

            var isBullet = BulletPrefix().IsMatch(raw);
            var line = StripBullet(raw);
            if (current is null)
            {
                current = new ExperienceItem();
            }

            if (!isBullet && headers.Count < 2 && description.Count == 0 && LooksLikeHeader(line))
            {
                headers.Add(line);
            }
            else
            {
                description.Add(line);
            }
        }

        Flush();
        return items.Take(20).ToList();
    }

    private static void ApplyHeaders(ExperienceItem item, List<string> headers)
    {
        if (headers.Count == 0)
        {
            return;
        }

        var first = headers[0];
        var parts = SplitHeader(first);
        if (parts.Length >= 2)
        {
            item.Position = parts[0];
            item.Company = parts[1];
            if (parts.Length > 2)
            {
                item.Location = parts[2];
            }
        }
        else
        {
            item.Position = first;
        }

        if (headers.Count > 1)
        {
            var second = SplitHeader(headers[1]);
            if (item.Company is null)
            {
                item.Company = second[0];
                if (second.Length > 1)
                {
                    item.Location = second[1];
                }
            }
            else
            {
                item.Location ??= headers[1];
            }
        }
    }

    private static void SetDates(ExperienceItem item, Match range)
    {
        item.StartDate = range.Groups["start"].Value.Trim();
        var end = range.Groups["end"].Value.Trim();
        item.IsCurrent = ResumeDates.IsCurrent(end);
        item.EndDate = item.IsCurrent ? null : end;
    }

    private static List<EducationItem> ParseEducations(List<string> lines)
    {
        var items = new List<EducationItem>();
        EducationItem? current = null;

        foreach (var raw in lines)
        {
            var line = StripBullet(raw);
            var range = ResumeDates.RangeRegex().Match(line);
            var key = line.ToSearchKey();
            var isSchool = SchoolWords.Any(w => key.Contains(w));

            if (isSchool && (current is null || current.School is not null))
            {
                current = new EducationItem();
                items.Add(current);
            }

            current ??= AddNew(items);

            if (range.Success)
            {
                current.StartDate = range.Groups["start"].Value.Trim();
                var end = range.Groups["end"].Value.Trim();
                current.EndDate = ResumeDates.IsCurrent(end) ? "Devam ediyor" : end;
                line = line.Remove(range.Index, range.Length).Trim(' ', '|', '-', '–', ',', '(', ')');
                if (line.Length == 0)
                {
                    continue;
                }

                key = line.ToSearchKey();
            }

            if (isSchool && current.School is null)
            {
                var parts = SplitHeader(line);
                current.School = parts[0];
                if (parts.Length > 1)
                {
                    current.Field ??= parts[1];
                }
            }
            else if (DegreeWords.Any(w => key.Contains(w)) && current.Degree is null)
            {
                var parts = SplitHeader(line);
                current.Degree = parts[0];
                if (parts.Length > 1)
                {
                    current.Field ??= parts[1];
                }
            }
            else if ((key.Contains("not ortalamasi") || key.Contains("gpa") || key.Contains("ortalama")) && current.Grade is null)
            {
                current.Grade = line;
            }
            else if (current.Field is null && line.Length < 90)
            {
                current.Field = line;
            }
        }

        return items.Where(e => e.School.HasValue() || e.Degree.HasValue()).Take(8).ToList();

        static EducationItem AddNew(List<EducationItem> list)
        {
            var item = new EducationItem();
            list.Add(item);
            return item;
        }
    }

    private static List<string> ParseSkills(List<string> lines)
    {
        var separators = new[] { ',', ';', '|', '•', '·', '/', '\t' };
        return lines
            .Select(l => l.Contains(':') && l.IndexOf(':') < 30 ? l[(l.IndexOf(':') + 1)..] : l)
            .SelectMany(l => l.Split(separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(StripBullet)
            .Where(s => s.Length is > 1 and <= 40)
            .DistinctBySearchKey()
            .Take(60)
            .ToList();
    }

    private static List<LanguageItem> ParseLanguages(List<string> lines)
    {
        var result = new List<LanguageItem>();
        foreach (var raw in lines)
        {
            var line = StripBullet(raw);
            foreach (var part in line.Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var key = part.ToSearchKey();
                var name = KnownLanguages.FirstOrDefault(l => key.Contains(l.Key));
                if (name.Value is null)
                {
                    continue;
                }

                result.Add(new LanguageItem { Name = name.Value, Level = DetectLanguageLevel(key) });
            }
        }

        return result.GroupBy(l => l.Name).Select(g => g.OrderByDescending(x => x.Level).First()).Take(8).ToList();
    }

    private static LanguageLevel DetectLanguageLevel(string key)
    {
        if (key.Contains("ana dil") || key.Contains("anadil") || key.Contains("native")) return LanguageLevel.AnaDil;
        if (key.Contains("c2") || key.Contains("c1") || key.Contains("ileri") || key.Contains("advanced") || key.Contains("fluent") || key.Contains("akici")) return LanguageLevel.Ileri;
        if (key.Contains("b2") || key.Contains("iyi") || key.Contains("upper")) return LanguageLevel.Iyi;
        if (key.Contains("a1") || key.Contains("a2") || key.Contains("baslangic") || key.Contains("beginner") || key.Contains("temel")) return LanguageLevel.Baslangic;
        return LanguageLevel.Orta;
    }

    private static List<ProjectItem> ParseProjects(List<string> lines)
    {
        var result = new List<ProjectItem>();
        ProjectItem? current = null;
        foreach (var raw in lines)
        {
            var isBullet = BulletPrefix().IsMatch(raw);
            var line = StripBullet(raw);
            if (!isBullet && LooksLikeHeader(line))
            {
                current = new ProjectItem { Name = line };
                var url = WebsiteRegex().Match(line);
                if (url.Success)
                {
                    current.Url = url.Value;
                }

                result.Add(current);
            }
            else if (current is not null)
            {
                current.Description = current.Description is null ? line : current.Description + " " + line;
            }
            else
            {
                current = new ProjectItem { Name = line.Truncate(80) };
                result.Add(current);
            }
        }

        return result.Take(10).ToList();
    }

    private static bool LooksLikeHeader(string line) =>
        line.Length is > 1 and <= 80 && !line.EndsWith('.') && line.WordCount() <= 9;

    private static string[] SplitHeader(string line) =>
        line.Split([" | ", " - ", " – ", " — ", " @ ", ", ", " / "], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string StripBullet(string line) => BulletPrefix().Replace(line, string.Empty).Trim();

    private static string? ExtractYear(string line)
    {
        var m = Regex.Match(line, @"(?:19|20)\d{2}");
        return m.Success ? m.Value : null;
    }

    private static readonly string[] SchoolWords = ["universite", "university", "lise", "high school", "fakulte", "faculty", "yuksekokul", "meslek yuksek", "college", "enstitu", "institute", "akademi", "anadolu lisesi"];
    private static readonly string[] DegreeWords = ["lisans", "yuksek lisans", "doktora", "on lisans", "onlisans", "bachelor", "master", "phd", "mba", "bsc", "msc", "mezun", "bolumu", "muhendisligi", "ogretmenligi"];

    private static readonly Dictionary<string, string> KnownLanguages = new()
    {
        ["ingilizce"] = "İngilizce", ["english"] = "İngilizce",
        ["almanca"] = "Almanca", ["german"] = "Almanca",
        ["fransizca"] = "Fransızca", ["french"] = "Fransızca",
        ["ispanyolca"] = "İspanyolca", ["spanish"] = "İspanyolca",
        ["italyanca"] = "İtalyanca", ["italian"] = "İtalyanca",
        ["rusca"] = "Rusça", ["russian"] = "Rusça",
        ["arapca"] = "Arapça", ["arabic"] = "Arapça",
        ["japonca"] = "Japonca", ["japanese"] = "Japonca",
        ["cince"] = "Çince", ["chinese"] = "Çince",
        ["turkce"] = "Türkçe", ["turkish"] = "Türkçe",
        ["korece"] = "Korece", ["portekizce"] = "Portekizce", ["felemenkce"] = "Felemenkçe"
    };

    public static readonly string[] TurkishCities =
    [
        "İstanbul", "Ankara", "İzmir", "Bursa", "Kocaeli", "Antalya", "Konya", "Adana", "Gaziantep", "Kayseri",
        "Eskişehir", "Mersin", "Sakarya", "Tekirdağ", "Denizli", "Samsun", "Trabzon", "Manisa", "Muğla", "Aydın",
        "Balıkesir", "Malatya", "Diyarbakır", "Şanlıurfa", "Hatay", "Erzurum", "Edirne", "Çanakkale", "Bolu", "Düzce"
    ];
}

internal static class ParsingStringExtensions
{
    public static string? NullIfEmpty(this string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
