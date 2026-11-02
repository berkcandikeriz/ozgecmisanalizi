namespace OzgecmisAnalizi.Entities.Dtos;

public class JobSearchQuery
{
    public string? Keywords { get; set; }
    public string? Location { get; set; }
    public Guid? ResumeId { get; set; }
    public int Page { get; set; } = 1;

    /// <summary>Dünya genelindeki uzaktan çalışma ilanları da eklensin mi (boşsa sunucu varsayılanı).</summary>
    public bool? IncludeRemote { get; set; }
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
    IReadOnlyList<string> MatchedSkills,
    bool IsTracked = false,
    bool IsRemote = false);

/// <summary>Sorgulanan ilan kaynağı ve kullanım koşullarının istediği atıf bağlantısı.</summary>
public record JobProviderDto(string Name, string Homepage, string Scope, int Count);

/// <summary>API anahtarı olmadan da çalışan, ilgili sitenin arama sonuç sayfasına giden bağlantı.</summary>
public record JobSiteLinkDto(string Site, string Url, string Description);

public record JobSearchResultDto(
    string Keywords,
    string? Location,
    IReadOnlyList<JobListingDto> Listings,
    IReadOnlyList<JobSiteLinkDto> SiteLinks,
    IReadOnlyList<string> ActiveProviders,
    IReadOnlyList<string> Warnings,
    int Page = 1,
    int HiddenCount = 0,
    IReadOnlyList<JobProviderDto>? Providers = null,
    IReadOnlyList<string>? Notices = null,
    bool IncludeRemote = true);
