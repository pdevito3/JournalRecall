using Microsoft.EntityFrameworkCore;
using JournalRecall.Api.Databases;

namespace JournalRecall.Api.Domain.Summaries.Services;

/// <summary>One child Summary feeding a roll-up: its anchor date and the narrative to fold up (issue 0014).</summary>
public sealed record ChildSummary(SummaryPeriod Period, DateOnly Anchor, string Content);

/// <summary>
/// Reads the lower-level Summaries that feed a roll-up (issue 0014): a Month rolls up its Day Summaries,
/// a Quarter its Months, a Year its Quarters. It uses whatever child Summaries currently exist (with
/// content) — "regenerate from the current lower-level summaries" — never cascading generation. Per-user
/// and private via the global query filter.
/// </summary>
public sealed class SummaryRollupReader(JournalRecallDbContext db)
{
    /// <summary>The existing child Summaries (with content) for an anchored roll-up period, oldest first.</summary>
    public async Task<IReadOnlyList<ChildSummary>> ReadAsync(
        SummaryPeriod period, DateOnly anchor, CancellationToken cancellationToken = default)
    {
        var childPeriod = SummaryPeriods.ChildPeriod(period);
        var (start, end) = SummaryPeriods.CalendarRange(period, anchor);

        return await db.Summaries.AsNoTracking()
            .Where(s => s.Period == childPeriod
                && s.PeriodDate >= start && s.PeriodDate <= end
                && s.Content != "")
            .OrderBy(s => s.PeriodDate)
            .Select(s => new ChildSummary(s.Period, s.PeriodDate, s.Content))
            .ToListAsync(cancellationToken);
    }
}
