using OzgecmisAnalizi.Business.Analysis;

namespace OzgecmisAnalizi.Business.Jobs;

/// <summary>
/// Tüm ilanları tek seferde veren (arama parametresi olmayan ya da sınırlı olan) uzaktan çalışma beslemeleri için
/// yerel süzgeç: unvan eşleşmesi + Türkiye'den başvurulabilecek bölge kontrolü.
/// </summary>
internal static class RemoteJobFilter
{
    public static IReadOnlyList<RawJob> Apply(IEnumerable<RawJob> jobs, JobQuery query, IReadOnlyCollection<string> allowedRegions, int max)
    {
        var phrases = query.AllKeywords
            .Select(k => SkillMatcher.NormalizeKey(k).Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(t => t.Length > 1).ToArray())
            .Where(t => t.Length > 0)
            .ToList();
        if (phrases.Count == 0)
        {
            return [];
        }

        return jobs
            .Where(j => RegionAllowed(j.Location, allowedRegions))
            .Where(j =>
            {
                var title = SkillMatcher.PrepareText(j.Title);
                return phrases.Any(tokens => tokens.All(t => SkillMatcher.Contains(title, t)));
            })
            .OrderByDescending(j => j.PostedAt)
            .Take(max)
            .ToList();
    }

    /// <summary>Konum boşsa ya da "Worldwide / Anywhere / Europe / EMEA / Turkey" gibi bir bölge içeriyorsa uygundur.</summary>
    public static bool RegionAllowed(string? location, IReadOnlyCollection<string> allowedRegions)
    {
        if (string.IsNullOrWhiteSpace(location) || allowedRegions.Count == 0)
        {
            return true;
        }

        return allowedRegions.Any(r => location.Contains(r, StringComparison.OrdinalIgnoreCase));
    }
}
