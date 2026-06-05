namespace JournalRecall.Api.Domain.Summaries;

/// <summary>
/// Period math for Summaries (CONTEXT.md). A Summary is keyed by an <em>anchor</em> date that
/// canonicalizes the period (a Day is its own date; a Week is its ISO-8601 Monday), and covers an
/// inclusive range of journaling days. Issue 0013 supports Day and Week; the higher tiers land in 0014.
/// </summary>
public static class SummaryPeriods
{
    /// <summary>True when this period is summarized directly from Sessions today (Day or Week).</summary>
    public static bool IsSupported(SummaryPeriod period) =>
        period is SummaryPeriod.Day or SummaryPeriod.Week;

    /// <summary>
    /// The canonical anchor date for the period containing <paramref name="date"/>: the date itself for
    /// a Day, the ISO-8601 Monday for a Week. Normalizing means any day in a week maps to one Summary.
    /// </summary>
    public static DateOnly Anchor(SummaryPeriod period, DateOnly date) => period switch
    {
        SummaryPeriod.Day => date,
        SummaryPeriod.Week => date.AddDays(-(((int)date.DayOfWeek + 6) % 7)), // back up to Monday
        _ => throw new NotSupportedException($"Period {period} is not supported yet (issue 0014)."),
    };

    /// <summary>The inclusive journaling-day range the anchored period covers.</summary>
    public static (DateOnly Start, DateOnly End) Range(SummaryPeriod period, DateOnly anchor) => period switch
    {
        SummaryPeriod.Day => (anchor, anchor),
        SummaryPeriod.Week => (anchor, anchor.AddDays(6)),
        _ => throw new NotSupportedException($"Period {period} is not supported yet (issue 0014)."),
    };
}
