using System.Globalization;
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

    /// <summary>Ön yazıyı, CV ile aynı şablonun başlık ve renkleriyle PDF'e çevirir.</summary>
    byte[] RenderCoverLetter(PersonalInfo sender, string company, string jobTitle, string body, ResumeTemplate template);
}

/// <summary>
/// ATS uyumlu CV PDF'leri: metin tabanlı, okunabilir sıra, standart başlıklar; görsel ve ikon kullanılmaz.
/// 8 şablon tek bir stil tanımından üretilir.
/// </summary>
public sealed class ResumePdfRenderer : IResumePdfRenderer
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    private enum HeaderStyle { Left, Centered, Band }
    private enum LayoutStyle { Single, SidebarLeft, SidebarRight, Timeline }

    private sealed record Style(
        string Accent,
        string Muted,
        string Font,
        HeaderStyle Header,
        LayoutStyle Layout,
        float BaseSize,
        float Margin,
        bool UpperTitles,
        string SidebarBg);

    private static readonly Dictionary<ResumeTemplate, Style> Styles = new()
    {
        [ResumeTemplate.Klasik] = new("#1f2937", "#4b5563", "Arial", HeaderStyle.Centered, LayoutStyle.Single, 10, 36, true, "#ffffff"),
        [ResumeTemplate.Modern] = new("#0f6b5c", "#55645d", "Arial", HeaderStyle.Left, LayoutStyle.SidebarLeft, 10, 32, true, "#eef6f3"),
        [ResumeTemplate.Kompakt] = new("#1e3a5f", "#4b5563", "Arial", HeaderStyle.Left, LayoutStyle.Single, 9, 26, true, "#ffffff"),
        [ResumeTemplate.Profesyonel] = new("#13201b", "#55645d", "Arial", HeaderStyle.Band, LayoutStyle.Single, 10, 32, true, "#ffffff"),
        [ResumeTemplate.Ortali] = new("#7c2d12", "#57534e", "Georgia", HeaderStyle.Centered, LayoutStyle.Single, 10, 40, false, "#ffffff"),
        [ResumeTemplate.Minimal] = new("#111827", "#6b7280", "Calibri", HeaderStyle.Left, LayoutStyle.Single, 10.5f, 44, false, "#ffffff"),
        [ResumeTemplate.ZamanCizelgesi] = new("#1d4ed8", "#4b5563", "Arial", HeaderStyle.Left, LayoutStyle.Timeline, 10, 34, true, "#ffffff"),
        [ResumeTemplate.Yonetici] = new("#3f2a56", "#57534e", "Georgia", HeaderStyle.Band, LayoutStyle.SidebarRight, 10, 30, false, "#f4f1f8")
    };

    public byte[] Render(ResumeContent content, ResumeTemplate template)
    {
        var st = Styles.GetValueOrDefault(template, Styles[ResumeTemplate.Modern]);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                ConfigurePage(page, st);

                switch (st.Layout)
                {
                    case LayoutStyle.SidebarLeft:
                    case LayoutStyle.SidebarRight:
                        page.Content().Column(outer =>
                        {
                            if (st.Header == HeaderStyle.Band)
                            {
                                Header(outer, content.Personal, st, includeContact: false);
                                outer.Item().PaddingTop(12);
                            }

                            outer.Item().Row(row =>
                            {
                                void Side(IContainer c) => c.Background(st.SidebarBg).Padding(14).Column(side => Sidebar(side, content, st));
                                void Main(IContainer c) => c.Column(main =>
                                {
                                    main.Spacing(8);
                                    if (st.Header != HeaderStyle.Band)
                                    {
                                        Header(main, content.Personal, st, includeContact: false);
                                    }

                                    MainSections(main, content, st, includeSkills: false);
                                });

                                if (st.Layout == LayoutStyle.SidebarLeft)
                                {
                                    Side(row.ConstantItem(165));
                                    Main(row.RelativeItem().PaddingLeft(18));
                                }
                                else
                                {
                                    Main(row.RelativeItem().PaddingRight(18));
                                    Side(row.ConstantItem(165));
                                }
                            });
                        });
                        break;
                    default:
                        page.Content().Column(col =>
                        {
                            col.Spacing(st.BaseSize < 10 ? 5 : 8);
                            Header(col, content.Personal, st, includeContact: true);
                            MainSections(col, content, st, includeSkills: true);
                        });
                        break;
                }

                Footer(page, st);
            });
        }).GeneratePdf();
    }

    public byte[] RenderCoverLetter(PersonalInfo sender, string company, string jobTitle, string body, ResumeTemplate template)
    {
        var st = Styles.GetValueOrDefault(template, Styles[ResumeTemplate.Modern]);
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                ConfigurePage(page, st with { Margin = Math.Max(st.Margin, 44), BaseSize = 10.5f });
                page.Content().Column(col =>
                {
                    col.Spacing(10);
                    Header(col, sender, st, includeContact: true);
                    col.Item().PaddingTop(8).Text(DateTime.Now.ToString("d MMMM yyyy", Tr)).FontColor(st.Muted);
                    col.Item().Text(t =>
                    {
                        t.Span($"{company}").Bold();
                        t.Span($" — {jobTitle} pozisyonu").FontColor(st.Muted);
                    });

                    foreach (var paragraph in body.Replace("\r\n", "\n").Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        col.Item().Text(paragraph).LineHeight(1.45f);
                    }
                });
                Footer(page, st);
            });
        }).GeneratePdf();
    }

    private static void ConfigurePage(PageDescriptor page, Style st)
    {
        page.Size(PageSizes.A4);
        page.Margin(st.Margin);
        page.PageColor(Colors.White);
        page.DefaultTextStyle(x => x.FontFamily(st.Font).FontSize(st.BaseSize).FontColor("#111827").LineHeight(1.3f));
    }

    private static void Footer(PageDescriptor page, Style st)
    {
        page.Footer().AlignRight().Text(t =>
        {
            t.DefaultTextStyle(s => s.FontSize(7).FontColor(st.Muted));
            t.CurrentPageNumber();
            t.Span(" / ");
            t.TotalPages();
        });
    }

    private static void Header(ColumnDescriptor col, PersonalInfo p, Style st, bool includeContact)
    {
        var contact = string.Join("  |  ", new[] { p.Email, p.Phone, p.City, p.LinkedIn, p.Website }.Where(x => x.HasValue()));
        var band = st.Header == HeaderStyle.Band;
        var centered = st.Header == HeaderStyle.Centered;

        col.Item().Element(e => band ? e.Background(st.Accent).Padding(16) : e.PaddingBottom(4)).Column(h =>
        {
            var nameColor = band ? "#ffffff" : st.Accent;
            var subColor = band ? "#e5e7eb" : st.Muted;

            var name = h.Item();
            if (centered) name = name.AlignCenter();
            name.Text(p.FullName.HasValue() ? p.FullName! : "Ad Soyad").FontSize(22).Bold().FontColor(nameColor);

            if (p.Title.HasValue())
            {
                var title = h.Item();
                if (centered) title = title.AlignCenter();
                title.Text(p.Title!).FontSize(12).FontColor(subColor);
            }

            if ((includeContact || band) && contact.Length > 0)
            {
                var line = h.Item().PaddingTop(4);
                if (centered) line = line.AlignCenter();
                line.Text(contact).FontSize(8.5f).FontColor(subColor);
            }
        });

        if (!band)
        {
            col.Item().LineHorizontal(centered ? 0.75f : 1).LineColor(st.Accent);
        }
    }

    private static void MainSections(ColumnDescriptor col, ResumeContent c, Style st, bool includeSkills)
    {
        if (c.Summary.HasValue())
        {
            SectionTitle(col, "Profesyonel Özet", st);
            col.Item().Text(c.Summary!);
        }

        if (c.Experiences.Count > 0)
        {
            SectionTitle(col, "İş Deneyimi", st);
            foreach (var e in c.Experiences)
            {
                var dates = DateRange(e.StartDate, e.IsCurrent ? "Günümüz" : e.EndDate);
                Entry(col, st, dates, item =>
                {
                    item.Item().Text(t =>
                    {
                        t.Span(e.Position ?? string.Empty).Bold();
                        if (e.Company.HasValue())
                        {
                            t.Span("  ·  " + e.Company).FontColor(st.Muted);
                        }
                    });

                    if (e.Location.HasValue())
                    {
                        item.Item().Text(e.Location!).FontSize(8.5f).FontColor(st.Muted);
                    }

                    Bullets(item, e.Description);
                });
            }
        }

        if (c.Educations.Count > 0)
        {
            SectionTitle(col, "Eğitim", st);
            foreach (var e in c.Educations)
            {
                Entry(col, st, DateRange(e.StartDate, e.EndDate), x =>
                {
                    x.Item().Text(e.School ?? string.Empty).Bold();
                    var degree = string.Join(", ", new[] { e.Degree, e.Field }.Where(v => v.HasValue()));
                    if (degree.Length > 0) x.Item().Text(degree);
                    if (e.Grade.HasValue()) x.Item().Text(e.Grade!).FontSize(8.5f).FontColor(st.Muted);
                });
            }
        }

        if (includeSkills && c.Skills.Count > 0)
        {
            SectionTitle(col, "Beceriler", st);
            col.Item().Text(string.Join("  •  ", c.Skills));
        }

        if (includeSkills && c.Languages.Count > 0)
        {
            SectionTitle(col, "Yabancı Dil", st);
            col.Item().Text(string.Join("  •  ", c.Languages.Select(l => $"{l.Name} ({LanguageLevelName(l.Level)})")));
        }

        if (c.Projects.Count > 0)
        {
            SectionTitle(col, "Projeler", st);
            foreach (var p in c.Projects)
            {
                col.Item().ShowEntire().Column(x =>
                {
                    x.Item().Text(t =>
                    {
                        t.Span(p.Name ?? string.Empty).Bold();
                        if (p.Url.HasValue()) t.Span("  " + p.Url).FontSize(8.5f).FontColor(st.Muted);
                    });
                    if (p.Description.HasValue()) x.Item().Text(p.Description!);
                });
            }
        }

        if (c.Certificates.Count > 0)
        {
            SectionTitle(col, "Sertifikalar ve Kurslar", st);
            foreach (var cert in c.Certificates)
            {
                col.Item().Text(t =>
                {
                    t.Span("• " + cert.Name);
                    var meta = string.Join(", ", new[] { cert.Issuer, cert.Date }.Where(v => v.HasValue()));
                    if (meta.Length > 0) t.Span($"  ({meta})").FontColor(st.Muted);
                });
            }
        }
    }

    /// <summary>Tarih bilgisini şablona göre sağa (varsayılan) ya da sol sütuna (zaman çizelgesi) yerleştirir.</summary>
    private static void Entry(ColumnDescriptor col, Style st, string dates, Action<ColumnDescriptor> body)
    {
        col.Item().ShowEntire().Row(r =>
        {
            if (st.Layout == LayoutStyle.Timeline)
            {
                r.ConstantItem(92).PaddingTop(1).Text(dates).FontSize(8.5f).Bold().FontColor(st.Accent);
                r.ConstantItem(2).Background(st.Accent);
                r.RelativeItem().PaddingLeft(10).Column(body);
            }
            else
            {
                r.RelativeItem().Column(body);
                if (dates.Length > 0)
                {
                    r.AutoItem().PaddingLeft(8).Text(dates).FontSize(8.5f).FontColor(st.Muted);
                }
            }
        });
    }

    private static void Sidebar(ColumnDescriptor side, ResumeContent c, Style st)
    {
        side.Spacing(6);
        var p = c.Personal;
        SideTitle(side, "İletişim", st);
        foreach (var item in new[] { p.Email, p.Phone, p.City, p.LinkedIn, p.Website }.Where(x => x.HasValue()))
        {
            side.Item().Text(item!).FontSize(8.5f);
        }

        if (c.Skills.Count > 0)
        {
            SideTitle(side, "Beceriler", st);
            foreach (var s in c.Skills.Take(30))
            {
                side.Item().Text("• " + s).FontSize(8.5f);
            }
        }

        if (c.Languages.Count > 0)
        {
            SideTitle(side, "Yabancı Dil", st);
            foreach (var l in c.Languages)
            {
                side.Item().Text($"{l.Name} – {LanguageLevelName(l.Level)}").FontSize(8.5f);
            }
        }
    }

    private static void SectionTitle(ColumnDescriptor col, string title, Style st)
    {
        var text = st.UpperTitles ? title.ToUpper(Tr) : title;
        var item = col.Item().PaddingTop(6);
        if (st.Header == HeaderStyle.Centered && !st.UpperTitles)
        {
            item.Column(c =>
            {
                c.Item().Text(text).FontSize(12).Bold().FontColor(st.Accent);
                c.Item().PaddingTop(2).Width(40).LineHorizontal(1.5f).LineColor(st.Accent);
            });
            return;
        }

        item.Text(text).FontSize(st.UpperTitles ? 10.5f : 12).Bold().FontColor(st.Accent).LetterSpacing(st.UpperTitles ? 0.04f : 0);
    }

    private static void SideTitle(ColumnDescriptor col, string title, Style st)
    {
        col.Item().PaddingTop(8).Text(st.UpperTitles ? title.ToUpper(Tr) : title).FontSize(9.5f).Bold().FontColor(st.Accent);
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
