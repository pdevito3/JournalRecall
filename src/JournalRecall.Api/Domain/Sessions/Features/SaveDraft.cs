using MediatR;
using Microsoft.EntityFrameworkCore;
using JournalRecall.Api.Databases;
using JournalRecall.Api.Domain.Summaries.Services;

namespace JournalRecall.Api.Domain.Sessions.Features;

public static class SaveDraft
{
    public sealed record Request(string RawText);

    /// <summary>Returns false when the Session doesn't exist for the current user (→ 404).</summary>
    public sealed record Command(Guid SessionId, string RawText) : IRequest<bool>;

    public sealed class Handler(JournalRecallDbContext db, SummaryStaleness staleness)
        : IRequestHandler<Command, bool>
    {
        public async Task<bool> Handle(Command request, CancellationToken cancellationToken)
        {
            var session = await db.Sessions
                .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken);
            if (session is null)
                return false;

            var revisionsBefore = session.LatestRawRevisionNumber;
            session.SaveDraft(request.RawText);
            await db.SaveChangesAsync(cancellationToken);

            // A real Raw change (a Revision was appended) makes the day's period Summaries Stale (issue 0014).
            if (session.LatestRawRevisionNumber > revisionsBefore)
                await staleness.MarkStaleForSessionAsync(session, cancellationToken);

            return true;
        }
    }
}
