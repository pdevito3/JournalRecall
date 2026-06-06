using System.Net;
using JournalRecall.Api.Domain.Sessions;
using JournalRecall.Api.Domain.Sessions.Dtos;
using JournalRecall.FunctionalTests.TestUtilities;

namespace JournalRecall.FunctionalTests.Sessions;

/// <summary>
/// Functional reference test (PRD-0003, TEST-0004): the SSE <c>cleanup/stream</c> endpoint, asserting the
/// <c>text/event-stream</c> content type, that streamed progress reaches a terminal event, and that the
/// run's side effect landed (the Session is Clean). Cleanup is driven by the scripted chat client.
/// </summary>
public class session_cleanup_stream_tests(WebTestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task streaming_cleanup_emits_progress_and_ends_clean()
    {
        var client = await RealAuth.CreateAuthenticatedClientAsync();

        var created = await client.PostAsync(ApiRoutes.Sessions.Create(), null);
        var id = (await created.ReadJsonAsync<SessionDto>())!.Id;
        (await client.PutJsonAsync(ApiRoutes.Sessions.Draft(id), new { rawText = "stream me" }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var response = await client.PostAsync(ApiRoutes.Sessions.CleanupStream(id), null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("text/event-stream");

        var events = await response.ReadServerSentEventsAsync();
        events.ShouldNotBeEmpty();
        events.ShouldContain(e => e.Contains("run.started")); // live progress, not a static spinner
        events.ShouldContain(e => e.Contains("completed"));    // ends in a terminal event

        // The run's side effects landed: the Session is now Clean.
        var after = await client.GetAsync(ApiRoutes.Sessions.Get(id));
        (await after.ReadJsonAsync<SessionDto>())!.CleanupStatus.ShouldBe(CleanupStatus.Clean);
    }
}
