using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;

namespace JournalRecall.Api.Tests;

/// <summary>
/// Raw Revision history (issue 0005): each save point that changes the Raw text appends an immutable
/// Revision; an unchanged save mints nothing; prior Revisions stay viewable byte-for-byte.
/// </summary>
public class RawRevisionTests : IClassFixture<SkeletonWebApplicationFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly SkeletonWebApplicationFactory _factory;

    public RawRevisionTests(SkeletonWebApplicationFactory factory) => _factory = factory;

    private sealed record Credentials(string Email, string Password);
    private sealed record SessionDto(Guid Id, DateTimeOffset CreatedAt, string RawDraft);
    private sealed record RevisionSummary(int RevisionNumber, DateTimeOffset CreatedAt);
    private sealed record Revision(int RevisionNumber, DateTimeOffset CreatedAt, string Content);

    private async Task<HttpClient> SignedInClient()
    {
        var client = _factory.CreateClient();
        var creds = new Credentials($"user-{Guid.NewGuid():N}@example.com", "Passw0rd!");
        await client.PostAsJsonAsync("/api/auth/register", creds);
        await client.PostAsJsonAsync("/api/auth/login", creds);
        return client;
    }

    private static async Task<Guid> NewSession(HttpClient client)
    {
        var created = await client.PostAsync("/api/sessions", null);
        return (await created.Content.ReadFromJsonAsync<SessionDto>(Json))!.Id;
    }

    private async Task Save(HttpClient client, Guid id, string text) =>
        (await client.PutAsJsonAsync($"/api/sessions/{id}/draft", new { rawText = text }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

    private async Task<List<RevisionSummary>> Revisions(HttpClient client, Guid id) =>
        (await client.GetFromJsonAsync<List<RevisionSummary>>($"/api/sessions/{id}/revisions", Json))!;

    [Fact]
    public async Task Each_changed_save_appends_a_revision_and_prior_revisions_are_immutable()
    {
        var client = await SignedInClient();
        var id = await NewSession(client);

        await Save(client, id, "draft one");
        await Save(client, id, "draft one, expanded");

        var revisions = await Revisions(client, id);
        revisions.Count.ShouldBe(2);
        revisions.Select(r => r.RevisionNumber).ShouldBe([2, 1]); // newest first

        // The first Revision still holds the original text, unchanged by the later save.
        var first = await client.GetFromJsonAsync<Revision>($"/api/sessions/{id}/revisions/1", Json);
        first!.Content.ShouldBe("draft one");
    }

    [Fact]
    public async Task An_unchanged_save_does_not_append_a_revision()
    {
        var client = await SignedInClient();
        var id = await NewSession(client);

        await Save(client, id, "same text");
        await Save(client, id, "same text"); // no content change → no new Revision

        (await Revisions(client, id)).Count.ShouldBe(1);
    }

    [Fact]
    public async Task A_user_cannot_read_another_users_revisions()
    {
        var alice = await SignedInClient();
        var id = await NewSession(alice);
        await Save(alice, id, "private words");

        var bob = await SignedInClient();
        (await bob.GetAsync($"/api/sessions/{id}/revisions")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
