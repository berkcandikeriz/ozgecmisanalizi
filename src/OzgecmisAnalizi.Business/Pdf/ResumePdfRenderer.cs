using OzgecmisAnalizi.Core.Extensions;
using OzgecmisAnalizi.Entities.Content;
using OzgecmisAnalizi.Entities.Enums;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace OzgecmisAnalizi.Business.Pdf;

public interface IResumePdfRenderer
{
    byte[] Render(ResumeContent content, ResumeTemplate template);
}

/// <summary>
/// ATS uyumlu CV PDF'leri: metin tabanlı, tek akışlı, standart başlıklar. Görsel/ikon kullanılmaz.
/// </summary>
public sealed class ResumePdfRenderer : IResumePdfRenderer
{
    private const string Font = "Arial";

    private static readonly Dictionary<ResumeTemplate, (string Accent, string Muted)> Palettes = new()
    {
        [ResumeTemplate.Klasik] = ("#1f2937", "#4b5563"),
        [ResumeTemplate.Modern] = ("#0f6b5c", "#55645d"),
        [ResumeTemplate.Kompakt] = ("#1e3a5f", "#4b5563"),
        [ResumeTemplate.Profesyonel] = ("#13201b", "#55645d")
    };

    public byte[] Render(ResumeContent content, ResumeTemplate template)
    {
        var (accent, muted) = Palettes[template];
        var compact = template == ResumeTemplate.Kompakt;
        var baseSize = compact ? 9f : 10f;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(compact ? 28 : 36);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontFamily(Font).FontSize(baseSize).FontColor("#111827").LineHeight(1.3f));

                if (template == ResumeTemplate.Modern)
                {
                    page.Content().Row(row =>
                    {
                        row.ConstantItem(165).Background("#eef6f3").Padding(14).Column(side => Sidebar(side, content, accent, muted));
                        row.RelativeItem().PaddingLeft(18).Column(main =>
                        {
                            main.Spacing(8);
                            Header(main, content, accent, muted, centered: false, band: false, includeContact: false);
                            MainSections(main, content, accent, muted, includeSkills: false);
                        });
                    });
                }
                else
                {
                    page.Content().Column(col =>
                    {
                        col.Spacing(compact ? 5 : 8);
                        Header(col, content, accent, muted,
                            centered: template == ResumeTemplate.Klasik,
                            band: template == ResumeTemplate.Profesyonel,
                            includeContact: true);
                        MainSections(col, content, accent, muted, includeSkills: true);
                    });
                }

