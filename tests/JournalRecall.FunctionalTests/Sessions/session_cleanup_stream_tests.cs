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

    [Fact]
    public async Task streaming_cleanup_emits_a_terminal_failure_and_closes_when_the_model_throws()
    {
        var client = await RealAuth.CreateAuthenticatedClientAsync();

        var created = await client.PostAsync(ApiRoutes.Sessions.Create(), null);
        var id = (await created.ReadJsonAsync<SessionDto>())!.Id;
        (await client.PutJsonAsync(ApiRoutes.Sessions.Draft(id), new { rawText = "stream me" }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Script the model to throw: the run must fail over the stream, not hang or silently succeed.
        RealAuth.CleanupChat.Throw = true;
        try
        {
            var response = await client.PostAsync(ApiRoutes.Sessions.CleanupStream(id), null);

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            response.Content.Headers.ContentType!.MediaType.ShouldBe("text/event-stream");

            // A bounded read so an endpoint that regresses to a hang fails the test rather than blocking
            // the suite forever. A clean close completes well inside this window.
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var events = await response.ReadServerSentEventsAsync(timeout.Token);

            // The stream reached a terminal *failure* event — not a silent "completed" success frame.
            events.ShouldNotBeEmpty();
            events.ShouldContain(e => e.Contains("\"failed\"")); // terminal failure event (wire type)
            events.ShouldNotContain(e => e.Contains("\"completed\""));

            // The run's terminal state landed: the Session ends Failed.
            var after = await client.GetAsync(ApiRoutes.Sessions.Get(id));
            (await after.ReadJsonAsync<SessionDto>())!.CleanupStatus.ShouldBe(CleanupStatus.Failed);
        }
        finally
        {
            RealAuth.CleanupChat.Throw = false;
        }
    }
}
