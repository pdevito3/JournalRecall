using System.Net;
using JournalRecall.Api.Domain.Sync.Dtos;
using JournalRecall.FunctionalTests.TestUtilities;

namespace JournalRecall.FunctionalTests.Sync;

/// <summary>
/// The delta sync change feed endpoint (issue 0033, ADR-0013) over HTTP: requires auth, a bootstrap
/// pull returns the caller's state plus a replayable cursor, and a cursor the server never issued is a
/// 400. The feed's behavioral contract (deltas, monotonicity, tenancy) lives in the integration layer.
/// </summary>
public class sync_endpoint_tests(WebTestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task an_anonymous_caller_cannot_pull_the_change_feed()
    {
        var anon = FakeAuth.CreateClient();

        var response = await anon.GetAsync(ApiRoutes.Sync.Changes);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task a_user_can_bootstrap_and_replay_the_returned_cursor()
    {
        var client = FakeAuth.CreateClient().AsUser(Guid.CreateVersion7());
        await client.PostAsync(ApiRoutes.Sessions.Create(), null);

        var bootstrap = await client.GetAsync(ApiRoutes.Sync.Changes);
        bootstrap.StatusCode.ShouldBe(HttpStatusCode.OK);
        var changes = await bootstrap.ReadJsonAsync<SyncChangesDto>();
        changes!.Sessions.Count.ShouldBe(1);
        changes.Cursor.ShouldNotBeNullOrWhiteSpace();

        // The cursor survives the query-string round trip and yields nothing new.
        var replay = await client.GetAsync(ApiRoutes.Sync.ChangesSince(changes.Cursor));
        replay.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await replay.ReadJsonAsync<SyncChangesDto>())!.Sessions.ShouldBeEmpty();
    }

    [Fact]
    public async Task a_cursor_the_server_never_issued_is_rejected()
    {
        var client = FakeAuth.CreateClient().AsUser(Guid.CreateVersion7());

        var response = await client.GetAsync(ApiRoutes.Sync.ChangesSince("definitely-not-a-cursor"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
