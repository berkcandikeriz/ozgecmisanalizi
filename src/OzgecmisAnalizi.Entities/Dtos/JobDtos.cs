namespace OzgecmisAnalizi.Entities.Dtos;

public class JobSearchQuery
{
    public string? Keywords { get; set; }
    public string? Location { get; set; }
    public Guid? ResumeId { get; set; }
    public int Page { get; set; } = 1;
}

public record JobListingDto(
    string Id,
    string Title,
    string Company,
    string? Location,
    string? Snippet,
    string Url,
    string Source,
    DateTime? PostedAt,
    string? Salary,
    string? EmploymentType,
    int MatchScore,
    IReadOnlyList<string> MatchedSkills);

/// <summary>API anahtarı olmadan da çalışan, ilgili sitenin arama sonuç sayfasına giden bağlantı.</summary>
public record JobSiteLinkDto(string Site, string Url, string Description);

public record JobSearchResultDto(
    string Keywords,
    string? Location,
    IReadOnlyList<JobListingDto> Listings,
    IReadOnlyList<JobSiteLinkDto> SiteLinks,
    IReadOnlyList<string> ActiveProviders,
    IReadOnlyList<string> Warnings);
