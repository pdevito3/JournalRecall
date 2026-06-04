using Mapster;
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
            // returns nothing (Privacy invariant), which the endpoint surfaces as 404.
            var session = await db.Sessions
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken);

            return session?.Adapt<SessionDto>();
        }
    }
}
