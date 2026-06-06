using MediatR;
using JournalRecall.Api.Databases;
using JournalRecall.Api.Domain.People.Dtos;
using JournalRecall.Api.Services;

namespace JournalRecall.Api.Domain.People.Features;

public static class CreatePerson
{
    public sealed record Command(PersonForWrite Person) : IRequest<PersonDto>;

    public sealed class Handler(JournalRecallDbContext db, ICurrentUserService currentUser)
        : IRequestHandler<Command, PersonDto>
    {
        public async Task<PersonDto> Handle(Command request, CancellationToken cancellationToken)
        {
            var userId = currentUser.UserId ?? throw new InvalidOperationException("No authenticated user.");

            // A blank label throws ArgumentException → mapped to a ProblemDetails 400 by the pipeline.
            var person = Person.Create(userId, request.Person.Label);
            db.People.Add(person);
            await db.SaveChangesAsync(cancellationToken);

            return new PersonDto(person.Id, person.Label);
        }
    }
}
