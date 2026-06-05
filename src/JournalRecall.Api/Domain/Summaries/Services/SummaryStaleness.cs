using Microsoft.EntityFrameworkCore;
using JournalRecall.Api.Databases;
using JournalRecall.Api.Domain.Sessions;
using JournalRecall.Api.Services;

namespace JournalRecall.Api.Domain.Summaries.Services;

/// <summary>
/// Propagates staleness up the Summary chain (issue 0014). A Summary goes Stale when anything beneath it
/// changes (CONTEXT.md): editing a Session marks its Day and Week — and, up the month chain, its Month,
/// Quarter, and Year — Stale; regenerating a lower Summary marks its ancestors Stale. Only Ready
/// Summaries flip (see <see cref="Summary.MarkStale"/>), leaving content for the regenerate affordance.
/// Per-user via the global query filter.
/// </summary>
public sealed class SummaryStaleness(JournalRecallDbContext db, ICurrentUserService currentUser)
{
    /// <summary>
    /// Marks the Summaries covering a changed Session's journaling day Stale: its Day and Week (which read
    /// the Session directly) plus its Month, Quarter, and Year up the chain.
    /// </summary>
    public async Task MarkStaleForSessionAsync(Session session, CancellationToken cancellationToken = default)
    {
        var timeZoneId = await CurrentUserTimeZone(cancellationToken);
        var day = JournalingDay.For(session.CreatedAt, timeZoneId);

        var targets = new List<(SummaryPeriod Period, DateOnly Anchor)>
        {
            (SummaryPeriod.Day, day),
            (SummaryPeriod.Week, SummaryPeriods.Anchor(SummaryPeriod.Week, day)),
        };
        targets.AddRange(SummaryPeriods.AncestorAnchors(SummaryPeriod.Day, day)); // Month, Quarter, Year

        await MarkStaleAsync(targets, cancellationToken);
    }

    /// <summary>Marks the period Summaries above this one Stale (e.g. a regenerated Day → Month, Quarter, Year).</summary>
    public Task PropagateAboveAsync(
        SummaryPeriod period, DateOnly anchor, CancellationToken cancellationToken = default) =>
        MarkStaleAsync(SummaryPeriods.AncestorAnchors(period, anchor), cancellationToken);

    private async Task MarkStaleAsync(
        IReadOnlyList<(SummaryPeriod Period, DateOnly Anchor)> targets, CancellationToken cancellationToken)
    {
        if (targets.Count == 0)
            return;

        var changed = false;
        foreach (var (period, anchor) in targets)
        {
            var summary = await db.Summaries
                .FirstOrDefaultAsync(s => s.Period == period && s.PeriodDate == anchor, cancellationToken);
            if (summary is null)
                continue; // a period with no Summary yet has nothing to invalidate

            summary.MarkStale();
            changed = true;
        }

        if (changed)
            await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<string?> CurrentUserTimeZone(CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId;
        if (userId is null)
            return null;

        return await db.Users
            .Where(u => u.Id == userId)
            .Select(u => u.TimeZoneId)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
