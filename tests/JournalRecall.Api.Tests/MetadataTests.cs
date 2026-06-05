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
/// Manual metadata + filtering (issue 0011): Topics/People/Mood are set per-Session with provenance
/// UserSet, the timeline filters by each via QueryKit, Custom mood round-trips its free text, and one
/// user's metadata is invisible to another.
/// </summary>
public class MetadataTests : IClassFixture<SkeletonWebApplicationFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly SkeletonWebApplicationFactory _factory;

    public MetadataTests(SkeletonWebApplicationFactory factory) => _factory = factory;

    private sealed record Credentials(string Email, string Password);
    private sealed record MoodDto(string Key, string? CustomValue);
    private sealed record SessionDto(Guid Id, DateTimeOffset CreatedAt, string RawDraft, string[] Topics, string[] People, MoodDto? Mood);
    private sealed record TimelineItem(Guid Id, DateTimeOffset CreatedAt, DateOnly JournalingDay, string Preview, string[] Topics, string[] People, MoodDto? Mood);

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

    private static async Task SetMetadata(HttpClient client, Guid id, object body) =>
        (await client.PutAsJsonAsync($"/api/sessions/{id}/metadata", body)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

    private static async Task<SessionDto> Get(HttpClient client, Guid id) =>
        (await client.GetFromJsonAsync<SessionDto>($"/api/sessions/{id}", Json))!;

    private static async Task<List<TimelineItem>> Timeline(HttpClient client, string? filter = null)
    {
        var url = filter is null ? "/api/sessions" : $"/api/sessions?filter={Uri.EscapeDataString(filter)}";
        return (await client.GetFromJsonAsync<List<TimelineItem>>(url, Json))!;
    }

    [Fact]
    public async Task A_user_can_set_topics_people_and_mood_including_custom()
    {
        var client = await SignedInClient();
        var id = await NewSession(client);

        await SetMetadata(client, id, new
        {
            topics = new[] { "work", "parenthood" },
            people = new[] { "Sam", "Alex" },
            mood = new { key = "Custom", customValue = "bittersweet" },
        });

        var session = await Get(client, id);
        session.Topics.ShouldBe(["work", "parenthood"]);
        session.People.ShouldBe(["Sam", "Alex"]);
        session.Mood!.Key.ShouldBe("Custom");
        session.Mood.CustomValue.ShouldBe("bittersweet"); // Custom round-trips its free text

        // Removing a topic / clearing the mood is just another set.
        await SetMetadata(client, id, new { topics = new[] { "work" }, people = Array.Empty<string>(), mood = (object?)null });
        var updated = await Get(client, id);
        updated.Topics.ShouldBe(["work"]);
        updated.People.ShouldBeEmpty();
        updated.Mood.ShouldBeNull();
    }

    [Fact]
    public async Task Manually_set_metadata_is_stored_with_provenance_userset()
    {
        var client = await SignedInClient();
        var id = await NewSession(client);
        await SetMetadata(client, id, new { topics = new[] { "work" }, people = new[] { "Sam" }, mood = (object?)null });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<JournalRecallDbContext>();
        var session = await db.Sessions.IgnoreQueryFilters().Include(s => s.Topics).Include(s => s.People)
            .FirstAsync(s => s.Id == id);

        session.Topics.ShouldAllBe(t => t.Provenance == MetadataProvenance.UserSet);
        session.People.ShouldAllBe(p => p.Provenance == MetadataProvenance.UserSet);
    }

    [Fact]
    public async Task The_timeline_can_be_filtered_by_topic_person_and_mood()
    {
        var client = await SignedInClient();
        var work = await NewSession(client);
        var travel = await NewSession(client);
        await SetMetadata(client, work, new { topics = new[] { "work" }, people = new[] { "Sam" }, mood = new { key = "Joyful", customValue = (string?)null } });
        await SetMetadata(client, travel, new { topics = new[] { "travel" }, people = new[] { "Alex" }, mood = new { key = "Tired", customValue = (string?)null } });

        (await Timeline(client, "topics == \"work\"")).Select(s => s.Id).ShouldBe([work]);
        (await Timeline(client, "people == \"Alex\"")).Select(s => s.Id).ShouldBe([travel]);
        (await Timeline(client, "mood == \"Joyful\"")).Select(s => s.Id).ShouldBe([work]);

        // No filter returns both (newest first).
        (await Timeline(client)).Count.ShouldBe(2);
    }

    [Fact]
    public async Task Another_users_metadata_is_not_visible_or_filterable()
    {
        var alice = await SignedInClient();
        var aliceSession = await NewSession(alice);
        await SetMetadata(alice, aliceSession, new { topics = new[] { "secret-project" }, people = Array.Empty<string>(), mood = (object?)null });

        var bob = await SignedInClient();
        var bobSession = await NewSession(bob);
        await SetMetadata(bob, bobSession, new { topics = new[] { "secret-project" }, people = Array.Empty<string>(), mood = (object?)null });

        // Bob filtering by the shared topic name sees only his own Session.
        (await Timeline(bob, "topics == \"secret-project\"")).Select(s => s.Id).ShouldBe([bobSession]);

        // Bob cannot set metadata on Alice's Session.
        (await bob.PutAsJsonAsync($"/api/sessions/{aliceSession}/metadata",
            new { topics = new[] { "x" }, people = Array.Empty<string>(), mood = (object?)null }))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task An_unknown_mood_is_rejected()
    {
        var client = await SignedInClient();
        var id = await NewSession(client);

        (await client.PutAsJsonAsync($"/api/sessions/{id}/metadata",
            new { topics = Array.Empty<string>(), people = Array.Empty<string>(), mood = new { key = "Ecstatic", customValue = (string?)null } }))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
