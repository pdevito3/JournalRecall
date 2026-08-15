using System.Diagnostics;
using System.Net;
using JournalRecall.Api.Domain.Sessions;
using JournalRecall.Api.Domain.Sessions.Dtos;
using JournalRecall.FunctionalTests.TestUtilities;

namespace JournalRecall.FunctionalTests.Observability;

/// <summary>
/// The three trace shapes the API adds on top of the standard instrumentation: a span per MediatR
/// request, a Cleanup run that is its own trace root, and a link from a recorded OnDevice result back to
/// the run the device traced.
/// </summary>
public class request_trace_tests(WebTestFixture fixture) : TestBase(fixture)
{
    private static object Result(object? trace = null) => new
    {
        cleanedMarkdown = "Polished: helo wrld",
        synopsis = "A short recap of the session.",
        topicSuggestions = Array.Empty<string>(),
        peopleProposal = Array.Empty<string>(),
        moodSuggestions = Array.Empty<string>(),
        baseRawRevisionNumber = 1,
        engine = "OnDevice",
        trace,
    };

    private async Task<Guid> CreateSessionWithDraftAsync(HttpClient client)
    {
        var created = await client.PostAsync(ApiRoutes.Sessions.Create(), null);
        var id = (await created.ReadJsonAsync<SessionDto>())!.Id;
        await client.PutJsonAsync(ApiRoutes.Sessions.Draft(id), new { rawText = "helo wrld" });
        return id;
    }

    [Fact]
    public async Task a_request_exports_a_span_named_after_its_feature_slice()
    {
        var client = await RealAuth.CreateAuthenticatedClientAsync();
        var id = await CreateSessionWithDraftAsync(client);
        RealAuth.ExportedActivities.Clear();

        (await client.GetAsync(ApiRoutes.Sessions.Get(id))).StatusCode.ShouldBe(HttpStatusCode.OK);

        var span = RealAuth.ExportedActivities
            .Where(a => a.DisplayName == "GetSession.Query")
            .ShouldHaveSingleItem();
        span.GetTagItem("journalrecall.request.name").ShouldBe("GetSession.Query");
        span.Status.ShouldBe(ActivityStatusCode.Unset);
    }

    [Fact]
    public async Task a_validation_error_is_tagged_on_the_span_but_not_marked_failed()
    {
        var client = await RealAuth.CreateAuthenticatedClientAsync();
        var id = await CreateSessionWithDraftAsync(client);
        RealAuth.ExportedActivities.Clear();

        // No cleanedMarkdown and no engine: the handler's shape validation rejects it.
        var response = await client.PostJsonAsync(ApiRoutes.Sessions.CleanupResult(id),
            new { baseRawRevisionNumber = 1 });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var span = RealAuth.ExportedActivities
            .Where(a => a.DisplayName == "RecordCleanupResult.Command")
            .ShouldHaveSingleItem();
        (span.GetTagItem("error.type") as string).ShouldEndWith("ValidationException");
        // A 4xx is the caller's fault, so the span stays Unset rather than Error.
        span.Status.ShouldBe(ActivityStatusCode.Unset);
    }

    [Fact]
    public async Task a_cleanup_run_starts_its_own_trace_and_links_back_to_the_request()
    {
        var client = await RealAuth.CreateAuthenticatedClientAsync();
        var id = await CreateSessionWithDraftAsync(client);
        RealAuth.ExportedActivities.Clear();

        var response = await client.PostAsync(ApiRoutes.Sessions.Cleanup(id), null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.ReadJsonAsync<SessionDto>())!.CleanupStatus.ShouldBe(CleanupStatus.Clean);

        var run = RealAuth.ExportedActivities
            .Where(a => a.DisplayName == "sessions.cleanup.run")
            .ShouldHaveSingleItem();
        run.Parent.ShouldBeNull();
        run.GetTagItem("session.id").ShouldBe(id);

        // The request that started the run is a link, not a parent, so the two are separate traces.
        var request = RealAuth.ExportedActivities.First(a => a.Kind == ActivityKind.Server);
        run.TraceId.ShouldNotBe(request.TraceId);
        run.Links.ShouldContain(l => l.Context.TraceId == request.TraceId);
    }

    [Fact]
    public async Task a_device_supplied_trace_context_links_the_recorded_result_to_the_device_run()
    {
        var client = await RealAuth.CreateAuthenticatedClientAsync();
        var id = await CreateSessionWithDraftAsync(client);
        var deviceTraceId = ActivityTraceId.CreateRandom();
        var traceParent = $"00-{deviceTraceId}-{ActivitySpanId.CreateRandom()}-01";
        RealAuth.ExportedActivities.Clear();

        var response = await client.PostJsonAsync(ApiRoutes.Sessions.CleanupResult(id),
            Result(new { traceParent, traceState = (string?)null }));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var span = RealAuth.ExportedActivities
            .Where(a => a.DisplayName == "RecordCleanupResult.Command")
            .ShouldHaveSingleItem();
        span.Links.ShouldContain(l => l.Context.TraceId == deviceTraceId);
        // The upload keeps its own trace; the device's run is a cause, not an ancestor.
        span.TraceId.ShouldNotBe(deviceTraceId);
    }

    [Fact]
    public async Task a_result_without_a_trace_context_records_normally()
    {
        var client = await RealAuth.CreateAuthenticatedClientAsync();
        var id = await CreateSessionWithDraftAsync(client);
        RealAuth.ExportedActivities.Clear();

        var response = await client.PostJsonAsync(ApiRoutes.Sessions.CleanupResult(id), Result());

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        RealAuth.ExportedActivities
            .Where(a => a.DisplayName == "RecordCleanupResult.Command")
            .ShouldHaveSingleItem()
            .Links.ShouldBeEmpty();
    }
}
