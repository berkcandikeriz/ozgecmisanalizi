using System.Text;
using Microsoft.EntityFrameworkCore;
using OzgecmisAnalizi.Business.Abstractions;
using OzgecmisAnalizi.Business.Ai;
using OzgecmisAnalizi.Business.Analysis;
using OzgecmisAnalizi.Business.Mapping;
using OzgecmisAnalizi.Business.Parsing;
using OzgecmisAnalizi.Core.Abstractions;
using OzgecmisAnalizi.Core.Extensions;
using OzgecmisAnalizi.Core.Tools;
using OzgecmisAnalizi.DataAccess.Repositories;
using OzgecmisAnalizi.Entities.Concrete;
using OzgecmisAnalizi.Entities.Content;
using OzgecmisAnalizi.Entities.Dtos;

namespace OzgecmisAnalizi.Business.Services;

public sealed class CoverLetterService(IUnitOfWork uow, ICurrentUser currentUser, IAiProvider ai, ISkillCatalog catalog) : ICoverLetterService
{
    private IRepository<CoverLetter> Letters => uow.Repository<CoverLetter>();

    public async Task<CoverLetterDto> GenerateAsync(CoverLetterRequest request, CancellationToken ct = default)
    {
        var userId = currentUser.RequireId();
        var jobTitle = Guard.Required(request.JobTitle, "Pozisyon", 200);
        var company = Guard.Required(request.Company, "Şirket", 200);
        var resume = Guard.Found(
            await uow.Repository<Resume>().Query().FirstOrDefaultAsync(r => r.Id == request.ResumeId && r.UserId == userId, ct),
            "CV", request.ResumeId);

        var tone = request.Tone.HasValue() ? request.Tone!.Trim().Truncate(40) : "profesyonel ve samimi";
        var resumeText = ResumeTextComposer.Compose(resume.Content);
        string? content = null;
        var engine = "Şablon";

        if (ai.IsEnabled)
        {
            content = (await ai.CompleteAsync(AiPrompts.System,
                AiPrompts.CoverLetter(resumeText.Truncate(9000), jobTitle, company, request.JobDescription?.Truncate(6000), tone),
                jsonOutput: false, ct))?.Trim();
            if (content.HasValue())
            {
                engine = ai.Name;
            }
        }

        if (!content.HasValue())
        {
            content = await BuildTemplateAsync(resume.Content, jobTitle, company, request.JobDescription, ct);
        }

        var letter = new CoverLetter
        {
            UserId = userId,
            ResumeId = resume.Id,
            JobApplicationId = request.JobApplicationId,
            JobTitle = jobTitle,
            Company = company,
            Content = content!,
            Engine = engine
        };
        await Letters.AddAsync(letter, ct);
        await uow.SaveChangesAsync(ct);
        return letter.ToDto();
    }

    public async Task<IReadOnlyList<CoverLetterDto>> ListAsync(CancellationToken ct = default)
    {
        var userId = currentUser.RequireId();
        var rows = await Letters.Query().Where(l => l.UserId == userId).OrderByDescending(l => l.CreatedAt).Take(100).ToListAsync(ct);
        return rows.Select(r => r.ToDto()).ToList();
    }

    public async Task<CoverLetterDto> GetAsync(Guid id, CancellationToken ct = default) => (await LoadAsync(id, false, ct)).ToDto();

    public async Task<CoverLetterDto> UpdateAsync(Guid id, CoverLetterUpdateRequest request, CancellationToken ct = default)
    {
        var letter = await LoadAsync(id, true, ct);
        letter.Content = Guard.Required(request.Content, "Ön yazı", 20000);
        await uow.SaveChangesAsync(ct);
        return letter.ToDto();
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var letter = await LoadAsync(id, true, ct);
        Letters.Remove(letter);
        await uow.SaveChangesAsync(ct);
    }

