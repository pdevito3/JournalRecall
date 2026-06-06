using MediatR;
using Microsoft.EntityFrameworkCore;
using JournalRecall.Api.Databases;

namespace JournalRecall.Api.Domain.Sessions.Features;

public static class GetTopics
{
    /// <summary>The distinct Topic names the current User has used across their Sessions (powers badge autocomplete).</summary>
    public sealed record Query : IRequest<IReadOnlyList<string>>;

    public sealed class Handler(JournalRecallDbContext db) : IRequestHandler<Query, IReadOnlyList<string>>
    {
        public async Task<IReadOnlyList<string>> Handle(Query request, CancellationToken cancellationToken)
        {
            // The global query filter scopes Sessions to the current user (Privacy invariant); the join +
            // distinct runs off the SessionTopic(SessionId, Name) index. No Topic entity — these are the
            // owned SessionTopic strings, deduped across the User's Sessions.
            return await db.Sessions
                .SelectMany(s => s.Topics.Select(t => t.Name))
                .Distinct()
                .OrderBy(name => name)
                .ToListAsync(cancellationToken);
        }
    }
}
