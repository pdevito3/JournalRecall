using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using JournalRecall.Api.Databases;
using JournalRecall.Api.Domain.Sessions;

namespace JournalRecall.Api.Tests;

/// <summary>
/// Timeline (issue 0006): reverse-chron list grouped by journaling day in the user's timezone, with
/// QueryKit date-range filtering, reflecting current Session state only.
/// </summary>
public class SessionTimelineTests : IClassFixture<SkeletonWebApplicationFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly SkeletonWebApplicationFactory _factory;

    public SessionTimelineTests(SkeletonWebApplicationFactory factory) => _factory = factory;

    private sealed record Credentials(string Email, string Password);
    private sealed record SessionDto(Guid Id, DateTimeOffset CreatedAt, string RawDraft);
    private sealed record TimelineItem(Guid Id, DateTimeOffset CreatedAt, DateOnly JournalingDay, string Preview);

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

    // Stamp a controlled CreatedAt on a Session, bypassing the global filter (no HttpContext here).
    private async Task SetCreatedAt(Guid sessionId, DateTimeOffset when)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<JournalRecallDbContext>();
        var session = await db.Sessions.IgnoreQueryFilters().FirstAsync(s => s.Id == sessionId);
        db.Entry(session).Property(nameof(Session.CreatedAt)).CurrentValue = when;
        await db.SaveChangesAsync();
    }

    private async Task SetTimeZone(HttpClient client, string? timeZoneId) =>
        (await client.PutAsJsonAsync("/api/me/settings", new { timeZoneId })).EnsureSuccessStatusCode();

    private async Task<List<TimelineItem>> Timeline(HttpClient client, string? filter = null)
    {
        var url = filter is null ? "/api/sessions" : $"/api/sessions?filter={Uri.EscapeDataString(filter)}";
        return (await client.GetFromJsonAsync<List<TimelineItem>>(url, Json))!;
    }

    [Fact]
    public async Task Sessions_are_newest_first_and_bucketed_by_journaling_day_in_the_users_timezone()
    {
        var client = await SignedInClient();
        var early = await NewSession(client);
        var late = await NewSession(client);
        await SetCreatedAt(early, new DateTimeOffset(2026, 6, 4, 3, 50, 0, TimeSpan.Zero));  // NY: 06-03 23:50
        await SetCreatedAt(late, new DateTimeOffset(2026, 6, 4, 4, 10, 0, TimeSpan.Zero));   // NY: 06-04 00:10
        await SetTimeZone(client, "America/New_York");

        var timeline = await Timeline(client);

        timeline[0].Id.ShouldBe(late);  // newest first
        timeline[1].Id.ShouldBe(early);
        timeline.Single(i => i.Id == early).JournalingDay.ShouldBe(new DateOnly(2026, 6, 3));
        timeline.Single(i => i.Id == late).JournalingDay.ShouldBe(new DateOnly(2026, 6, 4));
    }

    [Fact]
    public async Task Changing_the_timezone_rebuckets_across_the_day_boundary()
    {
        var client = await SignedInClient();
        var id = await NewSession(client);
        await SetCreatedAt(id, new DateTimeOffset(2026, 6, 4, 3, 50, 0, TimeSpan.Zero));

        await SetTimeZone(client, "America/New_York");
        (await Timeline(client)).Single(i => i.Id == id).JournalingDay.ShouldBe(new DateOnly(2026, 6, 3));

        await SetTimeZone(client, "UTC");
        (await Timeline(client)).Single(i => i.Id == id).JournalingDay.ShouldBe(new DateOnly(2026, 6, 4));
    }

    [Fact]
    public async Task QueryKit_date_range_filter_returns_only_matching_sessions()
    {
        var client = await SignedInClient();
        var june1 = await NewSession(client);
        var june10 = await NewSession(client);
        await SetCreatedAt(june1, new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        await SetCreatedAt(june10, new DateTimeOffset(2026, 6, 10, 12, 0, 0, TimeSpan.Zero));

        var filtered = await Timeline(client, "CreatedAt >= \"2026-06-05T00:00:00Z\"");

        filtered.Select(i => i.Id).ShouldBe([june10]);
    }

    [Fact]
    public async Task Timeline_reflects_current_state_only_one_row_per_session_despite_revisions()
    {
        var client = await SignedInClient();
        var id = await NewSession(client);
        foreach (var text in new[] { "v1", "v2", "v3" })
            await client.PutAsJsonAsync($"/api/sessions/{id}/draft", new { rawText = text });

        var rows = (await Timeline(client)).Where(i => i.Id == id).ToList();

        rows.Count.ShouldBe(1); // historical Revisions never appear as separate rows
        rows[0].Preview.ShouldBe("v3");
    }
}
