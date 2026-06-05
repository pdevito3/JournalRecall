using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using JournalRecall.Api.Databases;
using JournalRecall.Api.Domain.Sessions;

namespace JournalRecall.Api.Tests;

/// <summary>
/// Day &amp; Week Summaries (issue 0013): on-demand generation from a period's Sessions, reading the
/// Cleaned copy when present else Raw, with Week rolling up its days across a month boundary, refresh
/// re-running, per-user privacy, and no generation without an explicit trigger.
/// </summary>
public class SummaryTests : IClassFixture<SummaryWebApplicationFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly SummaryWebApplicationFactory _factory;

    public SummaryTests(SummaryWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.Summary.Narrative = "A reflective recap of the period.";
        _factory.Cleanup.Throw = false;
        _factory.Cleanup.CleanedOverride = null;
    }

    private sealed record Credentials(string Email, string Password);
    private sealed record SessionDto(Guid Id);
    private sealed record SummaryDto(
        string Period, DateOnly PeriodDate, string Status, string Content, int SourceCount, DateTimeOffset? GeneratedAt);

    private async Task<HttpClient> SignedInClient()
    {
        var client = _factory.CreateClient();
        var creds = new Credentials($"user-{Guid.NewGuid():N}@example.com", "Passw0rd!");
        await client.PostAsJsonAsync("/api/auth/register", creds);
        await client.PostAsJsonAsync("/api/auth/login", creds);
        return client;
    }

    private static async Task<Guid> NewSession(HttpClient client, string raw)
    {
        var created = await client.PostAsync("/api/sessions", null);
        var id = (await created.Content.ReadFromJsonAsync<SessionDto>(Json))!.Id;
        (await client.PutAsJsonAsync($"/api/sessions/{id}/draft", new { rawText = raw }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        return id;
    }

    private static async Task Cleanup(HttpClient client, Guid id) =>
        (await client.PostAsync($"/api/sessions/{id}/cleanup", null)).StatusCode.ShouldBe(HttpStatusCode.OK);

    // Stamp a controlled CreatedAt so a Session lands on a chosen journaling day (UTC zone by default).
    private async Task SetCreatedAt(Guid sessionId, DateTimeOffset when)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<JournalRecallDbContext>();
        var session = await db.Sessions.IgnoreQueryFilters().FirstAsync(s => s.Id == sessionId);
        db.Entry(session).Property(nameof(Session.CreatedAt)).CurrentValue = when;
        await db.SaveChangesAsync();
    }

    private static DateTimeOffset Noon(int year, int month, int day) =>
        new(year, month, day, 12, 0, 0, TimeSpan.Zero);

    private static async Task<SummaryDto> Get(HttpClient client, string period, string date) =>
        (await client.GetFromJsonAsync<SummaryDto>($"/api/summaries/{period}/{date}", Json))!;

    private static async Task<SummaryDto> Generate(HttpClient client, string period, string date)
    {
        var res = await client.PostAsync($"/api/summaries/{period}/{date}/generate", null);
        res.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await res.Content.ReadFromJsonAsync<SummaryDto>(Json))!;
    }

    [Fact]
    public async Task Generating_a_day_with_sessions_produces_and_displays_a_summary()
    {
        var client = await SignedInClient();
        var id = await NewSession(client, "Went for a long run and felt great.");
        await SetCreatedAt(id, Noon(2026, 3, 15));
        _factory.Summary.Narrative = "They went running and felt great.";

        var summary = await Generate(client, "day", "2026-03-15");

        summary.Period.ShouldBe("Day");
        summary.PeriodDate.ShouldBe(new DateOnly(2026, 3, 15));
        summary.Status.ShouldBe("Ready");
        summary.Content.ShouldBe("They went running and felt great.");
        summary.SourceCount.ShouldBe(1);
        summary.GeneratedAt.ShouldNotBeNull();

        // It's now persisted: a fresh read returns the same Ready Summary without regenerating.
        var before = _factory.Summary.CallCount;
        (await Get(client, "day", "2026-03-15")).Content.ShouldBe("They went running and felt great.");
        _factory.Summary.CallCount.ShouldBe(before);
    }

    [Fact]
    public async Task A_week_is_generated_and_displayed_from_its_days()
    {
        var client = await SignedInClient();
        await SetCreatedAt(await NewSession(client, "Monday thoughts."), Noon(2026, 3, 16)); // Mon
        await SetCreatedAt(await NewSession(client, "Friday thoughts."), Noon(2026, 3, 20)); // Fri

        var summary = await Generate(client, "week", "2026-03-18"); // any day in the week

        summary.Period.ShouldBe("Week");
        summary.PeriodDate.ShouldBe(new DateOnly(2026, 3, 16)); // anchored on the ISO Monday
        summary.Status.ShouldBe("Ready");
        summary.SourceCount.ShouldBe(2);
    }

    [Fact]
    public async Task A_summary_reads_the_cleaned_copy_when_present_else_the_raw_text()
    {
        var client = await SignedInClient();

        // One Session is cleaned (Cleaned = "Polished: …"); the other is left Raw.
        var cleanedId = await NewSession(client, "CLEANME original");
        await Cleanup(client, cleanedId);
        await SetCreatedAt(cleanedId, Noon(2026, 4, 10));

        var rawOnlyId = await NewSession(client, "RAWONLY untouched");
        await SetCreatedAt(rawOnlyId, Noon(2026, 4, 10));

        await Generate(client, "day", "2026-04-10");

        var prompt = _factory.Summary.LastUserText;
        prompt.ShouldContain("Polished: CLEANME original"); // the cleaned copy fed the summary
        prompt.ShouldContain("RAWONLY untouched");          // the un-cleaned Session fed its Raw text
    }

    [Fact]
    public async Task A_week_spanning_two_months_rolls_up_the_correct_days()
    {
        var client = await SignedInClient();
        // ISO week Mon 2026-06-29 … Sun 2026-07-05 straddles the June/July boundary.
        await SetCreatedAt(await NewSession(client, "Last day of June."), Noon(2026, 6, 30));   // in week
        await SetCreatedAt(await NewSession(client, "Second of July."), Noon(2026, 7, 2));       // in week
        await SetCreatedAt(await NewSession(client, "Sunday before."), Noon(2026, 6, 28));       // prior week
        await SetCreatedAt(await NewSession(client, "Next Monday."), Noon(2026, 7, 6));          // next week

        var summary = await Generate(client, "week", "2026-07-02");

        summary.PeriodDate.ShouldBe(new DateOnly(2026, 6, 29)); // ISO Monday, regardless of month
        summary.SourceCount.ShouldBe(2);                       // only the two in-week days

        var prompt = _factory.Summary.LastUserText;
        prompt.ShouldContain("Last day of June.");
        prompt.ShouldContain("Second of July.");
        prompt.ShouldNotContain("Sunday before.");
        prompt.ShouldNotContain("Next Monday.");
    }

    [Fact]
    public async Task Refresh_regenerates_the_summary()
    {
        var client = await SignedInClient();
        await SetCreatedAt(await NewSession(client, "An entry."), Noon(2026, 5, 5));

        _factory.Summary.Narrative = "First take.";
        (await Generate(client, "day", "2026-05-05")).Content.ShouldBe("First take.");

        // A Refresh re-runs the model and overwrites the narrative.
        var before = _factory.Summary.CallCount;
        _factory.Summary.Narrative = "Second take.";
        var refreshed = await Generate(client, "day", "2026-05-05");

        refreshed.Content.ShouldBe("Second take.");
        refreshed.Status.ShouldBe("Ready");
        _factory.Summary.CallCount.ShouldBe(before + 1);
    }

    [Fact]
    public async Task Reading_a_period_never_generates_a_summary()
    {
        var client = await SignedInClient();
        await SetCreatedAt(await NewSession(client, "Quiet day."), Noon(2026, 2, 14));

        var before = _factory.Summary.CallCount;
        var view = await Get(client, "day", "2026-02-14");

        view.Status.ShouldBe("Missing");     // generation is user-triggered only — a read does nothing
        view.Content.ShouldBeEmpty();
        view.SourceCount.ShouldBe(1);        // …but it reports there is something to summarize
        _factory.Summary.CallCount.ShouldBe(before);
    }

    [Fact]
    public async Task Summaries_are_per_user_and_private()
    {
        var alice = await SignedInClient();
        await SetCreatedAt(await NewSession(alice, "Alice's private day."), Noon(2026, 8, 1));
        _factory.Summary.Narrative = "Alice's recap.";
        (await Generate(alice, "day", "2026-08-01")).Status.ShouldBe("Ready");

        var bob = await SignedInClient();
        var bobView = await Get(bob, "day", "2026-08-01");
        bobView.Status.ShouldBe("Missing");   // Bob can't see Alice's Summary
        bobView.SourceCount.ShouldBe(0);      // nor her Sessions

        // Bob generating the same period is a no-op (he has no Sessions) and never touches Alice's.
        (await Generate(bob, "day", "2026-08-01")).Status.ShouldBe("Missing");
        (await Get(alice, "day", "2026-08-01")).Content.ShouldBe("Alice's recap.");
    }

    [Fact]
    public async Task An_unknown_period_or_malformed_date_is_rejected()
    {
        var client = await SignedInClient();
        (await client.GetAsync("/api/summaries/decade/2026-08-01")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await client.PostAsync("/api/summaries/decade/2026-08-01/generate", null)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await client.GetAsync("/api/summaries/day/notadate")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
