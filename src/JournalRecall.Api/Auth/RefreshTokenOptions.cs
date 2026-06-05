namespace JournalRecall.Api.Auth;

/// <summary>
/// Tunables for the refresh-token lifecycle (ADR-0005). The window slides on every use with no absolute
/// cap; the grace window tolerates a double-fired refresh without falsely revoking the chain.
/// </summary>
public sealed class RefreshTokenOptions
{
    public const string SectionName = "RefreshToken";

    /// <summary>How long an unused refresh token stays valid; reset on every use. No absolute cap.</summary>
    public TimeSpan InactivityWindow { get; set; } = TimeSpan.FromDays(60);

    /// <summary>How long after a token is rotated a re-presentation is treated as a benign double-fire
    /// (re-issue) rather than reuse/theft (chain revoke).</summary>
    public TimeSpan GraceWindow { get; set; } = TimeSpan.FromSeconds(30);
}
