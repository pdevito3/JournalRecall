namespace JournalRecall.Api.Domain.Sessions.Dtos;

/// <summary>
/// A Session as the client sees it: the Raw draft plus the AI-derived Cleaned copy, Synopsis, and the
/// effective <see cref="Sessions.CleanupStatus"/> (with Stale derived). <see cref="CleanedDraft"/> and
/// <see cref="Synopsis"/> are empty until a Cleanup run succeeds.
/// </summary>
public sealed record SessionDto(
    Guid Id,
    DateTimeOffset CreatedAt,
    string RawDraft,
    string CleanedDraft,
    string Synopsis,
    CleanupStatus CleanupStatus,
    bool CleanedHasHandEdits)
{
    /// <summary>Projects a loaded Session, surfacing its <see cref="Session.EffectiveCleanupStatus"/>.</summary>
    public static SessionDto From(Session session) => new(
        session.Id, session.CreatedAt, session.RawDraft, session.CleanedDraft, session.Synopsis,
        session.EffectiveCleanupStatus, session.CleanedHasHandEdits);
}
