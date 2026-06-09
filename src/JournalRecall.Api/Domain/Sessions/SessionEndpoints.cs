using MediatR;
using JournalRecall.AI.Transport;
using JournalRecall.Api.Domain.Sessions.Dtos;
using JournalRecall.Api.Domain.Sessions.Features;
using JournalRecall.Api.Domain.Sessions.Services;

namespace JournalRecall.Api.Domain.Sessions;

public static class SessionEndpoints
{
    public static IEndpointRouteBuilder MapSessions(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/sessions").RequireAuthorization();

        // Reverse-chronological timeline (current state only), optionally QueryKit-filtered, plus an
        // optional any-match `mood` (a JSON collection) and a single-select `activity` (a complex-type
        // scalar) — both filtered outside QueryKit and combinable with each other (PRD-0007).
        group.MapGet("", async (string? filter, string? mood, string? activity, ISender sender) =>
            Results.Ok(await sender.Send(new GetSessionList.Query(filter, mood, activity))));

        // Optional body carries a client-minted Session id (offline-first create, ADR-0013/issue 0031)
        // and/or a captured lat/long (geo opt-in, issue 0015); a plain POST with no body creates a
        // server-minted, location-less Session as before. Replaying a create with an id the caller
        // already owns idempotently returns the existing Session; an id owned by another user is a
        // plain 404 — the same shape as any not-yours resource, so existence never leaks.
        group.MapPost("", async (CreateSession.Request? body, ISender sender) =>
        {
            var dto = await sender.Send(new CreateSession.Command(body?.Latitude, body?.Longitude, body?.Id));
            return dto is null ? Results.NotFound() : Results.Created($"/api/sessions/{dto.Id}", dto);
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

        // Manual metadata (Topics, People, Mood) — all provenance UserSet (issue 0011).
        group.MapPut("/{id:guid}/metadata", async (Guid id, MetadataForWrite body, ISender sender) =>
        {
            var result = await sender.Send(new UpdateMetadata.Command(id, body));
            return result == UpdateMetadata.Result.Ok ? Results.NoContent() : Results.NotFound();
        });

        // Accept/reject an AI metadata Suggestion (issue 0012). Accept promotes it (AiSuggested).
        group.MapPost("/{id:guid}/suggestions/accept", async (Guid id, SuggestionRef body, ISender sender) =>
        {
            var handled = await sender.Send(new RespondToSuggestion.Command(id, body.Kind, body.Value, Accept: true));
            return handled ? Results.NoContent() : Results.NotFound();
        });

        group.MapPost("/{id:guid}/suggestions/reject", async (Guid id, SuggestionRef body, ISender sender) =>
        {
            var handled = await sender.Send(new RespondToSuggestion.Command(id, body.Kind, body.Value, Accept: false));
            return handled ? Results.NoContent() : Results.NotFound();
        });

        // Approve/reject an AI People-tag proposal (RICH-009). Approve inserts mentions deterministically
        // into the Cleaned copy (reassign / create-new / exact-match), reject drops the proposal.
        group.MapPost("/{id:guid}/people-proposals/respond", async (Guid id, PersonTagDecision body, ISender sender) =>
        {
            var handled = await sender.Send(
                new RespondToPersonProposal.Command(id, body.Label, body.Approve, body.BindToPersonId, body.CreateNew));
            return handled ? Results.NoContent() : Results.NotFound();
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
