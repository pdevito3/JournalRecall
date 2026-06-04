using Shouldly;
using JournalRecall.Api.Domain.Sessions;

namespace JournalRecall.Api.Tests;

/// <summary>
/// Pure test: the journaling day is the local calendar date in the user's timezone (CONTEXT.md).
/// The boundary case — two instants 20 minutes apart straddling local midnight — must land on
/// different days in a zone but the same day in UTC.
/// </summary>
public class JournalingDayTests
{
    // 03:50Z and 04:10Z on 2026-06-04 straddle midnight in America/New_York (UTC-4 in June).
    private static readonly DateTimeOffset BeforeLocalMidnight = new(2026, 6, 4, 3, 50, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset AfterLocalMidnight = new(2026, 6, 4, 4, 10, 0, TimeSpan.Zero);

    [Fact]
    public void Straddling_local_midnight_lands_on_different_days_in_a_zone()
    {
        JournalingDay.For(BeforeLocalMidnight, "America/New_York").ShouldBe(new DateOnly(2026, 6, 3));
        JournalingDay.For(AfterLocalMidnight, "America/New_York").ShouldBe(new DateOnly(2026, 6, 4));
    }

    [Fact]
    public void Same_instants_share_the_day_in_utc()
    {
        JournalingDay.For(BeforeLocalMidnight, "UTC").ShouldBe(new DateOnly(2026, 6, 4));
        JournalingDay.For(BeforeLocalMidnight, null).ShouldBe(new DateOnly(2026, 6, 4)); // null ⇒ UTC
    }

    [Fact]
    public void Validates_timezone_ids()
    {
        JournalingDay.IsValidTimeZone("America/New_York").ShouldBeTrue();
        JournalingDay.IsValidTimeZone(null).ShouldBeTrue(); // null ⇒ UTC is valid
        JournalingDay.IsValidTimeZone("Not/AZone").ShouldBeFalse();
    }
}
