using System.Globalization;
using OzgecmisAnalizi.Entities.Dtos;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace OzgecmisAnalizi.Business.Pdf;

public interface IAnalysisReportRenderer
{
    byte[] Render(AnalysisDto analysis, string? ownerName);
}

/// <summary>Analiz sonucunu paylaşılabilir PDF rapora çevirir.</summary>
public sealed class AnalysisReportRenderer : IAnalysisReportRenderer
{
    private const string Accent = "#0f6b5c";
    private const string Muted = "#55645d";
    private const string Soft = "#eef6f3";
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    public byte[] Render(AnalysisDto a, string? ownerName)
    {
        var d = a.Details;
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(10).FontColor("#13201b").LineHeight(1.3f));

                page.Header().Column(h =>
                {
                    h.Item().Row(r =>
                    {
                        r.RelativeItem().Column(c =>
                        {
                            c.Item().Text("ÖzgeçmişAnalizi · CV Analiz Raporu").FontSize(16).Bold().FontColor(Accent);
                            c.Item().Text($"{a.ResumeTitle}{(ownerName is null ? "" : " — " + ownerName)}").FontColor(Muted);
                        });
                        r.AutoItem().AlignRight().Text(a.CreatedAt.ToLocalTime().ToString("d MMMM yyyy", Tr)).FontColor(Muted);
                    });
                    h.Item().PaddingTop(6).LineHorizontal(1).LineColor(Accent);
                });

