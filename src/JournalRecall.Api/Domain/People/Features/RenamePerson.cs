using MediatR;
using Microsoft.EntityFrameworkCore;
using JournalRecall.Api.Databases;
using JournalRecall.Api.Domain.People.Dtos;

namespace JournalRecall.Api.Domain.People.Features;

public static class RenamePerson
{
    /// <summary>Returns false when the Person doesn't exist for the current user (→ 404).</summary>
    public sealed record Command(Guid Id, PersonForWrite Person) : IRequest<bool>;

    public sealed class Handler(JournalRecallDbContext db) : IRequestHandler<Command, bool>
    {
        public async Task<bool> Handle(Command request, CancellationToken cancellationToken)
        {
            // The global query filter scopes this to the current user; another user's row is invisible.
            var person = await db.People
                .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);
            if (person is null)
                return false;

            // The rename propagates everywhere a mention/SessionPerson references this id.
            person.Rename(request.Person.Label);
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
    }
}
