using JournalRecall.Api.Domain.Summaries;

namespace JournalRecall.UnitTests.Domain.Summaries;

/// <summary>
/// Pure unit tests for <see cref="SummaryPeriods"/> — the period math where off-by-one bugs hide:
/// anchoring, journaling-day ranges, calendar child ranges, the roll-up child tier, and the ancestor
/// chain used for staleness propagation.
/// </summary>
public class summary_periods_tests
{
    private static DateOnly D(int y, int m, int d) => new(y, m, d);

    [Fact]
    public void day_anchors_to_itself()
    {
        SummaryPeriods.Anchor(SummaryPeriod.Day, D(2026, 6, 10)).ShouldBe(D(2026, 6, 10));
    }

    [Theory]
    [InlineData(2026, 6, 10, 2026, 6, 8)]  // Wednesday → Monday
    [InlineData(2026, 6, 8, 2026, 6, 8)]   // Monday → itself
    [InlineData(2026, 6, 14, 2026, 6, 8)]  // Sunday → that week's Monday
    [InlineData(2026, 7, 1, 2026, 6, 29)]  // crosses the month boundary back to June
    public void week_anchors_to_the_iso_monday(int y, int m, int d, int ay, int am, int ad)
    {
        SummaryPeriods.Anchor(SummaryPeriod.Week, D(y, m, d)).ShouldBe(D(ay, am, ad));
    }

    [Theory]
    [InlineData(2026, 6, 10, 2026, 6, 1)]
    [InlineData(2026, 1, 31, 2026, 1, 1)]
    public void month_anchors_to_the_first_of_the_month(int y, int m, int d, int ay, int am, int ad)
    {
        SummaryPeriods.Anchor(SummaryPeriod.Month, D(y, m, d)).ShouldBe(D(ay, am, ad));
    }

    [Theory]
    [InlineData(2026, 1, 15, 2026, 1, 1)]  // Q1
    [InlineData(2026, 6, 10, 2026, 4, 1)]  // Q2
    [InlineData(2026, 9, 30, 2026, 7, 1)]  // Q3
    [InlineData(2026, 12, 31, 2026, 10, 1)] // Q4
    public void quarter_anchors_to_the_first_month_of_the_quarter(int y, int m, int d, int ay, int am, int ad)
    {
        SummaryPeriods.Anchor(SummaryPeriod.Quarter, D(y, m, d)).ShouldBe(D(ay, am, ad));
    }

    [Fact]
    public void year_anchors_to_january_first()
    {
        SummaryPeriods.Anchor(SummaryPeriod.Year, D(2026, 9, 9)).ShouldBe(D(2026, 1, 1));
    }

    [Fact]
    public void day_and_week_ranges_cover_the_right_journaling_days()
    {
        SummaryPeriods.Range(SummaryPeriod.Day, D(2026, 6, 10)).ShouldBe((D(2026, 6, 10), D(2026, 6, 10)));
        SummaryPeriods.Range(SummaryPeriod.Week, D(2026, 6, 8)).ShouldBe((D(2026, 6, 8), D(2026, 6, 14)));
    }

    [Fact]
    public void calendar_ranges_span_the_full_period_inclusive()
    {
        SummaryPeriods.CalendarRange(SummaryPeriod.Month, D(2026, 2, 1)).ShouldBe((D(2026, 2, 1), D(2026, 2, 28)));
        SummaryPeriods.CalendarRange(SummaryPeriod.Quarter, D(2026, 4, 1)).ShouldBe((D(2026, 4, 1), D(2026, 6, 30)));
        SummaryPeriods.CalendarRange(SummaryPeriod.Year, D(2026, 1, 1)).ShouldBe((D(2026, 1, 1), D(2026, 12, 31)));
    }

    [Fact]
    public void roll_up_tiers_summarize_the_level_below()
    {
        SummaryPeriods.ChildPeriod(SummaryPeriod.Month).ShouldBe(SummaryPeriod.Day);
        SummaryPeriods.ChildPeriod(SummaryPeriod.Quarter).ShouldBe(SummaryPeriod.Month);
        SummaryPeriods.ChildPeriod(SummaryPeriod.Year).ShouldBe(SummaryPeriod.Quarter);
    }

    [Fact]
    public void parents_walk_the_month_chain_with_week_and_year_off_chain()
    {
        SummaryPeriods.Parent(SummaryPeriod.Day).ShouldBe(SummaryPeriod.Month);
        SummaryPeriods.Parent(SummaryPeriod.Month).ShouldBe(SummaryPeriod.Quarter);
        SummaryPeriods.Parent(SummaryPeriod.Quarter).ShouldBe(SummaryPeriod.Year);
        SummaryPeriods.Parent(SummaryPeriod.Week).ShouldBeNull(); // parallel to the month chain
        SummaryPeriods.Parent(SummaryPeriod.Year).ShouldBeNull(); // the top
    }

    [Fact]
    public void ancestor_anchors_climb_from_a_day_to_month_quarter_and_year()
    {
        SummaryPeriods.AncestorAnchors(SummaryPeriod.Day, D(2026, 6, 10)).ShouldBe(
        [
            (SummaryPeriod.Month, D(2026, 6, 1)),
            (SummaryPeriod.Quarter, D(2026, 4, 1)),
            (SummaryPeriod.Year, D(2026, 1, 1)),
        ]);

        SummaryPeriods.AncestorAnchors(SummaryPeriod.Week, D(2026, 6, 8)).ShouldBeEmpty();
        SummaryPeriods.AncestorAnchors(SummaryPeriod.Year, D(2026, 1, 1)).ShouldBeEmpty();
    }

    [Fact]
    public void only_day_and_week_are_session_level()
    {
        SummaryPeriods.IsSessionLevel(SummaryPeriod.Day).ShouldBeTrue();
        SummaryPeriods.IsSessionLevel(SummaryPeriod.Week).ShouldBeTrue();
        SummaryPeriods.IsSessionLevel(SummaryPeriod.Month).ShouldBeFalse();
        SummaryPeriods.IsSessionLevel(SummaryPeriod.Quarter).ShouldBeFalse();
        SummaryPeriods.IsSessionLevel(SummaryPeriod.Year).ShouldBeFalse();
    }
}
