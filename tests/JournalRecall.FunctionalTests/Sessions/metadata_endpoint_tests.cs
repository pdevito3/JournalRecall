using System.Net;
using JournalRecall.Api.Domain.Sessions.Dtos;
using JournalRecall.FunctionalTests.TestUtilities;

namespace JournalRecall.FunctionalTests.Sessions;

/// <summary>
/// The metadata endpoint's HTTP status mapping (issue 0011, PRD-0006): a valid set (with multiple moods) is
/// 204 and another User's Session is 404. The behavior is covered at the integration layer; this asserts the
/// status contract.
/// </summary>
public class metadata_endpoint_tests(WebTestFixture fixture) : TestBase(fixture)
{
    private async Task<(HttpClient Client, Guid Id)> NewSession()
    {
        var client = await RealAuth.CreateAuthenticatedClientAsync();
        var created = await client.PostAsync(ApiRoutes.Sessions.Create(), null);
        return (client, (await created.ReadJsonAsync<SessionDto>())!.Id);
    }

    [Fact]
    public async Task a_valid_metadata_set_is_no_content()
    {
        var (client, id) = await NewSession();

        var response = await client.PutJsonAsync(ApiRoutes.Sessions.Metadata(id),
            new { topics = new[] { "work" }, moods = new[] { "Joyful", "bittersweet" }, activity = "Walking" });

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task a_full_replace_put_round_trips_activity_over_http()
    {
        var (client, id) = await NewSession();

        // A known activity round-trips canonicalized; a custom one round-trips as its raw words.
        await client.PutJsonAsync(ApiRoutes.Sessions.Metadata(id),
            new { topics = new[] { "work" }, moods = Array.Empty<string>(), activity = "walking" });
        (await (await client.GetAsync(ApiRoutes.Sessions.Get(id))).ReadJsonAsync<SessionDto>())!.Activity
            .ShouldBe("Walking");

        await client.PutJsonAsync(ApiRoutes.Sessions.Metadata(id),
            new { topics = Array.Empty<string>(), moods = Array.Empty<string>(), activity = "cooking" });
        var custom = await (await client.GetAsync(ApiRoutes.Sessions.Get(id))).ReadJsonAsync<SessionDto>();
        custom!.Activity.ShouldBe("cooking");
        custom.Topics.ShouldBeEmpty(); // the prior "work" topic was replaced wholesale (ADR-0011)
    }

    [Fact]
    public async Task another_users_session_is_not_found()
    {
        var (_, id) = await NewSession();
        var bob = await RealAuth.CreateAuthenticatedClientAsync();

        var response = await bob.PutJsonAsync(ApiRoutes.Sessions.Metadata(id),
            new { topics = new[] { "x" }, moods = Array.Empty<string>() });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
