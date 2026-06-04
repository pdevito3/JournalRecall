using MediatR;
using Microsoft.EntityFrameworkCore;
using JournalRecall.Api.Databases;
using JournalRecall.Api.Domain.Sessions.Dtos;

namespace JournalRecall.Api.Domain.Sessions.Features;

public static class GetRawRevisions
{
    /// <summary>Null when the Session doesn't exist for the current user (→ 404).</summary>
    public sealed record Query(Guid SessionId) : IRequest<IReadOnlyList<RawRevisionSummaryDto>?>;

    public sealed class Handler(JournalRecallDbContext db)
        : IRequestHandler<Query, IReadOnlyList<RawRevisionSummaryDto>?>
    {
        public async Task<IReadOnlyList<RawRevisionSummaryDto>?> Handle(Query request, CancellationToken cancellationToken)
        {
            var session = await db.Sessions
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken);
            if (session is null)
                return null;

            return session.RawRevisions
                .OrderByDescending(r => r.RevisionNumber) // newest first
                .Select(r => new RawRevisionSummaryDto(r.RevisionNumber, r.CreatedAt))
                .ToList();
        }
    }
}
