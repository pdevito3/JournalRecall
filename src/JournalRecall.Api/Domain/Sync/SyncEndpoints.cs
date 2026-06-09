using MediatR;
using JournalRecall.Api.Domain.Sync.Features;

namespace JournalRecall.Api.Domain.Sync;

public static class SyncEndpoints
{
    public static IEndpointRouteBuilder MapSync(this IEndpointRouteBuilder app)
    {
        // The offline-first client's pull-based delta feed (issue 0033, ADR-0013), scoped to the caller
        // like every other endpoint. No `since` = first-sync bootstrap (the user's full state).
        var group = app.MapGroup("/api/sync").RequireAuthorization();

        group.MapGet("/changes", async (string? since, ISender sender) =>
        {
            var changes = await sender.Send(new GetChanges.Query(since));
            return changes is null
                ? Results.Problem("Unrecognized cursor.", statusCode: StatusCodes.Status400BadRequest)
                : Results.Ok(changes);
        });

        return app;
    }
}
