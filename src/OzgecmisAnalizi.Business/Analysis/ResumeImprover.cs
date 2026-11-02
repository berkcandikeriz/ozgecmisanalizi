using System.Globalization;
using System.Text.RegularExpressions;
using OzgecmisAnalizi.Business.Parsing;
using OzgecmisAnalizi.Core.Extensions;
using OzgecmisAnalizi.Entities.Concrete;
using OzgecmisAnalizi.Entities.Content;
using OzgecmisAnalizi.Entities.Dtos;
using OzgecmisAnalizi.Entities.Enums;

namespace OzgecmisAnalizi.Business.Analysis;

public sealed record ImprovementResult(List<ResumeChangeDto> Changes, List<string> ManualTodos);

public interface IResumeImprover
{
    /// <summary>Analiz önerilerini CV'ye kural tabanlı olarak uygular; değişiklikleri ve elle yapılması gerekenleri döndürür.</summary>
    ImprovementResult Improve(ResumeContent content, string fullText, ResumeAnalysis? analysis, CatalogProfession? profession);

    /// <summary>Seçilen değişiklikleri içeriğe uygular. Yollar orijinal sıra numaralarına göredir; yeniden sıralama en son yapılır.</summary>
    int Apply(ResumeContent content, IEnumerable<ChangePatch> patches);
}

/// <summary>
/// "Önerileri uygula" motoru. Yalnızca CV'de zaten var olan bilgiyi düzenler: biçim, sıra, madde yapısı, kalıp ifadeler,
/// metinde geçip beceri listesinde olmayan beceriler. Kişinin yapmadığı bir başarıyı veya sayıyı asla eklemez;
/// bunlar "elle yapılacaklar" listesine yazılır.
/// </summary>
public sealed partial class ResumeImprover : IResumeImprover
{
    private const string Rule = "Kural motoru";
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    private static readonly string[] Cliches =
    [
        "takım oyuncusu", "takım çalışmasına yatkın", "motivasyonu yüksek", "dinamik bir", "sorumluluk sahibi", "öğrenmeye açık",
        "detaycı", "çok çalışkan", "stres altında çalışabilen", "hard working", "team player", "self motivated", "detail oriented",
        "problem çözme yeteneği yüksek", "iletişimi güçlü"
    ];

    [GeneratedRegex(@"(?<=[.!?])\s+(?=[A-ZÇĞİÖŞÜ0-9])")]
    private static partial Regex SentenceBoundary();

    [GeneratedRegex(@"^(?:ben|benim|ayrıca|bunun yanında|bu rolde)\s+", RegexOptions.IgnoreCase)]
    private static partial Regex WeakOpening();

    [GeneratedRegex(@"\d")]
    private static partial Regex HasDigit();

