namespace JournalRecall.Api.Auth;

/// <summary>
/// Feature-access permissions checked by HeimGuard. Permissions — not roles — gate endpoints, so the
/// role→permission mapping (see UserPolicyHandler) can evolve without touching call sites.
/// </summary>
public static class Permissions
{
    /// <summary>Access to the non-journal admin surface (user management, app settings).</summary>
    public const string CanAccessAdmin = "CanAccessAdmin";
}
