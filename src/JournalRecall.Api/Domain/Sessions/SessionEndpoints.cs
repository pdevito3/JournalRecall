using MediatR;
using JournalRecall.Api.Domain.Sessions.Features;

namespace JournalRecall.Api.Domain.Sessions;

public static class SessionEndpoints
{
    public static IEndpointRouteBuilder MapSessions(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/sessions").RequireAuthorization();

        group.MapPost("", async (ISender sender) =>
        {
            var dto = await sender.Send(new CreateSession.Command());
            return Results.Created($"/api/sessions/{dto.Id}", dto);
        });

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var dto = await sender.Send(new GetSession.Query(id));
            return dto is null ? Results.NotFound() : Results.Ok(dto);
        });

        group.MapPut("/{id:guid}/draft", async (Guid id, SaveDraft.Request body, ISender sender) =>
        {
            var saved = await sender.Send(new SaveDraft.Command(id, body.RawText));
            return saved ? Results.NoContent() : Results.NotFound();
        });

        // Per-Session Raw Revision history (drill-down; not part of any list/search index).
        group.MapGet("/{id:guid}/revisions", async (Guid id, ISender sender) =>
        {
            var revisions = await sender.Send(new GetRawRevisions.Query(id));
            return revisions is null ? Results.NotFound() : Results.Ok(revisions);
        });

        group.MapGet("/{id:guid}/revisions/{revisionNumber:int}", async (Guid id, int revisionNumber, ISender sender) =>
        {
            var revision = await sender.Send(new GetRawRevision.Query(id, revisionNumber));
            return revision is null ? Results.NotFound() : Results.Ok(revision);
        });

        return app;
    }
}
