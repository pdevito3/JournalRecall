using Microsoft.EntityFrameworkCore;
using JournalRecall.Api.Domain.Sessions;
using JournalRecall.Api.Domain.Sessions.Features;
using JournalRecall.Api.Domain.Summaries;
using JournalRecall.Api.Domain.Summaries.Dtos;
using JournalRecall.Api.Domain.Summaries.Features;
using JournalRecall.Api.Domain.Summaries.Services;
using JournalRecall.SharedTestHelpers.Fakes.Sessions;

namespace JournalRecall.IntegrationTests.FeatureTests.Summaries;

/// <summary>
/// Month/Quarter/Year roll-ups + staleness propagation (issue 0014) at the integration layer: a Month
/// rolls up its Day Summaries, a Quarter its Months, a Year its Quarters; editing a Session marks its Day,
/// Week, Month, Quarter, and Year Stale; and refreshing a higher period regenerates from the current
/// lower Summaries. Driven through SummaryGenerator + GetSummary + SaveDraft, no HTTP.
/// </summary>
public class summary_rollup_tests : TestBase
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
        await SetCreatedAt(scope, session.Id, when);
        return session.Id;
    }

    private static Task<SummaryDto> Generate(TestingServiceScope scope, SummaryPeriod period, DateOnly date, string narrative)
    {
        SummaryChat.Narrative = narrative;
        return scope.GetService<SummaryGenerator>().GenerateAsync(period, date);
    }

    private static Task<SummaryDto> Get(TestingServiceScope scope, SummaryPeriod period, DateOnly date) =>
        scope.SendAsync(new GetSummary.Query(period, date));

    [Fact]
    public async Task each_tier_rolls_up_the_level_below()
    {
        using var scope = new TestingServiceScope();
        await SessionOn(scope, "Tax thoughts.", Noon(2026, 4, 5));
        await SessionOn(scope, "Spring cleaning.", Noon(2026, 4, 20));

        await Generate(scope, SummaryPeriod.Day, new DateOnly(2026, 4, 5), "APR5-DAY");
        await Generate(scope, SummaryPeriod.Day, new DateOnly(2026, 4, 20), "APR20-DAY");

        // Month rolls up its Day Summaries (not the Sessions): the prompt carries the day narratives.
        var month = await Generate(scope, SummaryPeriod.Month, new DateOnly(2026, 4, 1), "APRIL-MONTH");
        month.Period.ShouldBe(SummaryPeriod.Month);
        month.PeriodDate.ShouldBe(new DateOnly(2026, 4, 1));
        month.SourceCount.ShouldBe(2);
        SummaryChat.LastUserText.ShouldContain("APR5-DAY");
        SummaryChat.LastUserText.ShouldContain("APR20-DAY");

        var quarter = await Generate(scope, SummaryPeriod.Quarter, new DateOnly(2026, 4, 15), "Q2-QUARTER");
        quarter.PeriodDate.ShouldBe(new DateOnly(2026, 4, 1)); // Q2 anchor
        quarter.SourceCount.ShouldBe(1);
        SummaryChat.LastUserText.ShouldContain("APRIL-MONTH");

        var year = await Generate(scope, SummaryPeriod.Year, new DateOnly(2026, 9, 9), "YEAR-2026");
        year.PeriodDate.ShouldBe(new DateOnly(2026, 1, 1));
        year.SourceCount.ShouldBe(1);
        SummaryChat.LastUserText.ShouldContain("Q2-QUARTER");
    }

    [Fact]
    public async Task editing_a_session_marks_its_day_week_month_quarter_and_year_stale()
    {
        using var scope = new TestingServiceScope();
        var id = await SessionOn(scope, "Original entry.", Noon(2026, 6, 10));

        await Generate(scope, SummaryPeriod.Day, new DateOnly(2026, 6, 10), "DAY-V1");
        await Generate(scope, SummaryPeriod.Week, new DateOnly(2026, 6, 10), "WEEK-V1");
        await Generate(scope, SummaryPeriod.Month, new DateOnly(2026, 6, 1), "MONTH-V1");
        await Generate(scope, SummaryPeriod.Quarter, new DateOnly(2026, 6, 1), "QUARTER-V1");
        await Generate(scope, SummaryPeriod.Year, new DateOnly(2026, 1, 1), "YEAR-V1");

        foreach (var (period, date) in Chain())
            (await Get(scope, period, date)).Status.ShouldBe(SummaryStatus.Ready);

        // Editing the Session invalidates every period that covers its day.
        await scope.SendAsync(new SaveDraft.Command(id, "Original entry, now revised."));

        foreach (var (period, date) in Chain())
            (await Get(scope, period, date)).Status.ShouldBe(SummaryStatus.Stale);
    }

    [Fact]
    public async Task refreshing_a_higher_period_regenerates_from_the_current_lower_summaries()
    {
        using var scope = new TestingServiceScope();
        var id = await SessionOn(scope, "Day one words.", Noon(2026, 6, 10));

        await Generate(scope, SummaryPeriod.Day, new DateOnly(2026, 6, 10), "DAY-V1");
        await Generate(scope, SummaryPeriod.Month, new DateOnly(2026, 6, 1), "MONTH-V1");

        // Edit the Session, then regenerate the Day with new content.
        await scope.SendAsync(new SaveDraft.Command(id, "Day one words, revised."));
        (await Get(scope, SummaryPeriod.Month, new DateOnly(2026, 6, 1))).Status.ShouldBe(SummaryStatus.Stale);
        await Generate(scope, SummaryPeriod.Day, new DateOnly(2026, 6, 10), "DAY-V2");

        // Refreshing the Month must read the *current* Day Summary (V2), not the stale snapshot.
        var month = await Generate(scope, SummaryPeriod.Month, new DateOnly(2026, 6, 1), "MONTH-V2");
        month.Status.ShouldBe(SummaryStatus.Ready);
        month.Content.ShouldBe("MONTH-V2");
        SummaryChat.LastUserText.ShouldContain("DAY-V2");
        SummaryChat.LastUserText.ShouldNotContain("DAY-V1");
    }

    [Fact]
    public async Task a_freshly_generated_summary_is_ready_not_stale()
    {
        using var scope = new TestingServiceScope();
        await SessionOn(scope, "Fresh entry.", Noon(2026, 2, 9));

        var day = await Generate(scope, SummaryPeriod.Day, new DateOnly(2026, 2, 9), "FRESH");
        day.Status.ShouldBe(SummaryStatus.Ready); // a fresh Summary offers no regeneration; only a Stale one does
    }

    private static IEnumerable<(SummaryPeriod Period, DateOnly Date)> Chain() =>
    [
        (SummaryPeriod.Day, new DateOnly(2026, 6, 10)),
        (SummaryPeriod.Week, new DateOnly(2026, 6, 10)),
        (SummaryPeriod.Month, new DateOnly(2026, 6, 1)),
        (SummaryPeriod.Quarter, new DateOnly(2026, 6, 1)),
        (SummaryPeriod.Year, new DateOnly(2026, 1, 1)),
    ];
}
