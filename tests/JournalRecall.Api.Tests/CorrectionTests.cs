using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;

namespace JournalRecall.Api.Tests;

/// <summary>
/// Corrections (issue 0009): per-user CRUD with strict isolation, plus their effect on Cleanup —
/// hint-mode entries are injected into the prompt, hard-replace entries are substituted
/// deterministically, and neither ever alters Raw.
/// </summary>
public class CorrectionTests : IClassFixture<CleanupWebApplicationFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly CleanupWebApplicationFactory _factory;

    public CorrectionTests(CleanupWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.Chat.Throw = false;
        _factory.Chat.CleanedOverride = null;
    }

    private sealed record Credentials(string Email, string Password);
    private sealed record CorrectionDto(Guid Id, string CanonicalTerm, string[] Mishearings, bool HardReplace, DateTimeOffset CreatedAt);
    private sealed record SessionDto(Guid Id, DateTimeOffset CreatedAt, string RawDraft, string CleanedDraft, string Synopsis, string CleanupStatus);

    private async Task<HttpClient> SignedInClient()
    {
        var client = _factory.CreateClient();
        var creds = new Credentials($"user-{Guid.NewGuid():N}@example.com", "Passw0rd!");
        await client.PostAsJsonAsync("/api/auth/register", creds);
        await client.PostAsJsonAsync("/api/auth/login", creds);
        return client;
    }

    private static async Task<CorrectionDto> Create(HttpClient client, string canonical, string[] mishearings, bool hardReplace)
    {
        var res = await client.PostAsJsonAsync("/api/me/corrections", new { canonicalTerm = canonical, mishearings, hardReplace });
        res.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await res.Content.ReadFromJsonAsync<CorrectionDto>(Json))!;
    }

    private static async Task<List<CorrectionDto>> List(HttpClient client) =>
        (await client.GetFromJsonAsync<List<CorrectionDto>>("/api/me/corrections", Json))!;

    private static async Task<Guid> NewSessionWithRaw(HttpClient client, string raw)
    {
        var created = await client.PostAsync("/api/sessions", null);
        var id = (await created.Content.ReadFromJsonAsync<SessionDto>(Json))!.Id;
        (await client.PutAsJsonAsync($"/api/sessions/{id}/draft", new { rawText = raw }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        return id;
    }

    private static async Task<SessionDto> Cleanup(HttpClient client, Guid id)
    {
        var res = await client.PostAsync($"/api/sessions/{id}/cleanup", null);
        res.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await res.Content.ReadFromJsonAsync<SessionDto>(Json))!;
    }

    [Fact]
    public async Task A_user_can_create_edit_and_delete_their_own_corrections()
    {
        var client = await SignedInClient();

        var created = await Create(client, "Profisee", ["prophecy", "professionally"], false);
        created.CanonicalTerm.ShouldBe("Profisee");
        created.Mishearings.ShouldBe(["prophecy", "professionally"]);
        created.HardReplace.ShouldBeFalse();

        (await List(client)).Count.ShouldBe(1);

        // Edit: flip to hard-replace and change mishearings.
        (await client.PutAsJsonAsync($"/api/me/corrections/{created.Id}",
            new { canonicalTerm = "Profisee", mishearings = new[] { "prophecy" }, hardReplace = true }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var edited = (await List(client)).Single();
        edited.HardReplace.ShouldBeTrue();
        edited.Mishearings.ShouldBe(["prophecy"]);

        // Delete.
        (await client.DeleteAsync($"/api/me/corrections/{created.Id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await List(client)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Another_users_corrections_are_invisible_and_untouchable()
    {
        var alice = await SignedInClient();
        var correction = await Create(alice, "Profisee", ["prophecy"], false);

        var bob = await SignedInClient();
        (await List(bob)).ShouldBeEmpty();
        (await bob.PutAsJsonAsync($"/api/me/corrections/{correction.Id}",
            new { canonicalTerm = "X", mishearings = Array.Empty<string>(), hardReplace = false }))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await bob.DeleteAsync($"/api/me/corrections/{correction.Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // Alice still has it.
        (await List(alice)).Count.ShouldBe(1);
    }

    [Fact]
    public async Task Hint_mode_correction_is_injected_into_the_cleanup_prompt_and_reflected_in_cleaned()
    {
        var client = await SignedInClient();
        await Create(client, "Profisee", ["prophecy"], hardReplace: false);
        var id = await NewSessionWithRaw(client, "we evaluated prophecy for our data");

        // Simulate an obedient model honoring the hint in its Cleaned output.
        _factory.Chat.CleanedOverride = "We evaluated Profisee for our data.";
        var result = await Cleanup(client, id);

        // The Corrections list reached the model as prompt context (hint mode).
        _factory.Chat.LastSystemText.ShouldContain("Profisee");
        _factory.Chat.LastSystemText.ShouldContain("prophecy");

        result.CleanedDraft.ShouldBe("We evaluated Profisee for our data.");
        result.RawDraft.ShouldBe("we evaluated prophecy for our data"); // Raw untouched
    }

    [Fact]
    public async Task Hard_replace_correction_substitutes_every_occurrence_in_cleaned_only()
    {
        var client = await SignedInClient();
        await Create(client, "Profisee", ["prophecy"], hardReplace: true);
        var id = await NewSessionWithRaw(client, "met the prophecy team about prophecy");

        // The fake echoes Raw verbatim as the Cleaned copy; the deterministic hard-replace pass then runs.
        var result = await Cleanup(client, id);

        result.CleanedDraft.ShouldBe("Polished: met the Profisee team about Profisee");
        result.CleanedDraft.ShouldNotContain("prophecy");
        result.RawDraft.ShouldBe("met the prophecy team about prophecy"); // Raw untouched

        // Hard-replace entries are handled deterministically, not pushed into the prompt as a hint.
        _factory.Chat.LastSystemText.ShouldNotContain("commonly misheard");
    }
}