    public ImprovementResult Improve(ResumeContent content, string fullText, ResumeAnalysis? analysis, CatalogProfession? profession)
    {
        var changes = new List<ResumeChangeDto>();
        var todos = new List<string>();
        var n = 0;
        string NextId() => $"c{++n}";

        void Add(string section, string label, string path, string kind, string format, string? before, string? after, string reason) =>
            changes.Add(new ResumeChangeDto(NextId(), section, label, path, kind, format, before, after, reason, Rule));

        void AddWithTip(string section, string label, string path, string kind, string? before, string? after, string reason, string? tip) =>
            changes.Add(new ResumeChangeDto(NextId(), section, label, path, kind, "text", before, after, reason, Rule, tip));

        var p = content.Personal;

        // --- Kişisel bilgiler
        if (!p.Title.HasValue() && profession is not null)
        {
            var title = profession.Name.Split('(')[0].Trim();
            Add("Kişisel bilgiler", "Unvan", "personal.title", "added", "text", null, title,
                "Adınızın altında hedef unvan olması, ATS'nin ve İK uzmanının CV'yi doğru pozisyonla eşleştirmesini sağlar.");
        }

        if (p.FullName.HasValue() && p.FullName == p.FullName!.ToUpper(Tr) && p.FullName.Length > 3)
        {
            Add("Kişisel bilgiler", "Ad soyad", "personal.fullName", "modified", "text", p.FullName, p.FullName.ToTitleCaseTr(),
                "Tamamı büyük harfle yazılmış ad bazı ATS'lerde yanlış ayrıştırılır.");
        }

        // --- Özet
        var summaryChange = ImproveSummary(content, profession, out var summaryReason);
        if (summaryChange is null && content.Summary.HasValue())
        {
            var (polished, polishNotes) = BulletRewriter.Polish(content.Summary!);
            if (polishNotes.Count > 0)
            {
                summaryChange = polished;
                summaryReason = string.Join(" ", polishNotes);
            }
        }
        else if (summaryChange is not null)
        {
            summaryChange = BulletRewriter.Polish(summaryChange).Text;
        }
        if (summaryChange is not null && summaryChange != (content.Summary ?? string.Empty).Trim())
        {
            Add("Özet", "Profesyonel özet", "summary", content.Summary.HasValue() ? "modified" : "added", "text",
                content.Summary, summaryChange, summaryReason);
        }

        // --- Deneyimler: madde yapısı ve tarih biçimi
        for (var i = 0; i < content.Experiences.Count; i++)
        {
            var e = content.Experiences[i];
            var label = string.Join(" · ", new[] { e.Position, e.Company }.Where(x => x.HasValue()));
            var rewrite = RewriteDescription(e.Description, e.IsCurrent);
            if (rewrite is not null && Canonical(rewrite.Value.Text) != Canonical(e.Description))
            {
                AddWithTip("İş deneyimi", $"{label} — açıklama", $"experience:{i}:description", "modified", e.Description, rewrite.Value.Text,
                    rewrite.Value.Reason, rewrite.Value.Tip);
            }
            else if (rewrite?.Tip is { } onlyTip)
            {
                todos.Add($"{label}: {onlyTip}");
            }

            NormalizeDateChange(e.StartDate, $"experience:{i}:startDate", $"{label} — başlangıç", "İş deneyimi", Add);
            if (!e.IsCurrent)
            {
                NormalizeDateChange(e.EndDate, $"experience:{i}:endDate", $"{label} — bitiş", "İş deneyimi", Add);
            }
        }

        // --- Deneyim sırası (en yeniden eskiye)
        var order = content.Experiences
            .Select((e, i) => (Index: i, Start: ResumeDates.Parse(e.StartDate), e.IsCurrent))
            .OrderByDescending(x => x.IsCurrent)
            .ThenByDescending(x => x.Start ?? DateOnly.MinValue)
            .Select(x => x.Index)
            .ToList();
        // Yalnızca tüm tarihler okunabiliyorsa önerilir; tarihi eksik bir pozisyon yüzünden yanlış sıralama yapılmasın.
        var allDated = content.Experiences.All(e => e.IsCurrent || ResumeDates.Parse(e.StartDate) is not null);
        if (content.Experiences.Count > 1 && allDated && !order.SequenceEqual(Enumerable.Range(0, content.Experiences.Count)))
        {
            string Describe(IEnumerable<int> idx) => string.Join("\n", idx.Select(i =>
            {
                var e = content.Experiences[i];
                return $"{e.Position} · {e.Company} ({e.StartDate} – {(e.IsCurrent ? "Günümüz" : e.EndDate)})";
            }));

            // Yeni sıra yolun içinde taşınır: experienceOrder:2,0,1
            Add("İş deneyimi", "Pozisyon sırası", $"experienceOrder:{string.Join(",", order)}", "reordered", "order",
                Describe(Enumerable.Range(0, content.Experiences.Count)), Describe(order),
                "ATS'ler ve İK uzmanları en güncel deneyimi en üstte bekler.");
        }

        // --- Eğitim tarihleri
        for (var i = 0; i < content.Educations.Count; i++)
        {
            var ed = content.Educations[i];
            var label = ed.School ?? "Eğitim";
            NormalizeDateChange(ed.StartDate, $"education:{i}:startDate", $"{label} — başlangıç", "Eğitim", Add);
            NormalizeDateChange(ed.EndDate, $"education:{i}:endDate", $"{label} — bitiş", "Eğitim", Add);
        }

        // --- Beceriler: yazımı standartlaştır, metinde geçenleri ekle, temel becerileri öne al
        var skillsAfter = ImproveSkills(content, fullText, profession, out var added, out var renamed);
        if (!skillsAfter.SequenceEqual(content.Skills))
        {
            var reasons = new List<string>();
            if (added.Count > 0) reasons.Add($"Deneyim ve projelerinizde geçen ama beceri listesinde olmayanlar eklendi: {string.Join(", ", added)}.");
            if (renamed.Count > 0) reasons.Add($"Yazım ilanlardaki standart biçime getirildi: {string.Join(", ", renamed)}.");
            reasons.Add("Mesleğin temel becerileri listenin başına alındı; ATS ilk becerilere daha çok ağırlık verir.");
            Add("Beceriler", "Beceri listesi", "skills", content.Skills.Count == 0 ? "added" : "modified", "list",
                string.Join(", ", content.Skills), string.Join(", ", skillsAfter), string.Join(" ", reasons));
        }

        // --- Elle yapılması gerekenler (uydurulamayacak bilgiler)
        if (!p.Phone.HasValue()) todos.Add("Telefon numaranızı ekleyin (+90 5xx xxx xx xx).");
        if (!p.Email.HasValue()) todos.Add("E-posta adresinizi ekleyin.");
        if (!p.LinkedIn.HasValue()) todos.Add("LinkedIn profil adresinizi açık metin olarak ekleyin (linkedin.com/in/kullanici-adi).");
        if (!p.City.HasValue()) todos.Add("Bulunduğunuz şehri ekleyin.");
        if (content.Languages.Count == 0) todos.Add("Yabancı dil bilginizi seviyesiyle ekleyin (örn. İngilizce – B2).");

        var unquantified = content.Experiences
            .SelectMany(e => (e.Description ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(b => (e.Company, Bullet: b.TrimStart('•', '-', ' '))))
            .Where(x => x.Bullet.Length > 15 && !HasDigit().IsMatch(x.Bullet))
            .Take(4)
            .ToList();
        foreach (var (company, bullet) in unquantified)
        {
            todos.Add($"{(company.HasValue() ? company + ": " : string.Empty)}\"{bullet.Truncate(90)}\" maddesine ölçülebilir bir sonuç ekleyin (süre, yüzde, kişi veya iş sayısı).");
        }

        if (analysis is not null)
        {
            var coreMissing = analysis.Details.MissingSkills.Where(m => m.Importance == "Olmazsa olmaz").Select(m => m.Skill).Take(5).ToList();
            if (coreMissing.Count > 0)
            {
                todos.Add($"Biliyorsanız beceriler bölümüne ekleyin: {string.Join(", ", coreMissing)}. Bilmiyorsanız CV'ye yazmayın; kariyer yolunda öğrenme önerileri var.");
            }
        }

        return new ImprovementResult(changes, todos);
    }

    public int Apply(ResumeContent content, IEnumerable<ChangePatch> patches)
    {
        var applied = 0;
        string? order = null;

        foreach (var patch in patches)
        {
            var path = patch.Path?.Trim() ?? string.Empty;
            var value = patch.After?.Trim();
            var parts = path.Split(':');

            switch (parts[0])
            {
                case "personal.title": content.Personal.Title = value; applied++; break;
                case "personal.fullName": content.Personal.FullName = value; applied++; break;
                case "summary": content.Summary = value; applied++; break;
                case "skills":
                    content.Skills = (value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).DistinctBySearchKey();
                    applied++;
                    break;
                case "experienceOrder": order = parts.Length > 1 ? parts[1] : value; break;
                case "experience" when parts.Length == 3 && int.TryParse(parts[1], out var ei) && ei >= 0 && ei < content.Experiences.Count:
                    var exp = content.Experiences[ei];
                    switch (parts[2])
                    {
                        case "description": exp.Description = value; applied++; break;
                        case "startDate": exp.StartDate = value; applied++; break;
                        case "endDate": exp.EndDate = value; applied++; break;
                    }

                    break;
                case "education" when parts.Length == 3 && int.TryParse(parts[1], out var di) && di >= 0 && di < content.Educations.Count:
                    var edu = content.Educations[di];
                    switch (parts[2])
                    {
                        case "startDate": edu.StartDate = value; applied++; break;
                        case "endDate": edu.EndDate = value; applied++; break;
                    }

                    break;
            }
        }

        // Sıra en son uygulanır: diğer yollar orijinal sıra numaralarını kullanır.
        if (order is not null)
        {
            var idx = order.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => int.TryParse(s, out var v) ? v : -1).ToList();
            if (idx.Count == content.Experiences.Count && idx.OrderBy(x => x).SequenceEqual(Enumerable.Range(0, idx.Count)))
            {
                content.Experiences = idx.Select(i => content.Experiences[i]).ToList();
                applied++;
            }
        }

        return applied;
    }

