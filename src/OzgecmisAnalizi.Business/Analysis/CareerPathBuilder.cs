using OzgecmisAnalizi.Entities.Content;
using OzgecmisAnalizi.Entities.Enums;

namespace OzgecmisAnalizi.Business.Analysis;

public static class CareerPathBuilder
{
    private static readonly string[] Durations = ["0-1 yıl", "1-3 yıl", "3-6 yıl", "6-10 yıl", "10+ yıl"];

    public static ExperienceLevel LevelFromYears(double years, bool hasExperience) => years switch
    {
        _ when !hasExperience || years < 0.75 => ExperienceLevel.Stajyer,
        < 3 => ExperienceLevel.Junior,
        < 6 => ExperienceLevel.Orta,
        < 10 => ExperienceLevel.Kidemli,
        _ => ExperienceLevel.Lider
    };

    /// <summary>Meslek basamakları; her basamak için eksik becerilerden odak alanı üretir.</summary>
    public static List<CareerStep> Build(CatalogProfession profession, ExperienceLevel current, IReadOnlyCollection<CatalogSkill> missing)
    {
        IReadOnlyList<string> titles = profession.CareerTitles.Count >= 5
            ? profession.CareerTitles
            : ["Stajyer", "Junior " + profession.Name, profession.Name, "Kıdemli " + profession.Name, profession.Name + " Lideri"];

        var core = missing.Where(m => m.Importance == SkillImportance.Core).Select(m => m.Name).ToList();
        var important = missing.Where(m => m.Importance == SkillImportance.Important).Select(m => m.Name).ToList();
        var nice = missing.Where(m => m.Importance == SkillImportance.NiceToHave).Select(m => m.Name).ToList();

        var steps = new List<CareerStep>();
        for (var i = 0; i < 5; i++)
        {
            var level = (ExperienceLevel)i;
            var focus = new List<string>();
            var offset = i - (int)current;
            if (offset <= 0)
            {
                focus.Add(offset == 0 ? "Bulunduğunuz seviye" : "Tamamlandı");
            }
            else
            {
                focus.AddRange(offset switch
                {
                    1 => core.Count > 0 ? core.Take(4) : important.Take(4),
                    2 => (core.Count > 0 ? important : nice).Take(4),
                    _ => Enumerable.Empty<string>()
                });

                if (level >= ExperienceLevel.Kidemli)
                {
                    focus.Add(level == ExperienceLevel.Lider ? "Ekip yönetimi, bütçe ve stratejik planlama" : "Mentorluk ve uçtan uca sorumluluk");
                }

                if (focus.Count == 0)
                {
                    focus.Add("Ölçülebilir başarılarla deneyim derinleştirme");
                }
            }

            steps.Add(new CareerStep
            {
                Level = SalaryEstimator.LevelName(level),
                Title = titles[i],
                Duration = Durations[i],
                Focus = focus,
                IsCurrent = level == current
            });
        }

        return steps;
    }
}