                page.Footer().AlignRight().Text(t =>
                {
                    t.DefaultTextStyle(s => s.FontSize(7).FontColor(muted));
                    t.CurrentPageNumber();
                    t.Span(" / ");
                    t.TotalPages();
                });
            });
        }).GeneratePdf();
    }

    private static void Header(ColumnDescriptor col, ResumeContent c, string accent, string muted, bool centered, bool band, bool includeContact)
    {
        var p = c.Personal;
        var contact = string.Join("  |  ", new[] { p.Email, p.Phone, p.City, p.LinkedIn, p.Website }.Where(x => x.HasValue()));

        col.Item().Element(e => band ? e.Background(accent).Padding(14) : e.PaddingBottom(4)).Column(h =>
        {
            var nameColor = band ? "#ffffff" : accent;
            var subColor = band ? "#e1f2ed" : muted;

            var name = h.Item();
            if (centered) name = name.AlignCenter();
            name.Text(p.FullName.HasValue() ? p.FullName! : "Ad Soyad").FontSize(22).Bold().FontColor(nameColor);

            if (p.Title.HasValue())
            {
                var title = h.Item();
                if (centered) title = title.AlignCenter();
                title.Text(p.Title!).FontSize(12).FontColor(subColor);
            }

            if (includeContact && contact.Length > 0)
            {
                var line = h.Item().PaddingTop(4);
                if (centered) line = line.AlignCenter();
                line.Text(contact).FontSize(8.5f).FontColor(subColor);
            }
        });

        if (!band)
        {
            col.Item().LineHorizontal(1).LineColor(accent);
        }
    }

    private static void MainSections(ColumnDescriptor col, ResumeContent c, string accent, string muted, bool includeSkills)
    {
        if (c.Summary.HasValue())
        {
            SectionTitle(col, "Profesyonel Özet", accent);
            col.Item().Text(c.Summary!);
        }

        if (c.Experiences.Count > 0)
        {
            SectionTitle(col, "İş Deneyimi", accent);
            foreach (var e in c.Experiences)
            {
                col.Item().ShowEntire().Column(item =>
                {
                    item.Item().Row(r =>
                    {
                        r.RelativeItem().Text(t =>
                        {
                            t.Span(e.Position ?? string.Empty).Bold();
                            if (e.Company.HasValue())
                            {
                                t.Span("  ·  " + e.Company).FontColor(muted);
                            }
                        });
                        r.AutoItem().Text(DateRange(e.StartDate, e.IsCurrent ? "Günümüz" : e.EndDate)).FontSize(8.5f).FontColor(muted);
                    });

                    if (e.Location.HasValue())
                    {
                        item.Item().Text(e.Location!).FontSize(8.5f).FontColor(muted);
                    }

                    Bullets(item, e.Description);
                });
            }
        }

        if (c.Educations.Count > 0)
        {
            SectionTitle(col, "Eğitim", accent);
            foreach (var e in c.Educations)
            {
                col.Item().ShowEntire().Row(r =>
                {
                    r.RelativeItem().Column(x =>
                    {
                        x.Item().Text(e.School ?? string.Empty).Bold();
                        var degree = string.Join(", ", new[] { e.Degree, e.Field }.Where(v => v.HasValue()));
                        if (degree.Length > 0) x.Item().Text(degree);
                        if (e.Grade.HasValue()) x.Item().Text(e.Grade!).FontSize(8.5f).FontColor(muted);
                    });
                    r.AutoItem().Text(DateRange(e.StartDate, e.EndDate)).FontSize(8.5f).FontColor(muted);
                });
            }
        }

        if (includeSkills && c.Skills.Count > 0)
        {
            SectionTitle(col, "Beceriler", accent);
            col.Item().Text(string.Join("  •  ", c.Skills));
        }

        if (includeSkills && c.Languages.Count > 0)
        {
            SectionTitle(col, "Yabancı Dil", accent);
            col.Item().Text(string.Join("  •  ", c.Languages.Select(l => $"{l.Name} ({LanguageLevelName(l.Level)})")));
        }

        if (c.Projects.Count > 0)
        {
            SectionTitle(col, "Projeler", accent);
            foreach (var p in c.Projects)
            {
                col.Item().ShowEntire().Column(x =>
                {
                    x.Item().Text(t =>
                    {
                        t.Span(p.Name ?? string.Empty).Bold();
                        if (p.Url.HasValue()) t.Span("  " + p.Url).FontSize(8.5f).FontColor(muted);
                    });
                    if (p.Description.HasValue()) x.Item().Text(p.Description!);
                });
            }
        }

        if (c.Certificates.Count > 0)
        {
            SectionTitle(col, "Sertifikalar ve Kurslar", accent);
            foreach (var cert in c.Certificates)
            {
                col.Item().Text(t =>
                {
                    t.Span("• " + cert.Name);
                    var meta = string.Join(", ", new[] { cert.Issuer, cert.Date }.Where(v => v.HasValue()));
                    if (meta.Length > 0) t.Span($"  ({meta})").FontColor(muted);
                });
            }
        }
    }

    private static void Sidebar(ColumnDescriptor side, ResumeContent c, string accent, string muted)
    {
        side.Spacing(6);
        var p = c.Personal;
        SideTitle(side, "İletişim", accent);
        foreach (var item in new[] { p.Email, p.Phone, p.City, p.LinkedIn, p.Website }.Where(x => x.HasValue()))
        {
            side.Item().Text(item!).FontSize(8.5f);
        }

        if (c.Skills.Count > 0)
        {
            SideTitle(side, "Beceriler", accent);
            foreach (var s in c.Skills.Take(30))
            {
                side.Item().Text("• " + s).FontSize(8.5f);
            }
        }

        if (c.Languages.Count > 0)
        {
            SideTitle(side, "Yabancı Dil", accent);
            foreach (var l in c.Languages)
            {
                side.Item().Text($"{l.Name} – {LanguageLevelName(l.Level)}").FontSize(8.5f);
            }
        }
    }

    private static void SectionTitle(ColumnDescriptor col, string title, string accent)
    {
        col.Item().PaddingTop(6).Text(title.ToUpper(System.Globalization.CultureInfo.GetCultureInfo("tr-TR")))
            .FontSize(10.5f).Bold().FontColor(accent).LetterSpacing(0.04f);
    }

    private static void SideTitle(ColumnDescriptor col, string title, string accent)
    {
        col.Item().PaddingTop(8).Text(title.ToUpper(System.Globalization.CultureInfo.GetCultureInfo("tr-TR")))
            .FontSize(9.5f).Bold().FontColor(accent);
    }

    private static void Bullets(ColumnDescriptor col, string? description)
    {
        if (!description.HasValue())
        {
            return;
        }

        foreach (var line in description!.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var text = line.TrimStart('•', '-', '*', ' ');
            col.Item().PaddingLeft(8).Row(r =>
            {
                r.ConstantItem(8).Text("•");
                r.RelativeItem().Text(text);
            });
        }
    }

    private static string DateRange(string? start, string? end)
    {
        if (!start.HasValue() && !end.HasValue()) return string.Empty;
        if (!start.HasValue()) return end!;
        return $"{start} – {(end.HasValue() ? end : "Günümüz")}";
    }

    public static string LanguageLevelName(LanguageLevel level) => level switch
    {
        LanguageLevel.Baslangic => "Başlangıç",
        LanguageLevel.Orta => "Orta",
        LanguageLevel.Iyi => "İyi",
        LanguageLevel.Ileri => "İleri",
        LanguageLevel.AnaDil => "Ana dil",
        _ => level.ToString()
    };
}
