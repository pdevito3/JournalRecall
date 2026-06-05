using MediatR;
using Microsoft.EntityFrameworkCore;
using JournalRecall.Api.Databases;

namespace JournalRecall.Api.Domain.Sessions.Features;

public static class SaveCleaned
{
    public sealed record Request(string CleanedText);

    /// <summary>Returns false when the Session doesn't exist for the current user (→ 404).</summary>
    public sealed record Command(Guid SessionId, string CleanedText) : IRequest<bool>;

    public sealed class Handler(JournalRecallDbContext db) : IRequestHandler<Command, bool>
    {
        public async Task<bool> Handle(Command request, CancellationToken cancellationToken)
        {
            var session = await db.Sessions
                .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken);
            if (session is null)
                return false;

            // A user hand-edit of the Cleaned copy — appends a Cleaned Revision, flags hand-edits, and
            // never touches Raw (ADR-0003, CONTEXT.md).
            session.EditCleaned(request.CleanedText);
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
    }
}
