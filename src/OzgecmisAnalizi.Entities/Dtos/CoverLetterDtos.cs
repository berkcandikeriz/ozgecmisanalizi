namespace OzgecmisAnalizi.Entities.Dtos;

public record CoverLetterRequest(Guid ResumeId, string JobTitle, string Company, string? JobDescription, string? Tone, Guid? JobApplicationId);

public record CoverLetterDto(Guid Id, Guid? ResumeId, string JobTitle, string Company, string Content, string Engine, DateTime CreatedAt);

public record CoverLetterUpdateRequest(string Content);