    private static string? ImproveSummary(ResumeContent c, CatalogProfession? profession, out string reason)
    {
        var current = (c.Summary ?? string.Empty).Trim();
        var words = current.WordCount();

        if (words >= 25)
        {
            var sentences = SentenceBoundary().Split(current).Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
            var kept = sentences.Where(s => !Cliches.Any(cl => s.Contains(cl, StringComparison.OrdinalIgnoreCase))).ToList();
            var removedCliche = kept.Count < sentences.Count;

            // 120 kelimeden uzunsa ilk cümleler ~90 kelimeye kadar tutulur.
            var trimmedLong = false;
            if (kept.Sum(s => s.WordCount()) > 120)
            {
                var acc = new List<string>();
                foreach (var s in kept)
                {
                    if (acc.Sum(x => x.WordCount()) + s.WordCount() > 90 && acc.Count > 0) break;
                    acc.Add(s);
                }

                kept = acc;
                trimmedLong = true;
            }

            reason = (removedCliche ? "Kanıtı olmayan kalıp ifadeler çıkarıldı. " : string.Empty)
                     + (trimmedLong ? "Özet 60-90 kelimelik ideal uzunluğa indirildi." : string.Empty);
            return removedCliche || trimmedLong ? string.Join(" ", kept) : null;
        }

        // Yok ya da çok kısa: CV'deki bilgilerden özet derlenir (yeni bilgi eklenmez).
        var title = c.Personal.Title.HasValue() ? c.Personal.Title! : profession?.Name.Split('(')[0].Trim() ?? c.Experiences.FirstOrDefault()?.Position;
        var years = c.Experiences.Sum(e => ResumeDates.YearsBetween(e.StartDate, e.EndDate, e.IsCurrent));
        var edu = c.Educations.FirstOrDefault();
        var latest = c.Experiences.FirstOrDefault(e => e.IsCurrent) ?? c.Experiences.FirstOrDefault();
        var skills = c.Skills.Take(4).ToList();

        var parts = new List<string>();
        if (years >= 1 && title.HasValue())
        {
            parts.Add($"{Math.Round(years):0} yılı aşkın deneyime sahip {title}.");
        }
        else if (title.HasValue() && edu is not null && (edu.Field.HasValue() || edu.School.HasValue()))
        {
            parts.Add($"{(edu.Field.HasValue() ? edu.Field + " alanında" : string.Empty)} {edu.School} {(edu.EndDate.HasValue() && !ResumeDates.IsCurrent(edu.EndDate) ? "mezunu" : "öğrencisi")} {title}.".Replace("  ", " ").Trim());
        }
        else if (title.HasValue())
        {
            parts.Add($"{title}.");
        }

        if (latest is not null && latest.Company.HasValue() && latest.Position.HasValue())
        {
            parts.Add($"{latest.Company} bünyesinde {latest.Position} olarak {(latest.IsCurrent ? "görev yapıyor" : "görev aldı")}.");
        }

        if (skills.Count > 0)
        {
            parts.Add(skills.Count == 1 ? $"{skills[0]} konusunda uygulamalı deneyim." : $"{string.Join(", ", skills.Take(skills.Count - 1))} ve {skills[^1]} konularında uygulamalı deneyim.");
        }

        if (profession is not null)
        {
            parts.Add($"{profession.Name.Split('(')[0].Trim()} rolünde ölçülebilir katkı sağlamayı hedefliyor.");
        }

        if (current.Length > 0 && !Cliches.Any(cl => current.Contains(cl, StringComparison.OrdinalIgnoreCase)))
        {
            parts.Insert(Math.Min(1, parts.Count), current.TrimEnd('.') + ".");
        }

        reason = words == 0
            ? "Özet bölümü yoktu. CV'nizdeki unvan, deneyim, eğitim ve becerilerden bir taslak oluşturuldu; kendi cümlelerinizle kişiselleştirin."
            : "Özet çok kısaydı. CV'nizdeki bilgilerle genişletildi; kendi cümlelerinizle kişiselleştirin.";
        return parts.Count == 0 ? null : string.Join(" ", parts);
    }

