using JournalRecall.Api.Domain.Sessions.DomainEvents;

namespace JournalRecall.Api.Domain.Sessions;

/// <summary>
/// The core aggregate (CONTEXT.md): one act of journaling started by a User on a given day. Owns the
/// Raw text the user wrote. There is no InProgress/Completed lifecycle — a Session simply exists.
///
/// Phase 2 (this slice) carries only the live, autosaved <see cref="RawDraft"/>; the append-only Raw
/// Revision stream minted at save points lands in issue 0005. Raw is human-owned and stored exactly as
/// typed — never mutated by the server.
/// </summary>
public sealed class Session : BaseEntity
{
    private readonly List<RawRevision> _rawRevisions = [];
    private readonly List<CleanedRevision> _cleanedRevisions = [];

    public Guid UserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public string RawDraft { get; private set; } = string.Empty;

    /// <summary>The AI-derived, polished copy produced by the latest Cleanup (CONTEXT.md). Empty until a run succeeds.</summary>
    public string CleanedDraft { get; private set; } = string.Empty;

    /// <summary>The short AI recap of this Session, written by Cleanup (CONTEXT.md). Empty until a run succeeds.</summary>
    public string Synopsis { get; private set; } = string.Empty;

    /// <summary>
    /// The stored coarse Cleanup status — never <see cref="Sessions.CleanupStatus.Stale"/>, which is
    /// derived (see <see cref="EffectiveCleanupStatus"/>).
    /// </summary>
    public CleanupStatus CleanupStatus { get; private set; } = CleanupStatus.NotRun;

    /// <summary>The Raw Revision number that the last successful Cleanup read. 0 when never cleaned.</summary>
    public int LastCleanedRawRevisionNumber { get; private set; }

    /// <summary>The Raw Revision number captured when the in-flight Cleanup began. Not persisted.</summary>
    private int _cleaningFromRawRevisionNumber;

    /// <summary>The append-only Raw Revision stream, oldest first (ADR-0003).</summary>
    public IReadOnlyList<RawRevision> RawRevisions => _rawRevisions;

    /// <summary>The append-only Cleaned Revision stream, oldest first (ADR-0003).</summary>
    public IReadOnlyList<CleanedRevision> CleanedRevisions => _cleanedRevisions;

    /// <summary>The latest Raw Revision number (== the count, since numbers are sequential). 0 when empty.</summary>
    public int LatestRawRevisionNumber => _rawRevisions.Count;

    /// <summary>
    /// The Cleanup status as the user sees it: the stored status, except a <see cref="CleanupStatus.Clean"/>
    /// Session whose Raw has advanced past the last cleaned Revision reads as <see cref="CleanupStatus.Stale"/>
    /// (CONTEXT.md — "Stale means the latest Raw Revision is newer than the last successful Cleanup").
    /// </summary>
    public CleanupStatus EffectiveCleanupStatus =>
        CleanupStatus == CleanupStatus.Clean && LatestRawRevisionNumber > LastCleanedRawRevisionNumber
            ? CleanupStatus.Stale
            : CleanupStatus;

    private Session() { } // EF

    public static Session Create(Guid userId)
    {
        var session = new Session { UserId = userId };
        session.QueueDomainEvent(new SessionCreated(session.Id));
        return session;
    }

    /// <summary>
    /// Save point for Raw: the live Draft mutates to the user's text (verbatim), and when the content
    /// actually changed a new immutable Revision is appended. A debounced/explicit save that doesn't
    /// change the text mints nothing, so rapid keystrokes never each create a Revision.
    /// </summary>
    public void SaveDraft(string rawText)
    {
        rawText ??= string.Empty;
        var changed = !string.Equals(rawText, LatestRawContent, StringComparison.Ordinal);

        RawDraft = rawText;
        if (changed)
            _rawRevisions.Add(new RawRevision(_rawRevisions.Count + 1, rawText));
    }

    private string LatestRawContent => _rawRevisions.Count == 0 ? string.Empty : _rawRevisions[^1].Content;

    /// <summary>
    /// Marks the Session as having a Cleanup in flight and snapshots the Raw Revision the run will read,
    /// so the resulting Cleaned copy is pinned to that exact Raw version (later Raw edits flip it Stale).
    /// </summary>
    public void BeginCleanup()
    {
        _cleaningFromRawRevisionNumber = LatestRawRevisionNumber;
        CleanupStatus = CleanupStatus.Running;
    }

    /// <summary>
    /// A successful Cleanup: appends a Cleaned Revision, sets the current Cleaned copy + Synopsis, and
    /// marks the Session <see cref="CleanupStatus.Clean"/>. Raw is never touched (CONTEXT.md, ADR-0003).
    /// </summary>
    public void CompleteCleanup(string cleanedText, string synopsis)
    {
        cleanedText ??= string.Empty;
        CleanedDraft = cleanedText;
        Synopsis = synopsis ?? string.Empty;
        _cleanedRevisions.Add(new CleanedRevision(_cleanedRevisions.Count + 1, cleanedText));
        LastCleanedRawRevisionNumber = _cleaningFromRawRevisionNumber;
        CleanupStatus = CleanupStatus.Clean;
    }

    /// <summary>A failed Cleanup: records the failure without corrupting Raw or any prior Cleaned copy.</summary>
    public void FailCleanup() => CleanupStatus = CleanupStatus.Failed;
}
