using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OzgecmisAnalizi.Core.Extensions;
using OzgecmisAnalizi.DataAccess.Context;
using OzgecmisAnalizi.Entities.Enums;

namespace OzgecmisAnalizi.Business.Analysis;

public sealed record CatalogSkill(string Name, SkillImportance Importance, string? Hint, IReadOnlyList<string> Keys);

public sealed record CatalogProfession(
    Guid Id,
    string Code,
    string Name,
    string Category,
    IReadOnlyList<string> NameKeys,
    IReadOnlyList<string> SearchKeywords,
    decimal JuniorFactor,
    decimal SeniorFactor,
    IReadOnlyList<string> CareerTitles,
    IReadOnlyList<CatalogSkill> Skills);

public interface ISkillCatalog
{
    Task<IReadOnlyList<CatalogProfession>> GetAsync(CancellationToken ct = default);
    Task<IReadOnlyList<CatalogSkill>> AllSkillsAsync(CancellationToken ct = default);
    void Invalidate();
}

/// <summary>Meslek ve beceri kataloğunu bellekte tutar (30 dk). Eşleştirme anahtarları önceden katlanır.</summary>
public sealed class SkillCatalog(IServiceScopeFactory scopeFactory) : ISkillCatalog
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(30);
    private readonly SemaphoreSlim _lock = new(1, 1);
    private IReadOnlyList<CatalogProfession>? _cache;
    private IReadOnlyList<CatalogSkill>? _allSkills;
    private DateTime _loadedAt;

    public async Task<IReadOnlyList<CatalogProfession>> GetAsync(CancellationToken ct = default)
    {
        if (_cache is not null && DateTime.UtcNow - _loadedAt < Ttl)
        {
            return _cache;
        }

        await _lock.WaitAsync(ct);
        try
        {
            if (_cache is not null && DateTime.UtcNow - _loadedAt < Ttl)
            {
                return _cache;
            }

            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var professions = await db.Professions.AsNoTracking().Include(p => p.Skills).ToListAsync(ct);

            _cache = professions.Select(p => new CatalogProfession(
                    p.Id,
                    p.Code,
                    p.Name,
                    p.Category,
                    BuildNameKeys(p.Name),
                    p.SearchKeywords.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                    p.JuniorSalaryFactor,
                    p.SeniorSalaryFactor,
                    p.CareerTitles.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                    p.Skills.OrderByDescending(s => s.Importance).ThenBy(s => s.Name)
                        .Select(s => new CatalogSkill(s.Name, s.Importance, s.LearningHint, BuildSkillKeys(s.Name, s.Aliases)))
                        .ToList()))
                .OrderBy(p => p.Name)
                .ToList();

            _allSkills = _cache.SelectMany(p => p.Skills)
                .GroupBy(s => s.Name.ToSearchKey())
                .Select(g => g.First())
                .ToList();
            _loadedAt = DateTime.UtcNow;
            return _cache;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<CatalogSkill>> AllSkillsAsync(CancellationToken ct = default)
    {
        await GetAsync(ct);
        return _allSkills ?? [];
    }

    public void Invalidate() => _cache = null;

    private static IReadOnlyList<string> BuildSkillKeys(string name, string aliases)
    {
        var keys = new List<string> { SkillMatcher.NormalizeKey(name) };
        keys.AddRange(aliases.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(SkillMatcher.NormalizeKey));

        // "Adobe Photoshop" → "photoshop" gibi parantez/eğik çizgili adların parçaları da anahtar olsun.
        foreach (var part in name.Split(['/', '(', ')'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            keys.Add(SkillMatcher.NormalizeKey(part));
        }

        return keys.Where(k => k.Length >= 1).Distinct().ToList();
    }

    private static IReadOnlyList<string> BuildNameKeys(string name)
    {
        var baseName = name.Split('(')[0];
        var keys = new List<string> { SkillMatcher.NormalizeKey(baseName) };
        keys.AddRange(baseName.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(SkillMatcher.NormalizeKey));
        return keys.Where(k => k.Length > 2).Distinct().ToList();
    }
}
