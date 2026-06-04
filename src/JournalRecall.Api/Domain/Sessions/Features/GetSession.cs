using MediatR;
using Microsoft.EntityFrameworkCore;
using JournalRecall.Api.Databases;
using JournalRecall.Api.Domain.Sessions.Dtos;

namespace JournalRecall.Api.Domain.Sessions.Features;

public static class GetSession
{
    public sealed record Query(Guid SessionId) : IRequest<SessionDto?>;

    public sealed class Handler(JournalRecallDbContext db) : IRequestHandler<Query, SessionDto?>
    {
        public async Task<SessionDto?> Handle(Query request, CancellationToken cancellationToken)
        {
            // The global query filter scopes this to the current user; another user's id simply
            // returns nothing (Privacy invariant), which the endpoint surfaces as 404. Project so a
            // read doesn't pull the whole Raw Revision history.
            return await db.Sessions
                .AsNoTracking()
                .Where(s => s.Id == request.SessionId)
                .Select(s => new SessionDto(s.Id, s.CreatedAt, s.RawDraft))
                .FirstOrDefaultAsync(cancellationToken);
        }
    }
}
