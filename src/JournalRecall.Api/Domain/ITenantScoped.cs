namespace JournalRecall.Api.Domain;

/// <summary>
/// Opt-in marker for an entity the Privacy invariant scopes to the current User (ADR-0012). The
/// DbContext applies the per-User <c>TenantFilter</c> automatically to every entity implementing this,
/// so a new tenant-scoped entity is protected by adding the interface alone — no hand-written
/// <c>HasQueryFilter</c> to forget.
///
/// Scoping keys off this marker, <b>not</b> the presence of a <c>UserId</c> column, because the two
/// diverge: <see cref="Identity.RefreshToken"/> owns a <c>UserId</c> but must stay unscoped so rotation
/// works with no current user established (ADR-0005), so it deliberately does <b>not</b> implement this.
/// </summary>
public interface ITenantScoped
{
    /// <summary>The owning User the tenant filter scopes rows to.</summary>
    Guid UserId { get; }
}
