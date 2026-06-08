using System.Net;
using JournalRecall.Api.Domain.Sessions.Dtos;
using JournalRecall.FunctionalTests.TestUtilities;

namespace JournalRecall.FunctionalTests.Sessions;

/// <summary>
/// Session core HTTP behavior (issue 0004) over real auth: autosave Draft survives a reload, the Privacy
/// invariant surfaces as 404 across users (session + revisions), and the timeline endpoint returns the
/// caller's sessions. The behavior-level coverage lives in the integration layer; these assert the HTTP
/// contract.
/// </summary>
public class session_endpoints_tests(WebTestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task autosave_draft_survives_a_reload()
    {
        var client = await RealAuth.CreateAuthenticatedClientAsync();
        var created = await client.PostAsync(ApiRoutes.Sessions.Create(), null);
        var id = (await created.ReadJsonAsync<SessionDto>())!.Id;

        var raw = "Line one.\n\n  - kept **exactly** as typed\tand spaced.";
        var save = await client.PutJsonAsync(ApiRoutes.Sessions.Draft(id), new { rawText = raw });
        save.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var reread = await client.GetAsync(ApiRoutes.Sessions.Get(id));
        (await reread.ReadJsonAsync<SessionDto>())!.RawDraft.ShouldBe(raw);
    }

    [Fact]
    public async Task a_user_cannot_read_another_users_session()
    {
        var alice = await RealAuth.CreateAuthenticatedClientAsync();
        var created = await alice.PostAsync(ApiRoutes.Sessions.Create(), null);
        var id = (await created.ReadJsonAsync<SessionDto>())!.Id;

        var bob = await RealAuth.CreateAuthenticatedClientAsync();
        (await bob.GetAsync(ApiRoutes.Sessions.Get(id)))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound); // never the content (Privacy invariant)
    }

    [Fact]
    public async Task a_user_cannot_read_another_users_revisions()
    {
        var alice = await RealAuth.CreateAuthenticatedClientAsync();
        var created = await alice.PostAsync(ApiRoutes.Sessions.Create(), null);
        var id = (await created.ReadJsonAsync<SessionDto>())!.Id;
        await alice.PutJsonAsync(ApiRoutes.Sessions.Draft(id), new { rawText = "private words" });

        var bob = await RealAuth.CreateAuthenticatedClientAsync();
        (await bob.GetAsync(ApiRoutes.Sessions.Revisions(id)))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task the_timeline_endpoint_returns_the_callers_sessions()
    {
        var client = await RealAuth.CreateAuthenticatedClientAsync();
        await client.PostAsync(ApiRoutes.Sessions.Create(), null);
        await client.PostAsync(ApiRoutes.Sessions.Create(), null);

        var list = await client.GetAsync(ApiRoutes.Sessions.Root);
        list.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await list.ReadJsonAsync<List<SessionListItemDto>>())!.Count.ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task the_timeline_endpoint_filters_by_activity()
    {
        var client = await RealAuth.CreateAuthenticatedClientAsync();
        var walk = (await (await client.PostAsync(ApiRoutes.Sessions.Create(), null)).ReadJsonAsync<SessionDto>())!.Id;
        var commute = (await (await client.PostAsync(ApiRoutes.Sessions.Create(), null)).ReadJsonAsync<SessionDto>())!.Id;
        await client.PutJsonAsync(ApiRoutes.Sessions.Metadata(walk),
            new { topics = Array.Empty<string>(), moods = Array.Empty<string>(), activity = "Walking" });
        await client.PutJsonAsync(ApiRoutes.Sessions.Metadata(commute),
            new { topics = Array.Empty<string>(), moods = Array.Empty<string>(), activity = "Commuting" });

        var filtered = await client.GetAsync($"{ApiRoutes.Sessions.Root}?activity=walking");
        var rows = (await filtered.ReadJsonAsync<List<SessionListItemDto>>())!;
        rows.Select(s => s.Id).ShouldBe([walk]);
        rows.Single().Activity.ShouldBe("Walking");
    }
}
