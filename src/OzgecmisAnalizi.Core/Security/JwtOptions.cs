namespace OzgecmisAnalizi.Core.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "OzgecmisAnalizi";
    public string Audience { get; set; } = "OzgecmisAnalizi.Clients";

    /// <summary>En az 32 karakter. Üretimde ortam değişkeni veya user-secrets ile verilmelidir.</summary>
    public string Key { get; set; } = string.Empty;

    public int ExpiryMinutes { get; set; } = 60 * 24 * 7;
}
