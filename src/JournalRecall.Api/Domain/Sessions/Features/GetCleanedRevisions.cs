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
            // Project the summary rows in SQL: select only (RevisionNumber, CreatedAt) per Revision so the
            // owned Revision bodies and the Session's other owned collections are never materialized.
            var summary = await db.Sessions
                .AsNoTracking()
                .Where(s => s.Id == request.SessionId)
                .Select(s => new
                {
                    Revisions = s.CleanedRevisions
                        .OrderByDescending(r => r.RevisionNumber) // newest first
                        .Select(r => new CleanedRevisionSummaryDto(r.RevisionNumber, r.CreatedAt))
                        .ToList(),
                })
                .FirstOrDefaultAsync(cancellationToken);

            // Null wrapper => the Session doesn't exist for this user (→ 404); empty list => no Revisions yet.
            return summary?.Revisions;
        }
    }
}
