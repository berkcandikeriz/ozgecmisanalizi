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
    string? EmploymentType,
    bool IsRemote = false);

/// <summary>Sağlayıcılara giden arama. <see cref="AltKeywords"/> meslek kataloğundaki İngilizce/eş anlamlı unvanlardır.</summary>
public sealed record JobQuery(
    string Keywords,
    string? Location,
    int Page,
    IReadOnlyList<string> AltKeywords,
    string? ClientIp,
    string? UserAgent)
{
    /// <summary>Önce kullanıcının yazdığı, sonra katalogdaki unvanlar; tekrarsız.</summary>
    public IEnumerable<string> AllKeywords => new[] { Keywords }.Concat(AltKeywords)
        .Where(k => !string.IsNullOrWhiteSpace(k))
        .DistinctBy(k => k.Trim().ToLowerInvariant());
}

public enum JobProviderScope
{
    /// <summary>Türkiye'deki ilanlar (konum filtresi uygulanır).</summary>
    Turkey,
    /// <summary>Dünya genelinde uzaktan çalışma ilanları.</summary>
    Remote
}

/// <summary>Resmi, herkese açık bir ilan API'sinden ilan getiren sağlayıcı.</summary>
public interface IJobProvider
{
    string Name { get; }
    bool IsEnabled { get; }
    JobProviderScope Scope { get; }

    /// <summary>Sağlayıcının kullanım koşullarının istediği kaynak bağlantısı (atıf).</summary>
    string Homepage { get; }

    Task<IReadOnlyList<RawJob>> SearchAsync(JobQuery query, CancellationToken ct);
}
