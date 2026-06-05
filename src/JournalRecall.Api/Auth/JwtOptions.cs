namespace JournalRecall.Api.Auth;

/// <summary>Binds the "Jwt" configuration section. The signing key must be set out-of-band in
/// production (env var); a dev key lives in appsettings.Development.json.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string SigningKey { get; set; } = "";
    public string Issuer { get; set; } = "JournalRecall";
    public string Audience { get; set; } = "JournalRecall";
    // Short access-token lifetime so revocation (logout/disable/password-change) takes effect promptly;
    // the silent refresh-token rotation (ADR-0005) hides the renewal from the User.
    public int ExpiryMinutes { get; set; } = 15;
}
