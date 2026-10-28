using OzgecmisAnalizi.Business.Abstractions;
using OzgecmisAnalizi.Business.Analysis;
using OzgecmisAnalizi.Core.Exceptions;
using OzgecmisAnalizi.Core.Extensions;
using OzgecmisAnalizi.Entities.Dtos;
using OzgecmisAnalizi.Entities.Enums;

namespace OzgecmisAnalizi.Business.Services;

public sealed class ProfessionService(ISkillCatalog catalog, ISalaryEstimator salary) : IProfessionService
{
    public async Task<IReadOnlyList<ProfessionListItemDto>> ListAsync(string? search, string? category, CancellationToken ct = default)
    {
        var all = await catalog.GetAsync(ct);
        var key = search.ToSearchKey();
        return all
            .Where(p => category.HasValue() == false || p.Category == category)
            .Where(p => key.Length == 0 || p.Name.ToSearchKey().Contains(key) || p.Skills.Any(s => s.Name.ToSearchKey().Contains(key)))
            .Select(p => new ProfessionListItemDto(
                p.Id, p.Code, p.Name, p.Category, p.Skills.Count,
                salary.Estimate(p, ExperienceLevel.Junior, null).MedianNet,
                salary.Estimate(p, ExperienceLevel.Kidemli, null).MedianNet))
            .ToList();
    }

    public async Task<ProfessionDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var p = (await catalog.GetAsync(ct)).FirstOrDefault(x => x.Id == id) ?? throw new NotFoundException("Meslek", id);
        return new ProfessionDto(
            p.Id, p.Code, p.Name, p.Category, null,
            p.Skills.Select(s => new ProfessionSkillDto(s.Name, ResumeAnalyzer.ImportanceName(s.Importance), s.Hint)).ToList(),
            CareerPathBuilder.Build(p, ExperienceLevel.Stajyer, p.Skills.ToList()),
            salary.EstimateAllLevels(p, null));
    }

    public async Task<IReadOnlyList<string>> CategoriesAsync(CancellationToken ct = default) =>
        (await catalog.GetAsync(ct)).Select(p => p.Category).Distinct().Order().ToList();
}
