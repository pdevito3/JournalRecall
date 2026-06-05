using MediatR;
using JournalRecall.AI.Transport;
using JournalRecall.Api.Domain.Sessions.Features;
using JournalRecall.Api.Domain.Sessions.Services;

namespace JournalRecall.Api.Domain.Sessions;

public static class SessionEndpoints
{
    public static IEndpointRouteBuilder MapSessions(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/sessions").RequireAuthorization();

        // Reverse-chronological timeline (current state only), optionally QueryKit-filtered.
        group.MapGet("", async (string? filter, ISender sender) =>
            Results.Ok(await sender.Send(new GetSessionList.Query(filter))));

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

        // Hand-edit the Cleaned copy: appends a Cleaned Revision; never touches Raw (issue 0010).
        group.MapPut("/{id:guid}/cleaned", async (Guid id, SaveCleaned.Request body, ISender sender) =>
        {
            var saved = await sender.Send(new SaveCleaned.Command(id, body.CleanedText));
            return saved ? Results.NoContent() : Results.NotFound();
        });

        // Manual AI Cleanup → Cleaned copy + Synopsis, never altering Raw (issue 0008). Runs to
        // completion and returns the updated Session (status + Cleaned/Synopsis).
        group.MapPost("/{id:guid}/cleanup", async (Guid id, SessionCleanupRunner cleanup, CancellationToken ct) =>
        {
            var dto = await cleanup.RunAsync(id, ct);
            return dto is null ? Results.NotFound() : Results.Ok(dto);
        });

        // The same Cleanup run, streamed as Server-Sent Events so the client shows live progress
        // (not a static spinner), ending in a terminal event (ADR-0005).
        group.MapPost("/{id:guid}/cleanup/stream", async (Guid id, SessionCleanupRunner cleanup, ISender sender, HttpContext http) =>
        {
            // Pre-check existence so a missing Session is a clean 404 rather than an empty stream.
            if (await sender.Send(new GetSession.Query(id)) is null)
                return Results.NotFound();

            return AgentResults.Stream(cleanup.StreamAsync(id, http.RequestAborted), StreamTransport.Sse);
        });

        // Per-Session Cleaned Revision history (drill-down; not part of any list/search index).
        group.MapGet("/{id:guid}/cleaned-revisions", async (Guid id, ISender sender) =>
        {
            var revisions = await sender.Send(new GetCleanedRevisions.Query(id));
            return revisions is null ? Results.NotFound() : Results.Ok(revisions);
        });

        group.MapGet("/{id:guid}/cleaned-revisions/{revisionNumber:int}", async (Guid id, int revisionNumber, ISender sender) =>
        {
            var revision = await sender.Send(new GetCleanedRevision.Query(id, revisionNumber));
            return revision is null ? Results.NotFound() : Results.Ok(revision);
        });

        return app;
    }
}