                page.Content().PaddingTop(10).Column(col =>
                {
                    col.Spacing(10);

                    // Puan özeti
                    col.Item().Background(Soft).Padding(14).Row(r =>
                    {
                        r.ConstantItem(120).Column(c =>
                        {
                            c.Item().Text($"{a.OverallScore}").FontSize(40).Bold().FontColor(Accent);
                            c.Item().Text($"/100 · {a.Label}").Bold();
                            c.Item().Text($"Tahmini ilk %{a.TopPercent}").FontSize(9).FontColor(Muted);
                        });
                        r.RelativeItem().Column(c =>
                        {
                            c.Spacing(4);
                            Meter(c, "Kapsam", a.ComprehensivenessScore);
                            Meter(c, "Yapı", a.StructureScore);
                            Meter(c, "Dil ve anlatım", a.ClarityScore);
                            Meter(c, "ATS okunabilirliği", a.AtsScore);
                            if (a.ProfessionName is not null)
                            {
                                Meter(c, $"Beceri eşleşmesi ({a.ProfessionName})", a.SkillMatchScore);
                            }
                        });
                    });

                    if (!string.IsNullOrWhiteSpace(d.AiSummary))
                    {
                        Title(col, "Genel değerlendirme");
                        col.Item().Text(d.AiSummary!);
                    }

                    if (d.Strengths.Count > 0)
                    {
                        Title(col, "Güçlü yönler");
                        BulletList(col, d.Strengths);
                    }

                    if (d.Improvements.Count > 0)
                    {
                        Title(col, "Öncelikli iyileştirmeler");
                        BulletList(col, d.Improvements);
                    }

                    Title(col, "ATS kontrolleri");
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(44);
                            c.ConstantColumn(150);
                            c.RelativeColumn();
                        });
                        foreach (var check in d.AtsChecks)
                        {
                            t.Cell().PaddingVertical(3).Text(check.Passed ? "Geçti" : "Eksik").FontColor(check.Passed ? "#15803d" : "#b42318").Bold();
                            t.Cell().PaddingVertical(3).Text(check.Name).Bold();
                            t.Cell().PaddingVertical(3).Text(check.Detail ?? string.Empty).FontColor(Muted);
                        }
                    });

                    if (d.Sections.Count > 0)
                    {
                        Title(col, "Bölüm bölüm öneriler");
                        foreach (var s in d.Sections)
                        {
                            col.Item().ShowEntire().Column(c =>
                            {
                                c.Item().Text($"{s.Section} — {s.Score}/100").Bold();
                                if (s.Tips.Count == 0)
                                {
                                    c.Item().Text("Bu bölüm iyi durumda.").FontColor(Muted);
                                }
                                else
                                {
                                    BulletList(c, s.Tips);
                                }
                            });
                        }
                    }

                    if (d.MissingSkills.Count > 0)
                    {
                        Title(col, "Eksik beceriler");
                        col.Item().Table(t =>
                        {
                            t.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(2);
                                c.RelativeColumn(1.2f);
                                c.RelativeColumn(3);
                            });
                            t.Header(h =>
                            {
                                h.Cell().Text("Beceri").Bold();
                                h.Cell().Text("Önem").Bold();
                                h.Cell().Text("Nasıl öğrenilir").Bold();
                            });
                            foreach (var g in d.MissingSkills.Take(20))
                            {
                                t.Cell().BorderBottom(0.5f).BorderColor("#dde3df").PaddingVertical(3).Text(g.Skill);
                                t.Cell().BorderBottom(0.5f).BorderColor("#dde3df").PaddingVertical(3).Text(g.Importance).FontColor(Muted);
                                t.Cell().BorderBottom(0.5f).BorderColor("#dde3df").PaddingVertical(3).Text(g.HowToLearn ?? "-").FontColor(Muted);
                            }
                        });
                    }

                    if (d.Salary is not null)
                    {
                        Title(col, "Tahmini maaş aralığı (net, aylık)");
                        col.Item().Text($"{d.Salary.Level}{(d.Salary.City is null ? "" : " · " + d.Salary.City)}: " +
                                        $"{d.Salary.MinNet.ToString("N0", Tr)} – {d.Salary.MaxNet.ToString("N0", Tr)} TL (ortanca {d.Salary.MedianNet.ToString("N0", Tr)} TL)").Bold();
                        col.Item().Text(d.Salary.Basis).FontSize(8.5f).FontColor(Muted);
                    }

                    if (d.CareerPath.Count > 0)
                    {
                        Title(col, "Kariyer yolu");
                        foreach (var step in d.CareerPath)
                        {
                            col.Item().ShowEntire().Row(r =>
                            {
                                r.ConstantItem(14).Text(step.IsCurrent ? "●" : "○").FontColor(Accent);
                                r.RelativeItem().Column(c =>
                                {
                                    c.Item().Text(t =>
                                    {
                                        t.Span(step.Title).Bold();
                                        t.Span($"  ·  {step.Level} · {step.Duration}").FontColor(Muted);
                                    });
                                    c.Item().Text(string.Join(", ", step.Focus)).FontSize(9).FontColor(Muted);
                                });
                            });
                        }
                    }

                    col.Item().PaddingTop(8).Text($"Değerlendirme motoru: {a.Engine}. Puanlar ve maaş aralıkları yol gösterici tahminlerdir.")
                        .FontSize(8).FontColor(Muted);
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.DefaultTextStyle(s => s.FontSize(8).FontColor(Muted));
                    t.CurrentPageNumber();
                    t.Span(" / ");
                    t.TotalPages();
                });
            });
        }).GeneratePdf();
    }

    private static void Title(ColumnDescriptor col, string text) =>
        col.Item().PaddingTop(4).Text(text).FontSize(12).Bold().FontColor(Accent);

    private static void BulletList(ColumnDescriptor col, IEnumerable<string> items)
    {
        foreach (var item in items)
        {
            col.Item().Row(r =>
            {
                r.ConstantItem(10).Text("•");
                r.RelativeItem().Text(item);
            });
        }
    }

    private static void Meter(ColumnDescriptor col, string label, int value)
    {
        var color = value >= 75 ? "#15803d" : value >= 55 ? "#a15c07" : "#b42318";
        col.Item().Row(r =>
        {
            r.ConstantItem(150).Text(label).FontSize(9);
            r.RelativeItem().PaddingTop(3).Height(7).Background("#dde3df").Row(bar =>
            {
                var filled = Math.Clamp(value, 1, 100);
                bar.RelativeItem(filled).Background(color);
                if (filled < 100)
                {
                    bar.RelativeItem(100 - filled);
                }
            });
            r.ConstantItem(34).AlignRight().Text($"{value}").FontSize(9).Bold();
        });
    }
}
