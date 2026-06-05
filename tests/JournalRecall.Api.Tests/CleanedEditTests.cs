using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;

namespace JournalRecall.Api.Tests;

/// <summary>
/// Edit Cleaned + safe re-run + history (issue 0010): the user can hand-edit the Cleaned copy (saved
/// as a Cleaned Revision), a re-run overwrites but retains the prior hand-edited Revision and clears
/// the hand-edit flag, and Raw is never affected by either.
/// </summary>
public class CleanedEditTests : IClassFixture<CleanupWebApplicationFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly CleanupWebApplicationFactory _factory;

    public CleanedEditTests(CleanupWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.Chat.Throw = false;
        _factory.Chat.CleanedOverride = null;
    }

    private sealed record Credentials(string Email, string Password);
    private sealed record SessionDto(Guid Id, DateTimeOffset CreatedAt, string RawDraft, string CleanedDraft, string Synopsis, string CleanupStatus, bool CleanedHasHandEdits);
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

    private static async Task SaveRaw(HttpClient client, Guid id, string text) =>
        (await client.PutAsJsonAsync($"/api/sessions/{id}/draft", new { rawText = text }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

    private static async Task SaveCleaned(HttpClient client, Guid id, string text) =>
        (await client.PutAsJsonAsync($"/api/sessions/{id}/cleaned", new { cleanedText = text }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

    private static async Task<SessionDto> Cleanup(HttpClient client, Guid id)
    {
        var res = await client.PostAsync($"/api/sessions/{id}/cleanup", null);
        res.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await res.Content.ReadFromJsonAsync<SessionDto>(Json))!;
    }

    private static async Task<SessionDto> Get(HttpClient client, Guid id) =>
        (await client.GetFromJsonAsync<SessionDto>($"/api/sessions/{id}", Json))!;

    private static async Task<List<RevisionSummary>> CleanedRevisions(HttpClient client, Guid id) =>
        (await client.GetFromJsonAsync<List<RevisionSummary>>($"/api/sessions/{id}/cleaned-revisions", Json))!;

    [Fact]
    public async Task Editing_the_cleaned_copy_appends_a_revision_and_flags_hand_edits_without_touching_raw()
    {
        var client = await SignedInClient();
        var id = await NewSession(client);
        await SaveRaw(client, id, "raw words");
        await Cleanup(client, id); // Cleaned v1 = "Polished: raw words"

        await SaveCleaned(client, id, "my polished version");

        var after = await Get(client, id);
        after.CleanedDraft.ShouldBe("my polished version");
        after.CleanedHasHandEdits.ShouldBeTrue();
        after.RawDraft.ShouldBe("raw words"); // Raw untouched

        var cleaned = await CleanedRevisions(client, id);
        cleaned.Count.ShouldBe(2);
        var v2 = await client.GetFromJsonAsync<Revision>($"/api/sessions/{id}/cleaned-revisions/2", Json);
        v2!.Content.ShouldBe("my polished version");

        // Raw history is untouched by the Cleaned edit.
        var rawRevisions = (await client.GetFromJsonAsync<List<RevisionSummary>>($"/api/sessions/{id}/revisions", Json))!;
        rawRevisions.Count.ShouldBe(1);
    }

    [Fact]
    public async Task An_unchanged_cleaned_save_does_not_append_a_revision()
    {
        var client = await SignedInClient();
        var id = await NewSession(client);
        await SaveRaw(client, id, "raw words");
        await Cleanup(client, id); // Cleaned v1 = "Polished: raw words"

        await SaveCleaned(client, id, "Polished: raw words"); // identical → no new Revision

        (await CleanedRevisions(client, id)).Count.ShouldBe(1);
        (await Get(client, id)).CleanedHasHandEdits.ShouldBeFalse();
    }

    [Fact]
    public async Task Re_running_after_a_hand_edit_overwrites_but_retains_the_prior_revision_and_clears_the_flag()
    {
        var client = await SignedInClient();
        var id = await NewSession(client);
        await SaveRaw(client, id, "raw words");
        await Cleanup(client, id);                       // Cleaned v1
        await SaveCleaned(client, id, "my hand edit");   // Cleaned v2 (hand-edit)

        (await Get(client, id)).CleanedHasHandEdits.ShouldBeTrue();

        // Confirm-and-re-run (the confirm itself is a client concern): regenerates the copy.
        var rerun = await Cleanup(client, id);           // Cleaned v3
        rerun.CleanedDraft.ShouldBe("Polished: raw words");
        rerun.CleanedHasHandEdits.ShouldBeFalse();

        // The prior hand-edited Revision is still retrievable from history.
        (await CleanedRevisions(client, id)).Count.ShouldBe(3);
        var v2 = await client.GetFromJsonAsync<Revision>($"/api/sessions/{id}/cleaned-revisions/2", Json);
        v2!.Content.ShouldBe("my hand edit");

        // Raw is unaffected by the re-run.
        rerun.RawDraft.ShouldBe("raw words");
    }

    [Fact]
    public async Task A_fresh_cleanup_with_no_hand_edits_leaves_the_flag_false()
    {
        var client = await SignedInClient();
        var id = await NewSession(client);
        await SaveRaw(client, id, "raw words");

        // No hand-edit between runs → re-running would not prompt (flag stays false).
        (await Cleanup(client, id)).CleanedHasHandEdits.ShouldBeFalse();
        (await Cleanup(client, id)).CleanedHasHandEdits.ShouldBeFalse();
    }
}
