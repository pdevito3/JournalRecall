namespace JournalRecall.Api.Domain.Summaries.Dtos;

/// <summary>
/// A period Summary as the client sees it: the period + anchor date, the current <see cref="Status"/>
/// (<see cref="SummaryStatus.Missing"/> when none has been generated yet), the narrative, the live count
/// of source items in the period (Sessions for a Day/Week, lower-level Summaries for a roll-up — so the
/// page can decide whether to generate), and the timestamp.
/// </summary>
public sealed record SummaryDto(
    SummaryPeriod Period,
    DateOnly PeriodDate,
    SummaryStatus Status,
    string Content,
    int SourceCount,
    DateTimeOffset? GeneratedAt)
{
    /// <summary>Projects a stored Summary, overriding the live in-period source count.</summary>
    public static SummaryDto From(Summary summary, int sourceCount) => new(
        summary.Period, summary.PeriodDate, summary.Status, summary.Content, sourceCount, summary.GeneratedAt);

    /// <summary>The response for a period with no stored Summary yet (carries the live source count).</summary>
    public static SummaryDto Missing(SummaryPeriod period, DateOnly anchor, int sourceCount) => new(
        period, anchor, SummaryStatus.Missing, string.Empty, sourceCount, null);
}
