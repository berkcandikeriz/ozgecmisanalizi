namespace OzgecmisAnalizi.Entities.Content;

/// <summary>Analiz sonucunun ayrıntıları. JSON kolonu olarak saklanır.</summary>
public class AnalysisDetails
{
    public List<SectionFeedback> Sections { get; set; } = [];
    public List<AtsCheck> AtsChecks { get; set; } = [];
    public List<string> DetectedSkills { get; set; } = [];
    public List<SkillGap> MissingSkills { get; set; } = [];
    public List<ProfessionMatch> ProfessionMatches { get; set; } = [];
    public List<CareerStep> CareerPath { get; set; } = [];
    public SalaryEstimate? Salary { get; set; }
    public List<string> Strengths { get; set; } = [];
    public List<string> Improvements { get; set; } = [];
    public string? AiSummary { get; set; }
    public double ExperienceYears { get; set; }
}

public class SectionFeedback
{
    public string Section { get; set; } = string.Empty;
    public int Score { get; set; }
    public List<string> Tips { get; set; } = [];
}

public class AtsCheck
{
    public string Name { get; set; } = string.Empty;
    public bool Passed { get; set; }
    public string? Detail { get; set; }
}

public class SkillGap
{
    public string Skill { get; set; } = string.Empty;
    public string Importance { get; set; } = string.Empty;
    public string? HowToLearn { get; set; }
}

public class ProfessionMatch
{
    public Guid ProfessionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int MatchPercent { get; set; }
}

public class CareerStep
{
    public string Level { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
    public List<string> Focus { get; set; } = [];
    public bool IsCurrent { get; set; }
}

public class SalaryEstimate
{
    public decimal MinNet { get; set; }
    public decimal MaxNet { get; set; }
    public decimal MedianNet { get; set; }
    public string Currency { get; set; } = "TRY";
    public string Level { get; set; } = string.Empty;
    public string? City { get; set; }
    public string Basis { get; set; } = string.Empty;
}
