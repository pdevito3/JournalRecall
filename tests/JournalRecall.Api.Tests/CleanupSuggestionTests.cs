using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using JournalRecall.Api.Databases;
using JournalRecall.Api.Domain.Sessions.Metadata;

namespace JournalRecall.Api.Tests;

/// <summary>
/// AI metadata Suggestions (issue 0012): a Cleanup run proposes Topic/Person/Mood Suggestions distinct
/// from accepted metadata; accept promotes them (provenance AiSuggested) and reject discards them;
/// UserSet metadata is never overwritten or duplicated; suggestions are per-user.
/// </summary>
public class CleanupSuggestionTests : IClassFixture<CleanupWebApplicationFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly CleanupWebApplicationFactory _factory;

    public CleanupSuggestionTests(CleanupWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.Chat.Throw = false;
        _factory.Chat.CleanedOverride = null;
        _factory.Chat.SuggestTopics = [];
        _factory.Chat.SuggestPeople = [];
        _factory.Chat.SuggestMood = null;
    }

    private sealed record Credentials(string Email, string Password);
    private sealed record MoodDto(string Key, string? CustomValue);
    private sealed record SuggestionDto(string Kind, string Value, string? MoodCustomValue);
    private sealed record SessionDto(Guid Id, string RawDraft, string[] Topics, string[] People, MoodDto? Mood, SuggestionDto[] Suggestions);

    private async Task<HttpClient> SignedInClient()
    {
        var client = _factory.CreateClient();
        var creds = new Credentials($"user-{Guid.NewGuid():N}@example.com", "Passw0rd!");
        await client.PostAsJsonAsync("/api/auth/register", creds);
        await client.PostAsJsonAsync("/api/auth/login", creds);
        return client;
    }

    private static async Task<Guid> NewSessionWithRaw(HttpClient client, string raw)
    {
        var created = await client.PostAsync("/api/sessions", null);
        var id = (await created.Content.ReadFromJsonAsync<SessionDto>(Json))!.Id;
        (await client.PutAsJsonAsync($"/api/sessions/{id}/draft", new { rawText = raw })).EnsureSuccessStatusCode();
        return id;
    }

    private static async Task Cleanup(HttpClient client, Guid id) =>
        (await client.PostAsync($"/api/sessions/{id}/cleanup", null)).StatusCode.ShouldBe(HttpStatusCode.OK);

    private static async Task SetMetadata(HttpClient client, Guid id, object body) =>
        (await client.PutAsJsonAsync($"/api/sessions/{id}/metadata", body)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

    private static async Task<SessionDto> Get(HttpClient client, Guid id) =>
        (await client.GetFromJsonAsync<SessionDto>($"/api/sessions/{id}", Json))!;

    [Fact]
    public async Task A_cleanup_run_yields_suggestions_distinct_from_accepted_metadata()
    {
        var client = await SignedInClient();
        _factory.Chat.SuggestTopics = ["work"];
        _factory.Chat.SuggestPeople = ["Sam"];
        _factory.Chat.SuggestMood = "Joyful";
        var id = await NewSessionWithRaw(client, "had a great day at work with Sam");

        await Cleanup(client, id);

        var session = await Get(client, id);
        session.Topics.ShouldBeEmpty();   // nothing accepted yet
        session.People.ShouldBeEmpty();
        session.Mood.ShouldBeNull();
        session.Suggestions.Select(s => (s.Kind, s.Value)).ShouldBe(
            [("Topic", "work"), ("Person", "Sam"), ("Mood", "Joyful")], ignoreOrder: true);
    }

    [Fact]
    public async Task Accepting_a_suggestion_promotes_it_with_provenance_aisuggested_and_removes_it()
    {
        var client = await SignedInClient();
        _factory.Chat.SuggestTopics = ["work"];
        var id = await NewSessionWithRaw(client, "work stuff");
        await Cleanup(client, id);

        (await client.PostAsJsonAsync($"/api/sessions/{id}/suggestions/accept", new { kind = "Topic", value = "work" }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var session = await Get(client, id);
        session.Topics.ShouldBe(["work"]);
        session.Suggestions.ShouldBeEmpty();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<JournalRecallDbContext>();
        var entity = await db.Sessions.IgnoreQueryFilters().Include(s => s.Topics).FirstAsync(s => s.Id == id);
        entity.Topics.Single(t => t.Name == "work").Provenance.ShouldBe(MetadataProvenance.AiSuggested);
    }

    [Fact]
    public async Task Rejecting_a_suggestion_removes_it_without_promoting()
    {
        var client = await SignedInClient();
        _factory.Chat.SuggestPeople = ["Sam"];
        var id = await NewSessionWithRaw(client, "saw Sam");
        await Cleanup(client, id);

        (await client.PostAsJsonAsync($"/api/sessions/{id}/suggestions/reject", new { kind = "Person", value = "Sam" }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var session = await Get(client, id);
        session.People.ShouldBeEmpty();
        session.Suggestions.ShouldBeEmpty();
    }

    [Fact]
    public async Task Ai_never_overwrites_or_duplicates_userset_metadata()
    {
        var client = await SignedInClient();
        var id = await NewSessionWithRaw(client, "work day, felt sad, saw Sam");
        // The user has already set a Topic and a Mood themselves.
        await SetMetadata(client, id, new { topics = new[] { "work" }, people = Array.Empty<string>(), mood = new { key = "Sad", customValue = (string?)null } });

        // AI suggests the same topic + a new one, and a different mood.
        _factory.Chat.SuggestTopics = ["work", "travel"];
        _factory.Chat.SuggestMood = "Joyful";
        await Cleanup(client, id);

        var session = await Get(client, id);
        // The already-set "work" is not re-suggested; only the new "travel" is.
        session.Suggestions.Where(s => s.Kind == "Topic").Select(s => s.Value).ShouldBe(["travel"]);
        // The user's mood is untouched and no mood Suggestion is offered.
        session.Mood!.Key.ShouldBe("Sad");
        session.Suggestions.ShouldNotContain(s => s.Kind == "Mood");
        // The user's Topic remains a single entry (no duplicate).
        session.Topics.ShouldBe(["work"]);
    }

    [Fact]
    public async Task Suggestions_are_scoped_to_the_owning_user()
    {
        var alice = await SignedInClient();
        _factory.Chat.SuggestTopics = ["work"];
        var id = await NewSessionWithRaw(alice, "work");
        await Cleanup(alice, id);

        var bob = await SignedInClient();
        (await bob.PostAsJsonAsync($"/api/sessions/{id}/suggestions/accept", new { kind = "Topic", value = "work" }))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await bob.PostAsJsonAsync($"/api/sessions/{id}/suggestions/reject", new { kind = "Topic", value = "work" }))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // Alice's suggestion is still pending.
        (await Get(alice, id)).Suggestions.ShouldContain(s => s.Kind == "Topic" && s.Value == "work");
    }
}
