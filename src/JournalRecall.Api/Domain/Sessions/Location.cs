namespace JournalRecall.Api.Domain.Sessions;

/// <summary>
/// An optional single geo-point stamped on a Session at creation (CONTEXT.md): coordinates only, no
/// track and no place label. Per-user opt-in and declinable per Session, so most Sessions carry none.
/// Persisted as two nullable scalar columns on the Session and reconstructed as this value object.
/// </summary>
public sealed record Location
{
    public double Latitude { get; }
    public double Longitude { get; }

    private Location(double latitude, double longitude)
    {
        Latitude = latitude;
        Longitude = longitude;
    }

    /// <summary>
    /// Builds a Location from a lat/long pair, or returns false when either is absent or out of range
    /// (latitude ±90, longitude ±180) — so a malformed or partial point is simply treated as "no location".
    /// </summary>
    public static bool TryCreate(double? latitude, double? longitude, out Location location)
    {
        location = null!;
        if (latitude is not { } lat || longitude is not { } lng)
            return false;
        if (double.IsNaN(lat) || double.IsNaN(lng) || lat is < -90 or > 90 || lng is < -180 or > 180)
            return false;

        location = new Location(lat, lng);
        return true;
    }
}
