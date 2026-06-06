using MediatR;
using JournalRecall.Api.Domain.People.Dtos;
using JournalRecall.Api.Domain.People.Features;

namespace JournalRecall.Api.Domain.People;

public static class PersonEndpoints
{
    public static IEndpointRouteBuilder MapPeople(this IEndpointRouteBuilder app)
    {
        // Per-User Person directory (PRD-0006). Scoped to the caller via the global query filter.
        var group = app.MapGroup("/api/people").RequireAuthorization();

        group.MapGet("", async (ISender sender) =>
            Results.Ok(await sender.Send(new GetPeople.Query())));

        group.MapPost("", async (PersonForWrite body, ISender sender) =>
        {
            var dto = await sender.Send(new CreatePerson.Command(body));
            return Results.Created($"/api/people/{dto.Id}", dto);
        });

        // Rename propagates because mentions/SessionPerson reference the PersonId, not the label.
        group.MapPatch("/{id:guid}", async (Guid id, PersonForWrite body, ISender sender) =>
        {
            var renamed = await sender.Send(new RenamePerson.Command(id, body));
            return renamed ? Results.NoContent() : Results.NotFound();
        });

        return app;
    }
}
