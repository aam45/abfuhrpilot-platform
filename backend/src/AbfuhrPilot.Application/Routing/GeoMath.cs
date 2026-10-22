namespace AbfuhrPilot.Application.Routing;

/// <summary>Great-circle distance helpers used by the local fallback provider.</summary>
public static class GeoMath
{
    private const double EarthRadiusMeters = 6_371_000;

    public static double HaversineMeters(GeoPoint a, GeoPoint b)
    {
        double dLat = ToRad(b.Latitude - a.Latitude);
        double dLng = ToRad(b.Longitude - a.Longitude);
        double lat1 = ToRad(a.Latitude), lat2 = ToRad(b.Latitude);
        double h = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(lat1) * Math.Cos(lat2) * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return 2 * EarthRadiusMeters * Math.Asin(Math.Min(1, Math.Sqrt(h)));
    }

    private static double ToRad(double deg) => deg * Math.PI / 180.0;
}
