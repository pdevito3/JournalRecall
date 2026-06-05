using MediatR;
using Microsoft.EntityFrameworkCore;
using JournalRecall.Api.Databases;
using JournalRecall.Api.Domain.Sessions.Dtos;

namespace JournalRecall.Api.Domain.Sessions.Features;

public static class GetCleanedRevisions
{
    /// <summary>Null when the Session doesn't exist for the current user (→ 404).</summary>
    public sealed record Query(Guid SessionId) : IRequest<IReadOnlyList<CleanedRevisionSummaryDto>?>;

    public sealed class Handler(JournalRecallDbContext db)
        : IRequestHandler<Query, IReadOnlyList<CleanedRevisionSummaryDto>?>
    {
        public async Task<IReadOnlyList<CleanedRevisionSummaryDto>?> Handle(Query request, CancellationToken cancellationToken)
        {
            var session = await db.Sessions
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken);
            if (session is null)
                return null;

            return session.CleanedRevisions
                .OrderByDescending(r => r.RevisionNumber) // newest first
                .Select(r => new CleanedRevisionSummaryDto(r.RevisionNumber, r.CreatedAt))
                .ToList();
        }
    }
}
