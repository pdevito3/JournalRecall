using Microsoft.EntityFrameworkCore;
using JournalRecall.Api.Domain.Sessions;
using JournalRecall.Api.Domain.Sessions.Services;
using JournalRecall.Api.Domain.Summaries;
using JournalRecall.Api.Domain.Summaries.Dtos;
using JournalRecall.Api.Domain.Summaries.Features;
using JournalRecall.Api.Domain.Summaries.Services;
using JournalRecall.SharedTestHelpers.Fakes.Sessions;

namespace JournalRecall.IntegrationTests.FeatureTests.Summaries;

/// <summary>
/// Day &amp; Week Summaries (issue 0013) at the integration layer: on-demand generation from a period's
/// Sessions, reading the Cleaned copy when present else Raw, Week rolling up its days across a month
/// boundary, refresh re-running, per-User privacy, and no generation without an explicit trigger. Driven
/// through SummaryGenerator + GetSummary and the scripted Summary client, no HTTP.
/// </summary>
public class summary_generation_tests : TestBase
{
    private static DateTimeOffset Noon(int year, int month, int day) => new(year, month, day, 12, 0, 0, TimeSpan.Zero);

    private static Task SetCreatedAt(TestingServiceScope scope, Guid id, DateTimeOffset when) =>
        scope.ExecuteDbContextAsync(async db =>
        {
            var session = await db.Sessions.IgnoreQueryFilters().FirstAsync(s => s.Id == id);
            db.Entry(session).Property(nameof(Session.CreatedAt)).CurrentValue = when;
            await db.SaveChangesAsync();
        });

    private static async Task<Guid> SessionOn(TestingServiceScope scope, string raw, DateTimeOffset when)
    {
        var session = new FakeSessionBuilder().WithUserId(scope.CurrentUserId).WithRawText(raw).Build();
        await scope.InsertAsync(session);
        await SetCreatedAt(scope, id: session.Id, when);
        return session.Id;
    }

    private static Task<SummaryDto> Generate(TestingServiceScope scope, SummaryPeriod period, DateOnly date) =>
        scope.GetService<SummaryGenerator>().GenerateAsync(period, date);

    private static Task<SummaryDto> Get(TestingServiceScope scope, SummaryPeriod period, DateOnly date) =>
        scope.SendAsync(new GetSummary.Query(period, date));

    [Fact]
    public async Task generating_a_day_with_sessions_produces_and_persists_a_summary()
    {
        using var scope = new TestingServiceScope();
        await SessionOn(scope, "Went for a long run and felt great.", Noon(2026, 3, 15));
        SummaryChat.Narrative = "They went running and felt great.";

        var summary = await Generate(scope, SummaryPeriod.Day, new DateOnly(2026, 3, 15));

        summary.Period.ShouldBe(SummaryPeriod.Day);
        summary.PeriodDate.ShouldBe(new DateOnly(2026, 3, 15));
        summary.Status.ShouldBe(SummaryStatus.Ready);
        summary.Content.ShouldBe("They went running and felt great.");
        summary.SourceCount.ShouldBe(1);
        summary.GeneratedAt.ShouldNotBeNull();

        // It's persisted: a fresh read returns the same Ready Summary without regenerating.
        var before = SummaryChat.CallCount;
        (await Get(scope, SummaryPeriod.Day, new DateOnly(2026, 3, 15))).Content.ShouldBe("They went running and felt great.");
        SummaryChat.CallCount.ShouldBe(before);
    }

    [Fact]
    public async Task a_week_is_generated_from_its_days()
    {
        using var scope = new TestingServiceScope();
        await SessionOn(scope, "Monday thoughts.", Noon(2026, 3, 16)); // Mon
        await SessionOn(scope, "Friday thoughts.", Noon(2026, 3, 20)); // Fri

        var summary = await Generate(scope, SummaryPeriod.Week, new DateOnly(2026, 3, 18)); // any day in the week

        summary.Period.ShouldBe(SummaryPeriod.Week);
        summary.PeriodDate.ShouldBe(new DateOnly(2026, 3, 16)); // anchored on the ISO Monday
        summary.Status.ShouldBe(SummaryStatus.Ready);
        summary.SourceCount.ShouldBe(2);
    }

