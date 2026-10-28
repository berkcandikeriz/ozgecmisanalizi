using OzgecmisAnalizi.Entities.Concrete;
using OzgecmisAnalizi.Entities.Dtos;

namespace OzgecmisAnalizi.Business.Mapping;

public static class Mappers
{
    public static UserDto ToDto(this User u) => new(u.Id, u.FullName, u.Email, u.Role.ToString(), u.City);

    public static ResumeDto ToDto(this Resume r) =>
        new(r.Id, r.Title, r.Template, r.Source, r.TargetProfessionId, r.OriginalFileName, r.Content, r.CreatedAt, r.UpdatedAt);

    public static AnalysisDto ToDto(this ResumeAnalysis a, string resumeTitle, string? professionName) =>
        new(a.Id, a.ResumeId, resumeTitle, a.ProfessionId, professionName, a.OverallScore, a.ComprehensivenessScore,
            a.StructureScore, a.ClarityScore, a.AtsScore, a.SkillMatchScore, a.Label, a.TopPercent, a.Engine, a.Details, a.CreatedAt);

    public static JobApplicationDto ToDto(this JobApplication j) =>
        new(j.Id, j.JobTitle, j.Company, j.Location, j.Url, j.Source, j.Description, j.Stage, j.MatchScore, j.ResumeId,
            j.AppliedAt, j.InterviewAt, j.Notes, j.CreatedAt);

    public static CoverLetterDto ToDto(this CoverLetter c) =>
        new(c.Id, c.ResumeId, c.JobTitle, c.Company, c.Content, c.Engine, c.CreatedAt);
}
