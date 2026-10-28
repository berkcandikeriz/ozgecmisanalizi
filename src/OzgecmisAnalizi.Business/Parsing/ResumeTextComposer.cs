using System.Text;
using OzgecmisAnalizi.Entities.Content;

namespace OzgecmisAnalizi.Business.Parsing;

/// <summary>Yapılandırılmış CV içeriğini analiz motorunun okuyabileceği düz metne çevirir (başlıklar standart).</summary>
public static class ResumeTextComposer
{
    public static string Compose(ResumeContent c)
    {
        var sb = new StringBuilder();
        var p = c.Personal;
        AppendIf(sb, p.FullName);
        AppendIf(sb, p.Title);
        AppendIf(sb, string.Join(" | ", new[] { p.Email, p.Phone, p.City, p.LinkedIn, p.Website }.Where(x => !string.IsNullOrWhiteSpace(x))));

        if (!string.IsNullOrWhiteSpace(c.Summary))
        {
            sb.AppendLine().AppendLine("Özet").AppendLine(c.Summary);
        }

        if (c.Experiences.Count > 0)
        {
            sb.AppendLine().AppendLine("İş Deneyimi");
            foreach (var e in c.Experiences)
            {
                sb.AppendLine($"{e.Position} | {e.Company}{(string.IsNullOrWhiteSpace(e.Location) ? "" : " | " + e.Location)}");
                sb.AppendLine($"{e.StartDate} - {(e.IsCurrent ? "Günümüz" : e.EndDate)}");
                AppendIf(sb, e.Description);
            }
        }

        if (c.Educations.Count > 0)
        {
            sb.AppendLine().AppendLine("Eğitim");
            foreach (var e in c.Educations)
            {
                sb.AppendLine($"{e.School} | {e.Degree} {e.Field}".Trim());
                sb.AppendLine($"{e.StartDate} - {e.EndDate}");
                AppendIf(sb, e.Grade);
            }
        }

        if (c.Skills.Count > 0)
        {
            sb.AppendLine().AppendLine("Beceriler").AppendLine(string.Join(", ", c.Skills));
        }

        if (c.Languages.Count > 0)
        {
            sb.AppendLine().AppendLine("Yabancı Dil");
            foreach (var l in c.Languages)
            {
                sb.AppendLine($"{l.Name} - {l.Level}");
            }
        }

        if (c.Certificates.Count > 0)
        {
            sb.AppendLine().AppendLine("Sertifikalar");
            foreach (var x in c.Certificates)
            {
                sb.AppendLine($"{x.Name} {x.Issuer} {x.Date}".Trim());
            }
        }

        if (c.Projects.Count > 0)
        {
            sb.AppendLine().AppendLine("Projeler");
            foreach (var x in c.Projects)
            {
                sb.AppendLine(x.Name);
                AppendIf(sb, x.Description);
            }
        }

        return sb.ToString();
    }

    private static void AppendIf(StringBuilder sb, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            sb.AppendLine(value);
        }
    }
}
