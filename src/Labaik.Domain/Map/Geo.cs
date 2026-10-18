namespace Labaik.Domain.Map;

// Straight-line geometry on the WGS84 sphere (good enough at walking distances).
public static class Geo
{
    private const double EarthRadiusMeters = 6_371_000;

    public static bool IsValid(double latitude, double longitude) =>
        latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180
        && !double.IsNaN(latitude) && !double.IsNaN(longitude);

    // Haversine distance in meters.
    public static double DistanceMeters(double fromLat, double fromLng, double toLat, double toLng)
    {
        var dLat = ToRadians(toLat - fromLat);
        var dLng = ToRadians(toLng - fromLng);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(ToRadians(fromLat)) * Math.Cos(ToRadians(toLat)) * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return 2 * EarthRadiusMeters * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }

    // Initial compass bearing in degrees (0 = north, 90 = east).
    public static double BearingDegrees(double fromLat, double fromLng, double toLat, double toLng)
    {
        var lat1 = ToRadians(fromLat);
        var lat2 = ToRadians(toLat);
        var dLng = ToRadians(toLng - fromLng);
        var y = Math.Sin(dLng) * Math.Cos(lat2);
        var x = Math.Cos(lat1) * Math.Sin(lat2) - Math.Sin(lat1) * Math.Cos(lat2) * Math.Cos(dLng);
        return (Math.Atan2(y, x) * 180 / Math.PI + 360) % 360;
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;
}
