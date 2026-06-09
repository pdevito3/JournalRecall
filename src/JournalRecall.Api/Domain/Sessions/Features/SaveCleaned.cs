using MediatR;
using Microsoft.EntityFrameworkCore;
using JournalRecall.Api.Databases;
using JournalRecall.Api.Domain.Summaries.Services;

namespace JournalRecall.Api.Domain.Sessions.Features;

public static class SaveCleaned
{
    /// <summary><paramref name="ClientSavedAt"/> is the optional offline-replay save time (ADR-0013,
    /// issue 0032); the web client omits it and behaves exactly as before.</summary>
    public sealed record Request(string CleanedText, DateTimeOffset? ClientSavedAt = null);

    /// <summary>Returns false when the Session doesn't exist for the current user (→ 404).</summary>
    public sealed record Command(Guid SessionId, string CleanedText, DateTimeOffset? ClientSavedAt = null)
        : IRequest<bool>;

    public sealed class Handler(JournalRecallDbContext db, SummaryStaleness staleness)
        : IRequestHandler<Command, bool>
    {
        public async Task<bool> Handle(Command request, CancellationToken cancellationToken)
        {
            var session = await db.Sessions
                .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken);
            if (session is null)
                return false;

            // Last-write-wins for queued offline edits (ADR-0013, issue 0032): a hand-edit the user saved
            // before this Session's last write must not clobber newer server state — acknowledged, not applied.
            if (request.ClientSavedAt is { } clientSavedAt && clientSavedAt < session.UpdatedAt)
                return true;

            // A user hand-edit of the Cleaned copy — appends a Cleaned Revision, flags hand-edits, and
            // never touches Raw (ADR-0003, CONTEXT.md).
            var changed = session.EditCleaned(request.CleanedText);
            // People Metadata projects from the prose (PRD-0006, RICH-007): reconcile to the union of
            // Raw + Cleaned @-mentions so editing the Cleaned copy retags People too.
            session.ReconcileMentionedPeople();
            await db.SaveChangesAsync(cancellationToken);

            // A changed Cleaned copy is what a Day/Week Summary reads — invalidate the period chain (issue 0014).
            if (changed)
                await staleness.MarkStaleForSessionAsync(session, cancellationToken);

            return true;
        }
    }
}
