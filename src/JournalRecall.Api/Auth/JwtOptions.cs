namespace JournalRecall.Api.Auth;

/// <summary>Binds the "Jwt" configuration section. The signing key must be set out-of-band in
/// production (env var); a dev key lives in appsettings.Development.json.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string SigningKey { get; set; } = "";
    public string Issuer { get; set; } = "JournalRecall";
    public string Audience { get; set; } = "JournalRecall";
    public int ExpiryMinutes { get; set; } = 60;
}
