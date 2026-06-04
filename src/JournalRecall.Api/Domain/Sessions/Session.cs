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

    public Guid UserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public string RawDraft { get; private set; } = string.Empty;

    /// <summary>The append-only Raw Revision stream, oldest first (ADR-0003).</summary>
    public IReadOnlyList<RawRevision> RawRevisions => _rawRevisions;

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
}
