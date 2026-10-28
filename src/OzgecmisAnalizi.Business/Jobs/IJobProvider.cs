namespace OzgecmisAnalizi.Business.Jobs;

public sealed record RawJob(
    string Id,
    string Title,
    string Company,
    string? Location,
    string? Snippet,
    string Url,
    string Source,
    DateTime? PostedAt,
    string? Salary,
    string? EmploymentType);

/// <summary>Resmi bir ilan API'sinden ilan getiren sağlayıcı.</summary>
public interface IJobProvider
{
    string Name { get; }
    bool IsEnabled { get; }
    Task<IReadOnlyList<RawJob>> SearchAsync(string keywords, string? location, int page, CancellationToken ct);
}
