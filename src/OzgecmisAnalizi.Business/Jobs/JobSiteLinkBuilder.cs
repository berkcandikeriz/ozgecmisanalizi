using Microsoft.Extensions.Options;
using OzgecmisAnalizi.Business.Options;
using OzgecmisAnalizi.Core.Extensions;
using OzgecmisAnalizi.Entities.Dtos;

namespace OzgecmisAnalizi.Business.Jobs;

/// <summary>LinkedIn, Indeed, Kariyer.net gibi sitelerin kendi arama sonuçlarına giden bağlantıları üretir (kazıma yapılmaz).</summary>
public sealed class JobSiteLinkBuilder(IOptions<JobSearchOptions> options)
{
    public IReadOnlyList<JobSiteLinkDto> Build(string keywords, string? location)
    {
        var loc = string.IsNullOrWhiteSpace(location) ? options.Value.DefaultLocation : location;
        return options.Value.SiteLinks.Select(kv => new JobSiteLinkDto(
                kv.Key,
                kv.Value
                    .Replace("{q}", Uri.EscapeDataString(keywords))
                    .Replace("{l}", Uri.EscapeDataString(loc))
                    .Replace("{qslug}", keywords.ToSlug())
                    .Replace("{lslug}", loc.ToSlug()),
                $"{kv.Key} üzerinde \"{keywords}\" · {loc} ilanları"))
            .ToList();
    }
}
