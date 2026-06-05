using Microsoft.AspNetCore.Identity;

namespace JournalRecall.Api.Domain.Identity;

/// <summary>
/// The tenant boundary (CONTEXT.md). All journal data belongs to exactly one User and is strictly
/// private to that User. Backed by ASP.NET Core Identity with Guid keys to match the BaseEntity
/// convention so a Session's UserId is a Guid everywhere. Roles (Admin/Member) arrive in issue 0003.
/// </summary>
public sealed class User : IdentityUser<Guid>
{
    /// <summary>
    /// The user's IANA timezone (e.g. "America/New_York"), defaulted from the browser on first run.
    /// Null means "not yet set" — the effective zone is UTC. Journaling-day/week/month membership is
    /// derived by projecting a Session's UTC timestamp into this zone (CONTEXT.md).
    /// </summary>
    public string? TimeZoneId { get; set; }

    /// <summary>
    /// Per-user geo opt-in (CONTEXT.md Location). Off by default: when false, no location is captured or
    /// stored for the user's Sessions. When true, a single lat/long may be stamped at Session creation
    /// (the user may still decline per Session). Strictly per-user — never affects another User.
    /// </summary>
    public bool LocationCaptureEnabled { get; set; }

    /// <summary>
    /// Set by an Admin to disable the account (issue 0016): a disabled User cannot log in. Their journal
    /// data is untouched and still strictly private — disabling is an access decision, not a data one.
    /// </summary>
    public bool IsDisabled { get; set; }

    /// <summary>
    /// True when the User must replace a temporary password before doing anything else (issue 0024): set
    /// on Admin-create and Admin-reset, cleared when the User sets their own password. While set, the
    /// server confines them to the change-password flow (the password-change sentinel).
    /// </summary>
    public bool MustChangePassword { get; set; }
}
