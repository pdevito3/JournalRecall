using System.Net;
using JournalRecall.Api.Domain.Sessions;
using JournalRecall.Api.Domain.Sessions.Dtos;
using JournalRecall.FunctionalTests.TestUtilities;

namespace JournalRecall.FunctionalTests.Sessions;

/// <summary>
/// The cleanup-result endpoint's HTTP contract (issue 0034): recording a client-run Cleanup returns 200
/// with the updated Session, an invalid payload is a validation error, and another User's Session is 404.
/// The post-processing behavior (parity, Corrections, Stale, proposals, Summaries) is covered at the
/// integration layer; this asserts the status contract.
/// </summary>
public class cleanup_result_endpoint_tests(WebTestFixture fixture) : TestBase(fixture)
{
    private static object Result(string raw) => new
    {
        cleanedMarkdown = $"Polished: {raw}",
        synopsis = "A short recap of the session.",
        topicSuggestions = Array.Empty<string>(),
        peopleProposal = Array.Empty<string>(),
        moodSuggestions = Array.Empty<string>(),
        baseRawRevisionNumber = 1,
        engine = "OnDevice",
    };

    [Fact]
    public async Task recording_a_result_returns_ok_with_the_clean_session()
    {
        var client = await RealAuth.CreateAuthenticatedClientAsync();
        var created = await client.PostAsync(ApiRoutes.Sessions.Create(), null);
        var id = (await created.ReadJsonAsync<SessionDto>())!.Id;
        await client.PutJsonAsync(ApiRoutes.Sessions.Draft(id), new { rawText = "helo wrld" });

        var response = await client.PostJsonAsync(ApiRoutes.Sessions.CleanupResult(id), Result("helo wrld"));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var dto = (await response.ReadJsonAsync<SessionDto>())!;
        dto.CleanupStatus.ShouldBe(CleanupStatus.Clean);
        dto.Synopsis.ShouldNotBeEmpty();
        dto.CleanedDraft.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task an_invalid_payload_is_a_validation_error()
    {
        var client = await RealAuth.CreateAuthenticatedClientAsync();
        var created = await client.PostAsync(ApiRoutes.Sessions.Create(), null);
        var id = (await created.ReadJsonAsync<SessionDto>())!.Id;
        await client.PutJsonAsync(ApiRoutes.Sessions.Draft(id), new { rawText = "some words" });

        // No cleanedMarkdown — the result shape is invalid, and the Session is left untouched.
        var response = await client.PostJsonAsync(ApiRoutes.Sessions.CleanupResult(id),
            new { baseRawRevisionNumber = 1, engine = "OnDevice" });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await (await client.GetAsync(ApiRoutes.Sessions.Get(id))).ReadJsonAsync<SessionDto>())!
            .CleanupStatus.ShouldBe(CleanupStatus.NotRun);
    }

    [Fact]
    public async Task a_user_cannot_record_a_result_on_another_users_session()
    {
        var alice = await RealAuth.CreateAuthenticatedClientAsync();
        var created = await alice.PostAsync(ApiRoutes.Sessions.Create(), null);
        var id = (await created.ReadJsonAsync<SessionDto>())!.Id;
        await alice.PutJsonAsync(ApiRoutes.Sessions.Draft(id), new { rawText = "alice private words" });

        var bob = await RealAuth.CreateAuthenticatedClientAsync();
        (await bob.PostJsonAsync(ApiRoutes.Sessions.CleanupResult(id), Result("alice private words")))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
