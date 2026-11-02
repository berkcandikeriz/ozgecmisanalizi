namespace OzgecmisAnalizi.Entities.Dtos;

public record CoverLetterRequest(Guid ResumeId, string JobTitle, string Company, string? JobDescription, string? Tone, Guid? JobApplicationId, Guid? StyleReferenceId = null);

public record CoverLetterDto(Guid Id, Guid? ResumeId, string JobTitle, string Company, string Content, string Engine, DateTime CreatedAt, Guid? JobApplicationId = null);

public record CoverLetterUpdateRequest(string Content);
