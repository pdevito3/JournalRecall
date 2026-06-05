using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;

namespace JournalRecall.Api.Tests;

/// <summary>
/// Session core (issue 0004): create → autosave Raw Draft → re-read, with the Privacy invariant
/// enforced at the data layer (one user can never read another's Session).
/// </summary>
public class SessionTests : IClassFixture<SkeletonWebApplicationFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly SkeletonWebApplicationFactory _factory;

    public SessionTests(SkeletonWebApplicationFactory factory) => _factory = factory;

    private sealed record Credentials(string Email, string Password);
    private sealed record SessionDto(Guid Id, DateTimeOffset CreatedAt, string RawDraft);

    private static Credentials NewUser() => new($"user-{Guid.NewGuid():N}@example.com", "Passw0rd!23");

    private async Task<HttpClient> SignedInClient()
    {
        var client = _factory.CreateClient();
        var creds = NewUser();
        await client.PostAsJsonAsync("/api/auth/register", creds);
        await client.PostAsJsonAsync("/api/auth/login", creds);
        return client;
    }

    [Fact]
    public async Task Create_then_autosave_draft_survives_a_reload()
    {
        var client = await SignedInClient();

        var created = await client.PostAsync("/api/sessions", null);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var session = await created.Content.ReadFromJsonAsync<SessionDto>(Json);

        var raw = "Line one.\n\n  - kept **exactly** as typed\tand spaced.";
        var save = await client.PutAsJsonAsync($"/api/sessions/{session!.Id}/draft", new { rawText = raw });
        save.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Re-read (simulating a page reload) returns the persisted Raw, byte-for-byte.
        var reread = await client.GetFromJsonAsync<SessionDto>($"/api/sessions/{session.Id}", Json);
        reread!.RawDraft.ShouldBe(raw);
    }

    [Fact]
    public async Task A_user_can_create_multiple_sessions_on_the_same_day()
    {
        var client = await SignedInClient();

        var first = await (await client.PostAsync("/api/sessions", null)).Content.ReadFromJsonAsync<SessionDto>(Json);
        var second = await (await client.PostAsync("/api/sessions", null)).Content.ReadFromJsonAsync<SessionDto>(Json);

        second!.Id.ShouldNotBe(first!.Id);
    }

    [Fact]
    public async Task A_user_cannot_read_another_users_session()
    {
        var alice = await SignedInClient();
        var created = await alice.PostAsync("/api/sessions", null);
        var aliceSession = await created.Content.ReadFromJsonAsync<SessionDto>(Json);

        var bob = await SignedInClient();
        var bobView = await bob.GetAsync($"/api/sessions/{aliceSession!.Id}");

        bobView.StatusCode.ShouldBe(HttpStatusCode.NotFound); // never the content (Privacy invariant)
    }

    [Fact]
    public async Task Anonymous_callers_cannot_create_sessions()
    {
        var anon = _factory.CreateClient();
        (await anon.PostAsync("/api/sessions", null)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
