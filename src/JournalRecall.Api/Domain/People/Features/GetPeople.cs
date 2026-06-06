using MediatR;
using Microsoft.EntityFrameworkCore;
using JournalRecall.Api.Databases;
using JournalRecall.Api.Domain.People.Dtos;

namespace JournalRecall.Api.Domain.People.Features;

public static class GetPeople
{
    /// <summary>The current User's Person directory (powers @-mention autocomplete + resolution).</summary>
    public sealed record Query : IRequest<IReadOnlyList<PersonDto>>;

    public sealed class Handler(JournalRecallDbContext db) : IRequestHandler<Query, IReadOnlyList<PersonDto>>
    {
        public async Task<IReadOnlyList<PersonDto>> Handle(Query request, CancellationToken cancellationToken)
        {
            // The global query filter scopes this to the current user (Privacy invariant).
            return await db.People
                .AsNoTracking()
                .OrderBy(p => p.Label)
                .Select(p => new PersonDto(p.Id, p.Label))
                .ToListAsync(cancellationToken);
        }
    }
}
