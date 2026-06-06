using System.Net;
using JournalRecall.Api.Domain.Sessions;
using JournalRecall.Api.Domain.Sessions.Dtos;
using JournalRecall.FunctionalTests.TestUtilities;

namespace JournalRecall.FunctionalTests.Sessions;

/// <summary>
/// The Cleanup endpoint's HTTP contract (issue 0008): a run returns 200 with the Clean Session, and
/// running Cleanup on another User's Session is 404. The behavior is covered at the integration layer;
/// this asserts the status contract. (The SSE variant is covered by session_cleanup_stream_tests.)
/// </summary>
public class cleanup_endpoint_tests(WebTestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task cleanup_returns_ok_with_the_clean_session()
    {
        var client = await RealAuth.CreateAuthenticatedClientAsync();
        var created = await client.PostAsync(ApiRoutes.Sessions.Create(), null);
        var id = (await created.ReadJsonAsync<SessionDto>())!.Id;
        await client.PutJsonAsync(ApiRoutes.Sessions.Draft(id), new { rawText = "helo wrld" });

        var response = await client.PostAsync(ApiRoutes.Sessions.Cleanup(id), null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.ReadJsonAsync<SessionDto>())!.CleanupStatus.ShouldBe(CleanupStatus.Clean);
    }

    [Fact]
    public async Task a_user_cannot_run_cleanup_on_another_users_session()
    {
        var alice = await RealAuth.CreateAuthenticatedClientAsync();
        var created = await alice.PostAsync(ApiRoutes.Sessions.Create(), null);
        var id = (await created.ReadJsonAsync<SessionDto>())!.Id;
        await alice.PutJsonAsync(ApiRoutes.Sessions.Draft(id), new { rawText = "alice private words" });

        var bob = await RealAuth.CreateAuthenticatedClientAsync();
        (await bob.PostAsync(ApiRoutes.Sessions.Cleanup(id), null)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