    [Fact]
    public async Task a_summary_reads_the_cleaned_copy_when_present_else_the_raw_text()
    {
        using var scope = new TestingServiceScope();
        var cleanedId = await SessionOn(scope, "CLEANME original", Noon(2026, 4, 10));
        await scope.GetService<SessionCleanupRunner>().RunAsync(cleanedId); // Cleaned = "Polished: …"
        await SessionOn(scope, "RAWONLY untouched", Noon(2026, 4, 10));

        await Generate(scope, SummaryPeriod.Day, new DateOnly(2026, 4, 10));

        var prompt = SummaryChat.LastUserText;
        prompt.ShouldContain("Polished: CLEANME original"); // the cleaned copy fed the summary
        prompt.ShouldContain("RAWONLY untouched");          // the un-cleaned Session fed its Raw text
    }

    [Fact]
    public async Task a_week_spanning_two_months_rolls_up_the_correct_days()
    {
        using var scope = new TestingServiceScope();
        // ISO week Mon 2026-06-29 … Sun 2026-07-05 straddles the June/July boundary.
        await SessionOn(scope, "Last day of June.", Noon(2026, 6, 30)); // in week
        await SessionOn(scope, "Second of July.", Noon(2026, 7, 2));    // in week
        await SessionOn(scope, "Sunday before.", Noon(2026, 6, 28));    // prior week
        await SessionOn(scope, "Next Monday.", Noon(2026, 7, 6));       // next week

        var summary = await Generate(scope, SummaryPeriod.Week, new DateOnly(2026, 7, 2));

        summary.PeriodDate.ShouldBe(new DateOnly(2026, 6, 29)); // ISO Monday, regardless of month
        summary.SourceCount.ShouldBe(2);                        // only the two in-week days

        var prompt = SummaryChat.LastUserText;
        prompt.ShouldContain("Last day of June.");
        prompt.ShouldContain("Second of July.");
        prompt.ShouldNotContain("Sunday before.");
        prompt.ShouldNotContain("Next Monday.");
    }

    [Fact]
    public async Task refresh_regenerates_the_summary()
    {
        using var scope = new TestingServiceScope();
        await SessionOn(scope, "An entry.", Noon(2026, 5, 5));

        SummaryChat.Narrative = "First take.";
        (await Generate(scope, SummaryPeriod.Day, new DateOnly(2026, 5, 5))).Content.ShouldBe("First take.");

        var before = SummaryChat.CallCount;
        SummaryChat.Narrative = "Second take.";
        var refreshed = await Generate(scope, SummaryPeriod.Day, new DateOnly(2026, 5, 5));

        refreshed.Content.ShouldBe("Second take.");
        refreshed.Status.ShouldBe(SummaryStatus.Ready);
        SummaryChat.CallCount.ShouldBe(before + 1);
    }

    [Fact]
    public async Task reading_a_period_never_generates_a_summary()
    {
        using var scope = new TestingServiceScope();
        await SessionOn(scope, "Quiet day.", Noon(2026, 2, 14));

        var before = SummaryChat.CallCount;
        var view = await Get(scope, SummaryPeriod.Day, new DateOnly(2026, 2, 14));

        view.Status.ShouldBe(SummaryStatus.Missing); // generation is user-triggered only
        view.Content.ShouldBeEmpty();
        view.SourceCount.ShouldBe(1);                // …but it reports there is something to summarize
        SummaryChat.CallCount.ShouldBe(before);
    }

    [Fact]
    public async Task summaries_are_per_user_and_private()
    {
        using var alice = new TestingServiceScope();
        await SessionOn(alice, "Alice's private day.", Noon(2026, 8, 1));
        SummaryChat.Narrative = "Alice's recap.";
        (await Generate(alice, SummaryPeriod.Day, new DateOnly(2026, 8, 1))).Status.ShouldBe(SummaryStatus.Ready);

        using var bob = new TestingServiceScope();
        var bobView = await Get(bob, SummaryPeriod.Day, new DateOnly(2026, 8, 1));
        bobView.Status.ShouldBe(SummaryStatus.Missing); // Bob can't see Alice's Summary
        bobView.SourceCount.ShouldBe(0);                // nor her Sessions

        // Bob generating the same period is a no-op (he has no Sessions) and never touches Alice's.
        (await Generate(bob, SummaryPeriod.Day, new DateOnly(2026, 8, 1))).Status.ShouldBe(SummaryStatus.Missing);
        (await Get(alice, SummaryPeriod.Day, new DateOnly(2026, 8, 1))).Content.ShouldBe("Alice's recap.");
    }
}
