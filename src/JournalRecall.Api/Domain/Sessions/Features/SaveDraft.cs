using MediatR;
using Microsoft.EntityFrameworkCore;
using JournalRecall.Api.Databases;
using JournalRecall.Api.Domain.Summaries.Services;

namespace JournalRecall.Api.Domain.Sessions.Features;

public static class SaveDraft
{
    /// <summary>
    /// <paramref name="BaseRevisionNumber"/> (the Raw Revision the client's edit was based on) and
    /// <paramref name="ClientSavedAt"/> (when the user actually saved) are the offline-replay fields
    /// (ADR-0013, issue 0032). Both optional — the web client omits them and behaves exactly as before.
    /// </summary>
    public sealed record Request(string RawText, int? BaseRevisionNumber = null, DateTimeOffset? ClientSavedAt = null);

    /// <summary>Returns false when the Session doesn't exist for the current user (→ 404).</summary>
    public sealed record Command(
        Guid SessionId, string RawText, int? BaseRevisionNumber = null, DateTimeOffset? ClientSavedAt = null)
        : IRequest<bool>;

    public sealed class Handler(JournalRecallDbContext db, SummaryStaleness staleness, TimeProvider timeProvider)
        : IRequestHandler<Command, bool>
    {
        public async Task<bool> Handle(Command request, CancellationToken cancellationToken)
        {
            var session = await db.Sessions
                .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken);
            if (session is null)
                return false;

            // A replayed offline edit carries the user's actual save time; an online save is happening now.
            var savedAt = request.ClientSavedAt ?? timeProvider.GetUtcNow();
            var draftAdvanced = session.SaveDraft(request.RawText, request.BaseRevisionNumber, savedAt);
            // People Metadata is a pure projection of the prose (PRD-0006, RICH-007): reconcile the
            // SessionPerson refs to the union of Raw + Cleaned @-mentions on every save.
            session.ReconcileMentionedPeople();
            await db.SaveChangesAsync(cancellationToken);

            // A real change to the current Draft makes the day's period Summaries Stale (issue 0014). An
            // LWW-losing contender only appended history (issue 0032), which Summaries never read.
            if (draftAdvanced)
                await staleness.MarkStaleForSessionAsync(session, cancellationToken);

            return true;
        }
    }
}
