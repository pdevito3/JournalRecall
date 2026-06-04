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

        return app;
    }
}
