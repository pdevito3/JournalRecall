using System.Net;
using JournalRecall.Api.Domain.Sessions.Dtos;
using JournalRecall.FunctionalTests.TestUtilities;

namespace JournalRecall.FunctionalTests.Sessions;

/// <summary>
/// The GET /topics endpoint's HTTP contract (PRD-0006, RICH-011): returns the caller's distinct Topic
/// names, another User's Topics are invisible, and it requires auth. Driven over the fake-auth host.
/// </summary>
public class topics_endpoint_tests(WebTestFixture fixture) : TestBase(fixture)
{
    private async Task TagSession(HttpClient client, params string[] topics)
    {
        var created = await client.PostAsync(ApiRoutes.Sessions.Create(), null);
        var id = (await created.ReadJsonAsync<SessionDto>())!.Id;
        await client.PutJsonAsync(ApiRoutes.Sessions.Metadata(id),
            new { topics, people = Array.Empty<string>(), moods = Array.Empty<string>() });
    }

    [Fact]
    public async Task returns_the_callers_distinct_topics()
    {
        var client = FakeAuth.CreateClient().AsUser(Guid.CreateVersion7());
        await TagSession(client, "work", "travel");
        await TagSession(client, "work", "home");

        var topics = await (await client.GetAsync(ApiRoutes.Topics.Root)).ReadJsonAsync<List<string>>();

        topics.ShouldBe(["home", "travel", "work"]); // distinct + sorted
    }

    [Fact]
    public async Task another_users_topics_are_invisible()
    {
        var alice = FakeAuth.CreateClient().AsUser(Guid.CreateVersion7());
        await TagSession(alice, "alice-secret");

        var bob = FakeAuth.CreateClient().AsUser(Guid.CreateVersion7());
        var topics = await (await bob.GetAsync(ApiRoutes.Topics.Root)).ReadJsonAsync<List<string>>();
        topics!.ShouldNotContain("alice-secret");
    }

    [Fact]
    public async Task the_endpoint_requires_authentication()
    {
        var anon = FakeAuth.CreateClient();

        (await anon.GetAsync(ApiRoutes.Topics.Root)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
