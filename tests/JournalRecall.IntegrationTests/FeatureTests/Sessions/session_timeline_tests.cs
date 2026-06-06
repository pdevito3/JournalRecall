using Microsoft.EntityFrameworkCore;
using JournalRecall.Api.Domain.Sessions;
using JournalRecall.Api.Domain.Sessions.Features;

namespace JournalRecall.IntegrationTests.FeatureTests.Sessions;

/// <summary>
/// Timeline (issue 0006) at the integration layer: reverse-chronological, bucketed by journaling day in
/// the user's timezone, QueryKit date-range filtered, and reflecting current state only — driven through
/// the GetSessionList query with no HTTP. (The endpoint/query-string shape is covered functionally.)
/// </summary>
public class session_timeline_tests : TestBase
{
    private static async Task<Guid> NewSession(TestingServiceScope scope) =>
        (await scope.SendAsync(new CreateSession.Command(null, null))).Id;

    private static Task SetCreatedAt(TestingServiceScope scope, Guid id, DateTimeOffset when) =>
        scope.ExecuteDbContextAsync(async db =>
        {
            var session = await db.Sessions.IgnoreQueryFilters().FirstAsync(s => s.Id == id);
            db.Entry(session).Property(nameof(Session.CreatedAt)).CurrentValue = when;
            await db.SaveChangesAsync();
        });

    private static Task SetTimeZone(TestingServiceScope scope, string? timeZoneId) =>
        scope.ExecuteDbContextAsync(async db =>
        {
            var user = await db.Users.FirstAsync(u => u.Id == scope.CurrentUserId);
            user.TimeZoneId = timeZoneId;
            await db.SaveChangesAsync();
        });

    [Fact]
    public async Task sessions_are_newest_first_and_bucketed_by_journaling_day_in_the_users_timezone()
    {
        using var scope = new TestingServiceScope();
        var early = await NewSession(scope);
        var late = await NewSession(scope);
        await SetCreatedAt(scope, early, new DateTimeOffset(2026, 6, 4, 3, 50, 0, TimeSpan.Zero));  // NY: 06-03 23:50
        await SetCreatedAt(scope, late, new DateTimeOffset(2026, 6, 4, 4, 10, 0, TimeSpan.Zero));   // NY: 06-04 00:10
        await SetTimeZone(scope, "America/New_York");

        var timeline = await scope.SendAsync(new GetSessionList.Query(null));

        timeline[0].Id.ShouldBe(late);  // newest first
        timeline[1].Id.ShouldBe(early);
        timeline.Single(i => i.Id == early).JournalingDay.ShouldBe(new DateOnly(2026, 6, 3));
        timeline.Single(i => i.Id == late).JournalingDay.ShouldBe(new DateOnly(2026, 6, 4));
    }

    [Fact]
    public async Task changing_the_timezone_rebuckets_across_the_day_boundary()
    {
        using var scope = new TestingServiceScope();
        var id = await NewSession(scope);
        await SetCreatedAt(scope, id, new DateTimeOffset(2026, 6, 4, 3, 50, 0, TimeSpan.Zero));

        await SetTimeZone(scope, "America/New_York");
        (await scope.SendAsync(new GetSessionList.Query(null))).Single(i => i.Id == id)
            .JournalingDay.ShouldBe(new DateOnly(2026, 6, 3));

        await SetTimeZone(scope, "UTC");
        (await scope.SendAsync(new GetSessionList.Query(null))).Single(i => i.Id == id)
            .JournalingDay.ShouldBe(new DateOnly(2026, 6, 4));
    }

    [Fact]
    public async Task querykit_date_range_filter_returns_only_matching_sessions()
    {
        using var scope = new TestingServiceScope();
        var june1 = await NewSession(scope);
        var june10 = await NewSession(scope);
        await SetCreatedAt(scope, june1, new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        await SetCreatedAt(scope, june10, new DateTimeOffset(2026, 6, 10, 12, 0, 0, TimeSpan.Zero));

        var filtered = await scope.SendAsync(new GetSessionList.Query("CreatedAt >= \"2026-06-05T00:00:00Z\""));

        filtered.Select(i => i.Id).ShouldBe([june10]);
    }

    [Fact]
    public async Task timeline_reflects_current_state_only_one_row_per_session_despite_revisions()
    {
        using var scope = new TestingServiceScope();
        var id = await NewSession(scope);
        foreach (var text in new[] { "v1", "v2", "v3" })
            await scope.SendAsync(new SaveDraft.Command(id, text));

        var rows = (await scope.SendAsync(new GetSessionList.Query(null))).Where(i => i.Id == id).ToList();

        rows.Count.ShouldBe(1); // historical Revisions never appear as separate rows
        rows[0].Preview.ShouldBe("v3");
    }
}
