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

            // Rewrite-on-rename (issue 0029): the @-mention nodes embed a snapshot label in the Raw/Cleaned
            // ProseMirror JSON, which the editor and the derived plaintext (search + AI input) render — so a
            // rename must re-label them too, not just the directory row. Load only the Sessions that actually
            // reference this Person (the SessionPerson badge join), scoped per-User by the global query filter.
            var affected = await db.Sessions
                .Where(s => s.People.Any(p => p.PersonId == request.Id))
                .ToListAsync(cancellationToken);
            foreach (var session in affected)
                session.RenamePersonMentions(request.Id, person.Label);

            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
    }
}
