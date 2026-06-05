using MediatR;
using JournalRecall.Api.Domain.Corrections.Dtos;
using JournalRecall.Api.Domain.Corrections.Features;

namespace JournalRecall.Api.Domain.Corrections;

public static class CorrectionEndpoints
{
    public static IEndpointRouteBuilder MapCorrections(this IEndpointRouteBuilder app)
    {
        // Per-user Corrections (CONTEXT.md). Scoped to the caller via the global query filter.
        var group = app.MapGroup("/api/me/corrections").RequireAuthorization();

        group.MapGet("", async (ISender sender) =>
            Results.Ok(await sender.Send(new GetCorrections.Query())));

        group.MapPost("", async (CorrectionForWrite body, ISender sender) =>
        {
            var dto = await sender.Send(new CreateCorrection.Command(body));
            return Results.Created($"/api/me/corrections/{dto.Id}", dto);
        });

        group.MapPut("/{id:guid}", async (Guid id, CorrectionForWrite body, ISender sender) =>
        {
            var updated = await sender.Send(new UpdateCorrection.Command(id, body));
            return updated ? Results.NoContent() : Results.NotFound();
        });

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var deleted = await sender.Send(new DeleteCorrection.Command(id));
            return deleted ? Results.NoContent() : Results.NotFound();
        });

        return app;
    }
}
