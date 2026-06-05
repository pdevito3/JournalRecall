using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;

namespace JournalRecall.Api.Tests;

/// <summary>
/// AI Cleanup → Cleaned + Synopsis (issue 0008): a manual run produces a Cleaned copy and a Synopsis
/// without ever altering Raw, drives the status through NotRun → Clean → Stale, and records a model
/// failure as Failed while leaving Raw and any prior Cleaned copy intact.
/// </summary>
public class CleanupTests : IClassFixture<CleanupWebApplicationFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly CleanupWebApplicationFactory _factory;

    public CleanupTests(CleanupWebApplicationFactory factory)
    {
        _factory = factory;
        // Reset the scripted model between tests (the factory — and its fake — is shared per class).
        _factory.Chat.Throw = false;
        _factory.Chat.CleanedOverride = null;
        _factory.Chat.Synopsis = "A short recap of the session.";
    }

    private sealed record Credentials(string Email, string Password);
    private sealed record SessionDto(Guid Id, DateTimeOffset CreatedAt, string RawDraft, string CleanedDraft, string Synopsis, string CleanupStatus);
    private sealed record Revision(int RevisionNumber, DateTimeOffset CreatedAt, string Content);
    private sealed record RevisionSummary(int RevisionNumber, DateTimeOffset CreatedAt);

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

    private static async Task Save(HttpClient client, Guid id, string text) =>
        (await client.PutAsJsonAsync($"/api/sessions/{id}/draft", new { rawText = text }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

    private static async Task<SessionDto> Cleanup(HttpClient client, Guid id)
    {
        var res = await client.PostAsync($"/api/sessions/{id}/cleanup", null);
        res.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await res.Content.ReadFromJsonAsync<SessionDto>(Json))!;
    }

    private static async Task<SessionDto> Get(HttpClient client, Guid id) =>
        (await client.GetFromJsonAsync<SessionDto>($"/api/sessions/{id}", Json))!;

    [Fact]
    public async Task Cleanup_produces_cleaned_and_synopsis_appends_a_revision_and_leaves_raw_unchanged()
    {
        var client = await SignedInClient();
        var id = await NewSession(client);
        await Save(client, id, "helo wrld this is my entry");

        (await Get(client, id)).CleanupStatus.ShouldBe("NotRun");

        var result = await Cleanup(client, id);

        result.CleanupStatus.ShouldBe("Clean");
        result.CleanedDraft.ShouldBe("Polished: helo wrld this is my entry");
        result.Synopsis.ShouldNotBeEmpty();

        // Raw is byte-for-byte unchanged: the draft and its single Revision still hold the original.
        var after = await Get(client, id);
        after.RawDraft.ShouldBe("helo wrld this is my entry");
        var rawRevisions = (await client.GetFromJsonAsync<List<RevisionSummary>>($"/api/sessions/{id}/revisions", Json))!;
        rawRevisions.Count.ShouldBe(1);
        var rawV1 = await client.GetFromJsonAsync<Revision>($"/api/sessions/{id}/revisions/1", Json);
        rawV1!.Content.ShouldBe("helo wrld this is my entry");

        // A Cleaned Revision was appended.
        var cleanedRevisions = (await client.GetFromJsonAsync<List<RevisionSummary>>($"/api/sessions/{id}/cleaned-revisions", Json))!;
        cleanedRevisions.Count.ShouldBe(1);
        var cleanedV1 = await client.GetFromJsonAsync<Revision>($"/api/sessions/{id}/cleaned-revisions/1", Json);
        cleanedV1!.Content.ShouldBe("Polished: helo wrld this is my entry");
    }

    [Fact]
    public async Task Editing_raw_after_cleanup_flips_status_to_stale()
    {
        var client = await SignedInClient();
        var id = await NewSession(client);
        await Save(client, id, "first draft");

        (await Cleanup(client, id)).CleanupStatus.ShouldBe("Clean");

        // A Raw edit after a successful Cleanup makes the Cleaned copy Stale (CONTEXT.md).
        await Save(client, id, "first draft, now revised");

        (await Get(client, id)).CleanupStatus.ShouldBe("Stale");
    }

    [Fact]
    public async Task Model_failure_yields_failed_without_corrupting_raw_or_prior_cleaned()
    {
        var client = await SignedInClient();
        var id = await NewSession(client);
        await Save(client, id, "original words");

        // First run succeeds, giving us a prior Cleaned copy to protect.
        var clean = await Cleanup(client, id);
        clean.CleanedDraft.ShouldBe("Polished: original words");

        // Edit Raw, then make the model fail on the re-run.
        await Save(client, id, "edited words");
        _factory.Chat.Throw = true;

        var failed = await Cleanup(client, id);

        failed.CleanupStatus.ShouldBe("Failed");
        failed.RawDraft.ShouldBe("edited words");            // Raw untouched
        failed.CleanedDraft.ShouldBe("Polished: original words"); // prior Cleaned copy intact

        // The Raw history is intact (two Revisions); the failed run appended no Cleaned Revision.
        var rawRevisions = (await client.GetFromJsonAsync<List<RevisionSummary>>($"/api/sessions/{id}/revisions", Json))!;
        rawRevisions.Count.ShouldBe(2);
        var cleanedRevisions = (await client.GetFromJsonAsync<List<RevisionSummary>>($"/api/sessions/{id}/cleaned-revisions", Json))!;
        cleanedRevisions.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Streaming_cleanup_emits_progress_and_ends_in_a_terminal_event()
    {
        var client = await SignedInClient();
        var id = await NewSession(client);
        await Save(client, id, "stream me");

        var res = await client.PostAsync($"/api/sessions/{id}/cleanup/stream", null);
        res.StatusCode.ShouldBe(HttpStatusCode.OK);
        res.Content.Headers.ContentType!.MediaType.ShouldBe("text/event-stream");

        var body = await res.Content.ReadAsStringAsync();
        body.ShouldContain("run.started");   // progress, not a static spinner
        body.ShouldContain("completed");      // ends in a terminal event

        // The run's side effects landed: the Session is now Clean.
        (await Get(client, id)).CleanupStatus.ShouldBe("Clean");
    }

    [Fact]
    public async Task A_user_cannot_run_cleanup_on_another_users_session()
    {
        var alice = await SignedInClient();
        var id = await NewSession(alice);
        await Save(alice, id, "alice private words");

        var bob = await SignedInClient();
        (await bob.PostAsync($"/api/sessions/{id}/cleanup", null)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