    /// <summary>Açıklamayı maddelere ayırır ve her maddeyi <see cref="BulletRewriter"/> ile yeniden yazar.</summary>
    private static (string Text, string Reason, string? Tip)? RewriteDescription(string? description, bool isCurrent)
    {
        var normalized = NormalizeBullets(description);
        if (normalized is null)
        {
            return null;
        }

        var lines = new List<string>();
        var notes = new List<string>();
        string? quantTip = null, verbTip = null;
        foreach (var bullet in normalized.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var r = BulletRewriter.Rewrite(bullet, isCurrent);
            lines.Add(r.Text);
            notes.AddRange(r.Notes);
            if (r.Tip is not null)
            {
                if (r.Tip.Contains("Etkiyi ölçülebilir")) quantTip ??= r.Tip[r.Tip.IndexOf("Etkiyi ölçülebilir", StringComparison.Ordinal)..];
                if (r.Tip.StartsWith("Madde bir eylemle bitmiyor", StringComparison.Ordinal)) verbTip ??= r.Tip.Split(". Etkiyi")[0].TrimEnd('.') + ".";
            }
        }

        if (Canonical(normalized) != Canonical(description))
        {
            notes.Insert(0, "Uzun paragraflar tek fikirli maddelere bölündü.");
        }

        var reason = notes.Count == 0
            ? "Maddeler taranabilir biçime getirildi."
            : string.Join(" ", notes.Distinct());
        var tip = string.Join(" ", new[] { verbTip, quantTip }.Where(t => t is not null));
        return (string.Join("\n", lines), reason, tip.Length == 0 ? null : tip);
    }

