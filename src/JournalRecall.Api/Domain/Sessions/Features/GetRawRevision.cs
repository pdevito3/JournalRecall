using MediatR;
using Microsoft.EntityFrameworkCore;
using JournalRecall.Api.Databases;
using JournalRecall.Api.Domain.Sessions.Dtos;

namespace JournalRecall.Api.Domain.Sessions.Features;

public static class GetRawRevision
{
    /// <summary>Null when the Session (for this user) or the Revision number doesn't exist (→ 404).</summary>
    public sealed record Query(Guid SessionId, int RevisionNumber) : IRequest<RawRevisionDto?>;

    public sealed class Handler(JournalRecallDbContext db) : IRequestHandler<Query, RawRevisionDto?>
    {
        public async Task<RawRevisionDto?> Handle(Query request, CancellationToken cancellationToken)
        {
            var session = await db.Sessions
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken);

            var revision = session?.RawRevisions.FirstOrDefault(r => r.RevisionNumber == request.RevisionNumber);
            return revision is null
                ? null
                : new RawRevisionDto(revision.RevisionNumber, revision.CreatedAt, revision.Content);
        }
    }
}
