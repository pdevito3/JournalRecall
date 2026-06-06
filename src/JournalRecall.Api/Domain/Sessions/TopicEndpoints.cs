using MediatR;
using JournalRecall.Api.Domain.Sessions.Features;

namespace JournalRecall.Api.Domain.Sessions;

public static class TopicEndpoints
{
    public static IEndpointRouteBuilder MapTopics(this IEndpointRouteBuilder app)
    {
        // The User's distinct Topic names for badge autocomplete (PRD-0006). There is no Topic directory
        // entity — Topics stay owned SessionTopic strings; this is a read over the User's Sessions.
        app.MapGet("/api/topics", async (ISender sender) =>
                Results.Ok(await sender.Send(new GetTopics.Query())))
            .RequireAuthorization();

        return app;
    }
}
