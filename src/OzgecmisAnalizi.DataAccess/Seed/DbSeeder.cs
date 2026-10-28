using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OzgecmisAnalizi.DataAccess.Context;
using OzgecmisAnalizi.Entities.Concrete;
using OzgecmisAnalizi.Entities.Enums;

namespace OzgecmisAnalizi.DataAccess.Seed;

/// <summary>Meslek kataloğunu gömülü professions.json dosyasından yükler. Var olan kodlara dokunmaz, yenilerini ekler.</summary>
public sealed class DbSeeder(AppDbContext db, ILogger<DbSeeder> logger)
{
    private const string ResourceName = "OzgecmisAnalizi.DataAccess.Seed.professions.json";

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var seed = await ReadSeedAsync(ct);
        var existingCodes = await db.Professions.IgnoreQueryFilters().Select(p => p.Code).ToListAsync(ct);
        var toAdd = seed.Where(s => !existingCodes.Contains(s.Code)).ToList();
        if (toAdd.Count == 0)
        {
            return;
        }

        foreach (var s in toAdd)
        {
            var profession = new Profession
            {
                Code = s.Code,
                Name = s.Name,
                Category = s.Category,
                Description = s.Description,
                SearchKeywords = string.Join(", ", s.Keywords),
                JuniorSalaryFactor = s.JuniorFactor,
                SeniorSalaryFactor = s.SeniorFactor,
                CareerTitles = string.Join("|", s.CareerTitles)
            };

            AddSkills(profession, s.Core, SkillImportance.Core);
            AddSkills(profession, s.Important, SkillImportance.Important);
            AddSkills(profession, s.Nice, SkillImportance.NiceToHave);
            db.Professions.Add(profession);
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("{Count} meslek kataloğa eklendi.", toAdd.Count);
    }

    private static void AddSkills(Profession profession, IEnumerable<SeedSkill> skills, SkillImportance importance)
    {
        foreach (var skill in skills)
        {
            profession.Skills.Add(new ProfessionSkill
            {
                Name = skill.Name,
                Aliases = string.Join(",", skill.Aliases),
                Importance = importance,
                LearningHint = skill.Hint
            });
        }
    }

    private static async Task<List<SeedProfession>> ReadSeedAsync(CancellationToken ct)
    {
        await using var stream = typeof(DbSeeder).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Gömülü kaynak bulunamadı: {ResourceName}");
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        return await JsonSerializer.DeserializeAsync<List<SeedProfession>>(stream, options, ct) ?? [];
    }

    private sealed class SeedProfession
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string? Description { get; set; }
        public List<string> Keywords { get; set; } = [];
        public decimal JuniorFactor { get; set; }
        public decimal SeniorFactor { get; set; }
        public List<string> CareerTitles { get; set; } = [];
        public List<SeedSkill> Core { get; set; } = [];
        public List<SeedSkill> Important { get; set; } = [];
        public List<SeedSkill> Nice { get; set; } = [];
    }

    private sealed class SeedSkill
    {
        public string Name { get; set; } = string.Empty;
        public List<string> Aliases { get; set; } = [];
        public string? Hint { get; set; }
    }
}
