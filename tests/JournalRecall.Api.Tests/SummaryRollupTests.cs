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
/// Month/Quarter/Year roll-ups + staleness propagation (issue 0014): a Month is generated from its Day
/// Summaries, a Quarter from its Months, a Year from its Quarters; editing a Session marks its Day, Week,
/// Month, Quarter, and Year Stale; and refreshing a higher period regenerates from the current lower
/// Summaries.
/// </summary>
public class SummaryRollupTests : IClassFixture<SummaryWebApplicationFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly SummaryWebApplicationFactory _factory;

    public SummaryRollupTests(SummaryWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.Summary.Narrative = "A reflective recap of the period.";
        _factory.Cleanup.Throw = false;
    }

    private sealed record Credentials(string Email, string Password);
    private sealed record SessionDto(Guid Id);
    private sealed record SummaryDto(
        string Period, DateOnly PeriodDate, string Status, string Content, int SourceCount, DateTimeOffset? GeneratedAt);

    private async Task<HttpClient> SignedInClient()
    {
        var client = _factory.CreateClient();
        var creds = new Credentials($"user-{Guid.NewGuid():N}@example.com", "Passw0rd!23");
        await client.PostAsJsonAsync("/api/auth/register", creds);
        await client.PostAsJsonAsync("/api/auth/login", creds);
        return client;
    }

    private static async Task<Guid> NewSession(HttpClient client, string raw)
    {
        var created = await client.PostAsync("/api/sessions", null);
        var id = (await created.Content.ReadFromJsonAsync<SessionDto>(Json))!.Id;
        await Save(client, id, raw);
        return id;
    }

    private static async Task Save(HttpClient client, Guid id, string raw) =>
        (await client.PutAsJsonAsync($"/api/sessions/{id}/draft", new { rawText = raw }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

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

    // Generate with a controlled narrative so a later roll-up's prompt can be checked for it.
    private async Task<SummaryDto> Generate(HttpClient client, string period, string date, string narrative)
    {
        _factory.Summary.Narrative = narrative;
        var res = await client.PostAsync($"/api/summaries/{period}/{date}/generate", null);
        res.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await res.Content.ReadFromJsonAsync<SummaryDto>(Json))!;
    }

    [Fact]
    public async Task Each_tier_rolls_up_the_level_below_month_from_days_quarter_from_months_year_from_quarters()
    {
        var client = await SignedInClient();
        await SetCreatedAt(await NewSession(client, "Tax thoughts."), Noon(2026, 4, 5));
        await SetCreatedAt(await NewSession(client, "Spring cleaning."), Noon(2026, 4, 20));

        await Generate(client, "day", "2026-04-05", "APR5-DAY");
        await Generate(client, "day", "2026-04-20", "APR20-DAY");

        // Month rolls up its Day Summaries (not the Sessions): the prompt carries the day narratives.
        var month = await Generate(client, "month", "2026-04-01", "APRIL-MONTH");
        month.Period.ShouldBe("Month");
        month.PeriodDate.ShouldBe(new DateOnly(2026, 4, 1));
        month.SourceCount.ShouldBe(2);
        _factory.Summary.LastUserText.ShouldContain("APR5-DAY");
        _factory.Summary.LastUserText.ShouldContain("APR20-DAY");

        // Quarter rolls up its Months.
        var quarter = await Generate(client, "quarter", "2026-04-15", "Q2-QUARTER");
        quarter.PeriodDate.ShouldBe(new DateOnly(2026, 4, 1)); // Q2 anchor
        quarter.SourceCount.ShouldBe(1);
        _factory.Summary.LastUserText.ShouldContain("APRIL-MONTH");

        // Year rolls up its Quarters.
        var year = await Generate(client, "year", "2026-09-09", "YEAR-2026");
        year.PeriodDate.ShouldBe(new DateOnly(2026, 1, 1));
        year.SourceCount.ShouldBe(1);
        _factory.Summary.LastUserText.ShouldContain("Q2-QUARTER");
    }

    [Fact]
    public async Task Editing_a_session_marks_its_day_week_month_quarter_and_year_stale()
    {
        var client = await SignedInClient();
        var id = await NewSession(client, "Original entry.");
        await SetCreatedAt(id, Noon(2026, 6, 10));

        // Build the whole chain bottom-up so every tier is Ready.
        await Generate(client, "day", "2026-06-10", "DAY-V1");
        await Generate(client, "week", "2026-06-10", "WEEK-V1");
        await Generate(client, "month", "2026-06-01", "MONTH-V1");
        await Generate(client, "quarter", "2026-06-01", "QUARTER-V1");
        await Generate(client, "year", "2026-01-01", "YEAR-V1");

        foreach (var (period, date) in Chain())
            (await Get(client, period, date)).Status.ShouldBe("Ready");

        // Editing the Session invalidates every period that covers its day.
        await Save(client, id, "Original entry, now revised.");

        foreach (var (period, date) in Chain())
            (await Get(client, period, date)).Status.ShouldBe("Stale");
    }

    [Fact]
    public async Task Refreshing_a_higher_period_regenerates_from_the_current_lower_summaries()
    {
        var client = await SignedInClient();
        var id = await NewSession(client, "Day one words.");
        await SetCreatedAt(id, Noon(2026, 6, 10));

        await Generate(client, "day", "2026-06-10", "DAY-V1");
        await Generate(client, "month", "2026-06-01", "MONTH-V1");

        // Edit the Session, then regenerate the Day with new content.
        await Save(client, id, "Day one words, revised.");
        (await Get(client, "month", "2026-06-01")).Status.ShouldBe("Stale");
        await Generate(client, "day", "2026-06-10", "DAY-V2");

        // Refreshing the Month must read the *current* Day Summary (V2), not the stale snapshot.
        var month = await Generate(client, "month", "2026-06-01", "MONTH-V2");
        month.Status.ShouldBe("Ready");
        month.Content.ShouldBe("MONTH-V2");
        _factory.Summary.LastUserText.ShouldContain("DAY-V2");
        _factory.Summary.LastUserText.ShouldNotContain("DAY-V1");
    }

    [Fact]
    public async Task A_freshly_generated_summary_is_ready_not_stale()
    {
        var client = await SignedInClient();
        var id = await NewSession(client, "Fresh entry.");
        await SetCreatedAt(id, Noon(2026, 2, 9));

        var day = await Generate(client, "day", "2026-02-09", "FRESH");
        day.Status.ShouldBe("Ready"); // a fresh Summary offers no regeneration; only a Stale one does
    }

    private static IEnumerable<(string Period, string Date)> Chain() =>
    [
        ("day", "2026-06-10"),
        ("week", "2026-06-10"),
        ("month", "2026-06-01"),
        ("quarter", "2026-06-01"),
        ("year", "2026-01-01"),
    ];
}
