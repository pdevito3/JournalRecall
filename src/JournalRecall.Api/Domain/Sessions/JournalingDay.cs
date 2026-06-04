namespace JournalRecall.Api.Domain.Sessions;

/// <summary>
/// Derives the journaling day (CONTEXT.md): the calendar date a Session belongs to, computed by
/// projecting its absolute UTC timestamp into the User's configured timezone. UTC is stored;
/// day/week/month membership is always derived, never stored.
/// </summary>
public static class JournalingDay
{
    /// <summary>The journaling day for an instant in the given IANA timezone (null/invalid ⇒ UTC).</summary>
    public static DateOnly For(DateTimeOffset instant, string? timeZoneId)
    {
        var local = TimeZoneInfo.ConvertTime(instant, Resolve(timeZoneId));
        return DateOnly.FromDateTime(local.DateTime);
    }

    /// <summary>True when the id is null/empty (⇒ UTC) or a resolvable IANA timezone.</summary>
    public static bool IsValidTimeZone(string? timeZoneId) => TryResolve(timeZoneId, out _);

    private static TimeZoneInfo Resolve(string? timeZoneId) =>
        TryResolve(timeZoneId, out var tz) ? tz : TimeZoneInfo.Utc;

    private static bool TryResolve(string? timeZoneId, out TimeZoneInfo timeZone)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            timeZone = TimeZoneInfo.Utc;
            return true;
        }

        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return true;
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            timeZone = TimeZoneInfo.Utc;
            return false;
        }
    }
}
