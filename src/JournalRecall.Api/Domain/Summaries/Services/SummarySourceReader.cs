using Microsoft.EntityFrameworkCore;
using JournalRecall.Api.Databases;
using JournalRecall.Api.Domain.Sessions;
using JournalRecall.Api.Services;

namespace JournalRecall.Api.Domain.Summaries.Services;

/// <summary>
/// Reads the Session texts that feed a Day/Week Summary (issue 0013). Membership is by <em>journaling
/// day</em> — derived by projecting each Session's UTC timestamp into the User's timezone (CONTEXT.md),
/// so it can't be filtered purely in SQL. We prefilter by a padded UTC window (±1 day covers any real
/// timezone offset) and then keep only the Sessions whose journaling day falls in the period's range.
/// Each Session contributes its Cleaned copy when present, else its Raw text.
/// </summary>
public sealed class SummarySourceReader(JournalRecallDbContext db, ICurrentUserService currentUser)
{
    /// <summary>The ordered (oldest-first) source texts for the anchored period; empty when none.</summary>
    public async Task<IReadOnlyList<string>> ReadAsync(
        SummaryPeriod period, DateOnly anchor, CancellationToken cancellationToken = default)
    {
        var (start, end) = SummaryPeriods.Range(period, anchor);
        var windowStart = new DateTimeOffset(start.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).AddDays(-1);
        var windowEnd = new DateTimeOffset(end.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).AddDays(2);

        var candidates = await db.Sessions.AsNoTracking()
            .Where(s => s.CreatedAt >= windowStart && s.CreatedAt < windowEnd)
            .OrderBy(s => s.CreatedAt)
            .Select(s => new { s.CreatedAt, s.RawPlainText, s.CleanedPlainText })
            .ToListAsync(cancellationToken);

        var timeZoneId = await CurrentUserTimeZone(cancellationToken);

        return candidates
            .Where(s =>
            {
                var day = JournalingDay.For(s.CreatedAt, timeZoneId);
                return day >= start && day <= end;
            })
            // Read the Cleaned copy when the Session has one, else the Raw text (acceptance criteria).
            // Both are the derived plaintext projections (ADR-0009) — the AI never sees the JSON markup.
            .Select(s => string.IsNullOrWhiteSpace(s.CleanedPlainText) ? s.RawPlainText : s.CleanedPlainText)
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToList();
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
