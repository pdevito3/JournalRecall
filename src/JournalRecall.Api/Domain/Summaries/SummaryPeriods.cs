namespace JournalRecall.Api.Domain.Summaries;

/// <summary>
/// Period math for Summaries (CONTEXT.md). A Summary is keyed by an <em>anchor</em> date that
/// canonicalizes the period (a Day is its own date; a Week is its ISO-8601 Monday; Month/Quarter/Year
/// are their first calendar day). Day and Week summarize Sessions directly; Month rolls up its Days,
/// Quarter its Months, Year its Quarters. Week sits outside the month chain (issue 0014).
/// </summary>
public static class SummaryPeriods
{
    /// <summary>All periods are supported; kept so endpoints can reject an unknown route value.</summary>
    public static bool IsSupported(SummaryPeriod period) => Enum.IsDefined(period);

    /// <summary>True when the period is summarized directly from Sessions (Day or Week).</summary>
    public static bool IsSessionLevel(SummaryPeriod period) =>
        period is SummaryPeriod.Day or SummaryPeriod.Week;

    /// <summary>
    /// The canonical anchor date for the period containing <paramref name="date"/>: the date itself for
    /// a Day, the ISO-8601 Monday for a Week, the first calendar day for Month/Quarter/Year.
    /// </summary>
    public static DateOnly Anchor(SummaryPeriod period, DateOnly date) => period switch
    {
        SummaryPeriod.Day => date,
        SummaryPeriod.Week => date.AddDays(-(((int)date.DayOfWeek + 6) % 7)), // back up to Monday
        SummaryPeriod.Month => new DateOnly(date.Year, date.Month, 1),
        SummaryPeriod.Quarter => new DateOnly(date.Year, ((date.Month - 1) / 3) * 3 + 1, 1),
        SummaryPeriod.Year => new DateOnly(date.Year, 1, 1),
        _ => throw new ArgumentOutOfRangeException(nameof(period)),
    };

    /// <summary>
    /// The inclusive journaling-day range a Day or Week anchor covers — the window over which Sessions
    /// are gathered. Defined only for the session-level periods.
    /// </summary>
    public static (DateOnly Start, DateOnly End) Range(SummaryPeriod period, DateOnly anchor) => period switch
    {
        SummaryPeriod.Day => (anchor, anchor),
        SummaryPeriod.Week => (anchor, anchor.AddDays(6)),
        _ => throw new NotSupportedException($"{period} is not summarized from Sessions directly."),
    };

    /// <summary>The inclusive calendar span an anchored Month/Quarter/Year covers — used to find its children.</summary>
    public static (DateOnly Start, DateOnly End) CalendarRange(SummaryPeriod period, DateOnly anchor) => period switch
    {
        SummaryPeriod.Month => (anchor, anchor.AddMonths(1).AddDays(-1)),
        SummaryPeriod.Quarter => (anchor, anchor.AddMonths(3).AddDays(-1)),
        SummaryPeriod.Year => (anchor, anchor.AddYears(1).AddDays(-1)),
        _ => throw new NotSupportedException($"{period} has no calendar child range."),
    };

    /// <summary>The tier a roll-up period summarizes: Month←Day, Quarter←Month, Year←Quarter.</summary>
    public static SummaryPeriod ChildPeriod(SummaryPeriod period) => period switch
    {
        SummaryPeriod.Month => SummaryPeriod.Day,
        SummaryPeriod.Quarter => SummaryPeriod.Month,
        SummaryPeriod.Year => SummaryPeriod.Quarter,
        _ => throw new NotSupportedException($"{period} is not a roll-up period."),
    };

    /// <summary>The next period up the month chain, or null at the top (Year) and off-chain (Week).</summary>
    public static SummaryPeriod? Parent(SummaryPeriod period) => period switch
    {
        SummaryPeriod.Day => SummaryPeriod.Month,
        SummaryPeriod.Month => SummaryPeriod.Quarter,
        SummaryPeriod.Quarter => SummaryPeriod.Year,
        _ => null, // Week is parallel (no parent); Year is the top
    };

    /// <summary>
    /// The (period, anchor) pairs above this one in the month chain — Day → Month, Quarter, Year. The
    /// targets of upward staleness propagation when this period's content changes (issue 0014).
    /// </summary>
    public static IReadOnlyList<(SummaryPeriod Period, DateOnly Anchor)> AncestorAnchors(
        SummaryPeriod period, DateOnly anchor)
    {
        var ancestors = new List<(SummaryPeriod, DateOnly)>();
        var current = period;
        var date = anchor;
        while (Parent(current) is { } parent)
        {
            date = Anchor(parent, date);
            ancestors.Add((parent, date));
            current = parent;
        }
        return ancestors;
    }
}
