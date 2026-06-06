using JournalRecall.Api.Domain.Sessions;

namespace JournalRecall.UnitTests.Domain.Sessions;

/// <summary>Pure unit tests for the <see cref="Location"/> value object's range validation.</summary>
public class location_tests
{
    [Fact]
    public void a_valid_point_is_created()
    {
        Location.TryCreate(40.7128, -74.0060, out var location).ShouldBeTrue();
        location.Latitude.ShouldBe(40.7128);
        location.Longitude.ShouldBe(-74.0060);
    }

    [Theory]
    [InlineData(90, 180)]
    [InlineData(-90, -180)]
    [InlineData(0, 0)]
    public void boundary_coordinates_are_valid(double lat, double lng)
    {
        Location.TryCreate(lat, lng, out _).ShouldBeTrue();
    }

    [Fact]
    public void a_partial_point_is_not_a_location()
    {
        Location.TryCreate(40.7, null, out _).ShouldBeFalse();
        Location.TryCreate(null, -74.0, out _).ShouldBeFalse();
        Location.TryCreate(null, null, out _).ShouldBeFalse();
    }

    [Theory]
    [InlineData(90.1, 0)]
    [InlineData(-90.1, 0)]
    [InlineData(0, 180.1)]
    [InlineData(0, -180.1)]
    [InlineData(double.NaN, 0)]
    [InlineData(0, double.NaN)]
    public void out_of_range_or_nan_coordinates_are_rejected(double lat, double lng)
    {
        Location.TryCreate(lat, lng, out _).ShouldBeFalse();
    }
}