    private async Task<CoverLetter> LoadAsync(Guid id, bool tracked, CancellationToken ct)
    {
        var userId = currentUser.RequireId();
        var q = tracked ? Letters.Tracked() : Letters.Query();
        return Guard.Found(await q.FirstOrDefaultAsync(l => l.Id == id && l.UserId == userId, ct), "Ön yazı", id);
    }

    /// <summary>Yapay zeka yoksa CV verisinden kişiselleştirilmiş bir şablon mektup üretir.</summary>
    private async Task<string> BuildTemplateAsync(ResumeContent c, string jobTitle, string company, string? jobDescription, CancellationToken ct)
    {
        var name = c.Personal.FullName ?? "Ad Soyad";
        var latest = c.Experiences.FirstOrDefault();
        var years = c.Experiences.Sum(e => ResumeDates.YearsBetween(e.StartDate, e.EndDate, e.IsCurrent));

        var highlightSkills = c.Skills.Take(4).ToList();
        if (jobDescription.HasValue())
        {
            var jd = SkillMatcher.PrepareText(jobDescription);
            var all = await catalog.AllSkillsAsync(ct);
            var resumePrepared = SkillMatcher.PrepareText(string.Join(", ", c.Skills) + " " + ResumeTextComposer.Compose(c));
            var overlap = all.Where(s => SkillMatcher.ContainsAny(jd, s.Keys) && SkillMatcher.ContainsAny(resumePrepared, s.Keys))
                .Select(s => s.Name).Take(4).ToList();
            if (overlap.Count > 0)
            {
                highlightSkills = overlap;
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Sayın {company} İşe Alım Ekibi,");
        sb.AppendLine();
        sb.AppendLine(years >= 1
            ? $"{company} bünyesindeki {jobTitle} pozisyonu için başvurumu iletmek istiyorum. Yaklaşık {Math.Round(years):0} yıllık deneyimim boyunca {(latest?.Company is { } lc ? lc + " başta olmak üzere " : string.Empty)}farklı ekiplerde sorumluluk aldım ve bu pozisyonun gerektirdiği yetkinlikleri geliştirdim."
            : $"{company} bünyesindeki {jobTitle} pozisyonu için başvurumu iletmek istiyorum. Eğitimim ve projelerim boyunca edindiğim bilgi ve becerileri ekibinize katkıya dönüştürmeyi hedefliyorum.");
        sb.AppendLine();

        if (latest is not null)
        {
            var firstBullet = latest.Description?.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.TrimStart('•', '-', ' ');
            sb.AppendLine($"Son olarak {latest.Company} şirketinde {latest.Position} olarak çalıştım.{(firstBullet.HasValue() ? $" Bu rolde {LowerFirst(firstBullet!)}" : string.Empty)}");
            sb.AppendLine();
        }

        if (highlightSkills.Count > 0)
        {
            sb.AppendLine($"Özellikle {string.Join(", ", highlightSkills)} konularındaki deneyimimin {jobTitle} rolünde doğrudan değer yaratacağına inanıyorum. Yeni araçları hızla öğrenir, sonuç odaklı ve iş birliğine açık çalışırım.");
            sb.AppendLine();
        }

        sb.AppendLine("Ekibinize nasıl katkı sağlayabileceğimi bir görüşmede ayrıntılı olarak anlatmaktan memnuniyet duyarım. Zaman ayırdığınız için teşekkür ederim.");
        sb.AppendLine();
        sb.AppendLine("Saygılarımla,");
        sb.AppendLine(name);
        if (c.Personal.Email.HasValue()) sb.AppendLine(c.Personal.Email);
        if (c.Personal.Phone.HasValue()) sb.AppendLine(c.Personal.Phone);
        return sb.ToString().Trim();
    }

    private static string LowerFirst(string value)
    {
        var trimmed = value.Trim().TrimEnd('.');
        if (trimmed.Length == 0) return trimmed;
        return char.ToLower(trimmed[0], System.Globalization.CultureInfo.GetCultureInfo("tr-TR")) + trimmed[1..] + ".";
    }
}
