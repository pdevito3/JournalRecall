using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;
using JournalRecall.Api.Databases;
using JournalRecall.Api.Domain.Corrections.Dtos;

namespace JournalRecall.Api.Domain.Corrections.Features;

public static class GetCorrections
{
    public sealed record Query : IRequest<IReadOnlyList<CorrectionDto>>;

    public sealed class Handler(JournalRecallDbContext db) : IRequestHandler<Query, IReadOnlyList<CorrectionDto>>
    {
        public async Task<IReadOnlyList<CorrectionDto>> Handle(Query request, CancellationToken cancellationToken)
        {
            // The global query filter scopes this to the current user (Privacy invariant).
            var corrections = await db.Corrections
                .AsNoTracking()
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync(cancellationToken);

            return corrections.Adapt<List<CorrectionDto>>();
        }
    }
}
