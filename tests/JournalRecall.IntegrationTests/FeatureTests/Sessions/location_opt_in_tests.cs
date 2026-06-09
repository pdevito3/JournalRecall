using Microsoft.EntityFrameworkCore;
using JournalRecall.Api.Domain.Sessions.Features;

namespace JournalRecall.IntegrationTests.FeatureTests.Sessions;

/// <summary>
/// Location opt-in (issue 0015) at the integration layer: off by default (no location stored even if
/// coordinates are supplied); when on, a valid point is stored; declining or an out-of-range point leaves
/// it empty; and the setting is strictly per-User — all via CreateSession with no HTTP.
/// </summary>
public class location_opt_in_tests : TestBase
{
    private const double Lat = 40.7128;
    private const double Lng = -74.0060;

    private static Task EnableGeo(TestingServiceScope scope) =>
        scope.ExecuteDbContextAsync(async db =>
        {
            var user = await db.Users.FirstAsync(u => u.Id == scope.CurrentUserId);
            user.LocationCaptureEnabled = true;
            await db.SaveChangesAsync();
        });

    [Fact]
    public async Task with_opt_in_off_by_default_no_location_is_stored_even_if_supplied()
    {
        using var scope = new TestingServiceScope();

        var dto = await scope.SendAsync(new CreateSession.Command(Lat, Lng));

        dto!.Location.ShouldBeNull();
    }

    [Fact]
    public async Task with_opt_in_on_a_supplied_point_is_stored_and_shown()
    {
        using var scope = new TestingServiceScope();
        await EnableGeo(scope);

        var dto = await scope.SendAsync(new CreateSession.Command(Lat, Lng));

        dto!.Location.ShouldNotBeNull();
        dto.Location!.Latitude.ShouldBe(Lat);
        dto.Location.Longitude.ShouldBe(Lng);

        // …and it persists on the Session view.
        var view = await scope.SendAsync(new GetSession.Query(dto.Id));
        view!.Location!.Latitude.ShouldBe(Lat);
    }

    [Fact]
    public async Task with_opt_in_on_declining_leaves_the_location_empty()
    {
        using var scope = new TestingServiceScope();
        await EnableGeo(scope);

        var dto = await scope.SendAsync(new CreateSession.Command(null, null));

        dto!.Location.ShouldBeNull();
    }

    [Fact]
    public async Task an_out_of_range_point_is_treated_as_no_location()
    {
        using var scope = new TestingServiceScope();
        await EnableGeo(scope);

        var dto = await scope.SendAsync(new CreateSession.Command(999.0, 0.0));

        dto!.Location.ShouldBeNull();
    }

    [Fact]
    public async Task the_geo_setting_is_per_user()
    {
        using var alice = new TestingServiceScope();
        await EnableGeo(alice);
        (await alice.SendAsync(new CreateSession.Command(Lat, Lng)))!.Location.ShouldNotBeNull();

        // Bob never opted in; Alice enabling it must not affect his Sessions.
        using var bob = new TestingServiceScope();
        (await bob.SendAsync(new CreateSession.Command(Lat, Lng)))!.Location.ShouldBeNull();
    }
}
