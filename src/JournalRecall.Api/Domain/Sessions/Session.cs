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
    public Guid UserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public string RawDraft { get; private set; } = string.Empty;

    private Session() { } // EF

    public static Session Create(Guid userId)
    {
        var session = new Session { UserId = userId };
        session.QueueDomainEvent(new SessionCreated(session.Id));
        return session;
    }

    /// <summary>Replace the live Draft with the user's text, verbatim (autosave save point).</summary>
    public void SaveDraft(string rawText) => RawDraft = rawText ?? string.Empty;
}