    /// <summary>Yalnızca madde işareti, boşluk veya satır sonu noktası farkı olan metinleri eşit sayar (gereksiz değişiklik gösterilmesin).</summary>
    private static string Canonical(string? text) =>
        string.Join("\n", (text ?? string.Empty).Replace("\r", string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(l => l.TrimStart('•', '-', '*', '–', '·', '▪', '►', ' ').TrimEnd('.', ' ')));

    /// <summary>Paragrafları tek fikirli maddelere böler, zayıf açılışları ve kalıp ifadeleri temizler.</summary>
    private static string? NormalizeBullets(string? description)
    {
        if (!description.HasValue())
        {
            return null;
        }

        var bullets = new List<string>();
        foreach (var rawLine in description!.Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var line = rawLine.TrimStart('•', '-', '*', '–', '·', '▪', '►', ' ');
            foreach (var sentence in SentenceBoundary().Split(line))
            {
                var s = WeakOpening().Replace(sentence.Trim(), string.Empty).Trim().TrimEnd(';', ',');
                if (s.Length < 3 || Cliches.Any(cl => s.Equals(cl, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                bullets.Add(char.ToUpper(s[0], Tr) + s[1..]);
            }
        }

        return bullets.Count == 0 ? null : string.Join("\n", bullets.Select(b => "• " + b));
    }

    private static void NormalizeDateChange(string? value, string path, string label, string section,
        Action<string, string, string, string, string, string?, string?, string> add)
    {
        var normalized = NormalizeDate(value);
        if (normalized is not null && normalized != value)
        {
            add(section, label, path, "modified", "text", value, normalized,
                "Tarihler tek biçime getirildi (AA/YYYY); ATS'ler deneyim süresini bu biçimden hesaplar.");
        }
    }

    /// <summary>"2021-3", "3.2021", "mart 2021" → "03/2021". Yalnızca yıl yazılmışsa dokunulmaz.</summary>
    private static string? NormalizeDate(string? value)
    {
        if (!value.HasValue() || ResumeDates.IsCurrent(value))
        {
            return null;
        }

        var v = value!.Trim();
        if (Regex.IsMatch(v, @"^(?:19|20)\d{2}$"))
        {
            return null;
        }

        var parsed = ResumeDates.Parse(v);
        return parsed is null ? null : $"{parsed.Value.Month:00}/{parsed.Value.Year}";
    }

    private static List<string> ImproveSkills(ResumeContent c, string fullText, CatalogProfession? profession, out List<string> added, out List<string> renamed)
    {
        added = [];
        renamed = [];
        var result = new List<string>();
        IReadOnlyList<CatalogSkill> catalogSkills = profession?.Skills ?? Array.Empty<CatalogSkill>();

        // 1) Yazımı katalogdaki adla aynı olan ama büyük/küçük harfi farklı becerileri düzelt ("c#" → "C#", "javascript" → "JavaScript").
        //    Eş anlamlılar üzerinden ad değiştirilmez: katalogda alternatif teknolojiler aynı grupta olabilir (Angular ≠ React).
        foreach (var skill in c.Skills.Where(s => s.HasValue()))
        {
            var trimmed = skill.Trim();
            var standard = BulletRewriter.CanonicalTerm(trimmed);
            if (standard != trimmed)
            {
                renamed.Add($"{trimmed} → {standard}");
                result.Add(standard);
                continue;
            }

            var match = catalogSkills.FirstOrDefault(cs => cs.Name.ToSearchKey() == trimmed.ToSearchKey());
            if (match is not null && match.Name != trimmed)
            {
                renamed.Add($"{trimmed} → {match.Name}");
                result.Add(match.Name);
            }
            else
            {
                result.Add(trimmed);
            }
        }

        // 2) Metinde adıyla geçen ama listede olmayan meslek becerileri (yalnızca CV'de zaten yazanlar).
        var prepared = SkillMatcher.PrepareText(fullText);
        var listPrepared = SkillMatcher.PrepareText(string.Join(", ", result));
        foreach (var skill in catalogSkills)
        {
            var ownKey = SkillMatcher.NormalizeKey(skill.Name);
            if (SkillMatcher.Contains(prepared, ownKey) && !SkillMatcher.Contains(listPrepared, ownKey))
            {
                added.Add(skill.Name);
                result.Add(skill.Name);
            }
        }

        result = result.DistinctBySearchKey();

        // 3) Temel beceriler öne.
        if (profession is not null)
        {
            int Rank(string s)
            {
                var k = SkillMatcher.NormalizeKey(s);
                var cs = catalogSkills.FirstOrDefault(x => SkillMatcher.NormalizeKey(x.Name) == k);
                return cs is null ? 0 : (int)cs.Importance;
            }

            result = result.Select((s, i) => (s, i)).OrderByDescending(x => Rank(x.s)).ThenBy(x => x.i).Select(x => x.s).ToList();
        }

        return result;
    }
}
