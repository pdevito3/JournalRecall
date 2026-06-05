using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;

namespace JournalRecall.Api.Tests;

/// <summary>
/// Location opt-in (issue 0015): off by default (no location captured even if coordinates are posted);
/// when on, a single lat/long is stored at Session creation and shown on the Session, declining leaves
/// it empty, and the setting is strictly per-user.
/// </summary>
public class LocationTests : IClassFixture<SkeletonWebApplicationFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly SkeletonWebApplicationFactory _factory;

    public LocationTests(SkeletonWebApplicationFactory factory) => _factory = factory;

    private sealed record Credentials(string Email, string Password);
    private sealed record LocationDto(double Latitude, double Longitude);
    private sealed record SessionDto(Guid Id, LocationDto? Location);

    private const double Lat = 40.7128;
    private const double Lng = -74.0060;

    private async Task<HttpClient> SignedInClient()
    {
        var client = _factory.CreateClient();
        var creds = new Credentials($"user-{Guid.NewGuid():N}@example.com", "Passw0rd!");
        await client.PostAsJsonAsync("/api/auth/register", creds);
        await client.PostAsJsonAsync("/api/auth/login", creds);
        return client;
    }

    private static async Task EnableGeo(HttpClient client) =>
        (await client.PutAsJsonAsync("/api/me/settings", new { timeZoneId = (string?)null, locationCaptureEnabled = true }))
            .EnsureSuccessStatusCode();

    private static async Task<SessionDto> CreateWith(HttpClient client, object? body)
    {
        var res = body is null
            ? await client.PostAsync("/api/sessions", null)
            : await client.PostAsJsonAsync("/api/sessions", body);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<SessionDto>(Json))!;
    }

    private static async Task<SessionDto> Get(HttpClient client, Guid id) =>
        (await client.GetFromJsonAsync<SessionDto>($"/api/sessions/{id}", Json))!;

    [Fact]
    public async Task With_opt_in_off_by_default_no_location_is_stored_even_if_posted()
    {
        var client = await SignedInClient();

        // A point is posted, but the user never opted in — the server must ignore it.
        var session = await CreateWith(client, new { latitude = Lat, longitude = Lng });

        session.Location.ShouldBeNull();
        (await Get(client, session.Id)).Location.ShouldBeNull();
    }

    [Fact]
    public async Task With_opt_in_on_a_posted_point_is_stored_and_shown_on_the_session()
    {
        var client = await SignedInClient();
        await EnableGeo(client);

        var created = await CreateWith(client, new { latitude = Lat, longitude = Lng });

        created.Location.ShouldNotBeNull();
        created.Location!.Latitude.ShouldBe(Lat);
        created.Location.Longitude.ShouldBe(Lng);

        // …and it persists as a single point on the Session view.
        var view = await Get(client, created.Id);
        view.Location.ShouldNotBeNull();
        view.Location!.Latitude.ShouldBe(Lat);
        view.Location.Longitude.ShouldBe(Lng);
    }

    [Fact]
    public async Task With_opt_in_on_declining_leaves_the_location_empty()
    {
        var client = await SignedInClient();
        await EnableGeo(client);

        // Opted in, but the user declined for this Session (no body) — nothing is stored.
        var session = await CreateWith(client, body: null);

        session.Location.ShouldBeNull();
    }

    [Fact]
    public async Task An_out_of_range_point_is_treated_as_no_location()
    {
        var client = await SignedInClient();
        await EnableGeo(client);

        var session = await CreateWith(client, new { latitude = 999.0, longitude = 0.0 });

        session.Location.ShouldBeNull();
    }

    [Fact]
    public async Task The_geo_setting_is_per_user()
    {
        var alice = await SignedInClient();
        await EnableGeo(alice);
        (await CreateWith(alice, new { latitude = Lat, longitude = Lng })).Location.ShouldNotBeNull();

        // Bob never opted in; Alice enabling it must not affect his Sessions.
        var bob = await SignedInClient();
        (await CreateWith(bob, new { latitude = Lat, longitude = Lng })).Location.ShouldBeNull();
    }
}
