using OzgecmisAnalizi.Entities.Content;

namespace OzgecmisAnalizi.Entities.Dtos;

public record ProfessionListItemDto(Guid Id, string Code, string Name, string Category, int SkillCount, decimal JuniorNet, decimal SeniorNet);

public record ProfessionSkillDto(string Name, string Importance, string? LearningHint);

public record ProfessionDto(
    Guid Id,
    string Code,
    string Name,
    string Category,
    string? Description,
    IReadOnlyList<ProfessionSkillDto> Skills,
    IReadOnlyList<CareerStep> CareerPath,
    IReadOnlyList<SalaryEstimate> Salaries);
