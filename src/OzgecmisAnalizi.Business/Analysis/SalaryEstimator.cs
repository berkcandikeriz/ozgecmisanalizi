using Microsoft.Extensions.Options;
using OzgecmisAnalizi.Business.Options;
using OzgecmisAnalizi.Core.Extensions;
using OzgecmisAnalizi.Entities.Content;
using OzgecmisAnalizi.Entities.Enums;

namespace OzgecmisAnalizi.Business.Analysis;

public interface ISalaryEstimator
{
    SalaryEstimate Estimate(CatalogProfession profession, ExperienceLevel level, string? city);
    IReadOnlyList<SalaryEstimate> EstimateAllLevels(CatalogProfession profession, string? city);
}

/// <summary>
/// Maaş tahmini: mesleğin junior/kıdemli katsayıları × net asgari ücret × şehir katsayısı.
/// Sonuçlar piyasa verisi değil, yapılandırılabilir referans aralıklarıdır.
/// </summary>
public sealed class SalaryEstimator(IOptions<SalaryOptions> options) : ISalaryEstimator
{
    private readonly SalaryOptions _o = options.Value;

    public SalaryEstimate Estimate(CatalogProfession profession, ExperienceLevel level, string? city)
    {
        var factor = level switch
        {
            ExperienceLevel.Stajyer => Math.Max(1m, profession.JuniorFactor * 0.7m),
            ExperienceLevel.Junior => profession.JuniorFactor,
            ExperienceLevel.Orta => (profession.JuniorFactor + profession.SeniorFactor) / 2m,
            ExperienceLevel.Kidemli => profession.SeniorFactor,
            _ => profession.SeniorFactor * 1.35m
        };

        var cityFactor = ResolveCityFactor(city, out var cityName);
        var median = _o.MinimumNetWage * factor * cityFactor;
        return new SalaryEstimate
        {
            MedianNet = RoundTo(median, 500),
            MinNet = RoundTo(Math.Max(_o.MinimumNetWage, median * 0.85m), 500),
            MaxNet = RoundTo(median * 1.2m, 500),
            Currency = "TRY",
            Level = LevelName(level),
            City = cityName,
            Basis = $"{_o.ReferenceYear} net asgari ücret ({_o.MinimumNetWage:N2} TL) × meslek katsayısı {factor:0.00} × şehir katsayısı {cityFactor:0.00}. Tahmini değerdir."
        };
    }

    public IReadOnlyList<SalaryEstimate> EstimateAllLevels(CatalogProfession profession, string? city) =>
        Enum.GetValues<ExperienceLevel>().Select(l => Estimate(profession, l, city)).ToList();

    public static string LevelName(ExperienceLevel level) => level switch
    {
        ExperienceLevel.Stajyer => "Stajyer / Yeni mezun",
        ExperienceLevel.Junior => "Junior (1-3 yıl)",
        ExperienceLevel.Orta => "Orta seviye (3-6 yıl)",
        ExperienceLevel.Kidemli => "Kıdemli (6-10 yıl)",
        _ => "Lider / Yönetici (10+ yıl)"
    };

    private decimal ResolveCityFactor(string? city, out string? cityName)
    {
        cityName = city;
        if (string.IsNullOrWhiteSpace(city))
        {
            cityName = null;
            return 1m;
        }

        var key = city.ToSearchKey();
        foreach (var (name, value) in _o.CityFactors)
        {
            if (name.ToSearchKey() == key)
            {
                cityName = name;
                return value;
            }
        }

        return _o.DefaultCityFactor;
    }

    private static decimal RoundTo(decimal value, int step) => Math.Round(value / step, MidpointRounding.AwayFromZero) * step;
}
