using System.Globalization;

namespace AbfuhrPilot.Application.Routing;

/// <summary>
/// Builds external deep-links for turn-by-turn navigation. AbfuhrPilot plans the
/// route; the driver taps "Navigieren" and the device's maps app takes over —
/// no paid in-app navigation SDK. Provider-neutral, no external calls.
/// </summary>
public class NavigationLinkBuilder : INavigationLinkBuilder
{
    public NavigationLinks BuildNavigationLinks(NavigationLinkRequest r)
    {
        var dest = Coord(r.Destination);
        var waypoints = r.Waypoints.Select(Coord).ToList();

        // Google Maps: origin + waypoints + destination (driving).
        var g = new List<string> { "https://www.google.com/maps/dir/?api=1", $"destination={dest}", "travelmode=driving" };
        if (r.From is not null) g.Add($"origin={Coord(r.From)}");
        if (waypoints.Count > 0) g.Add($"waypoints={string.Join("%7C", waypoints)}");
        var google = string.Join("&", g);

        // Apple Maps: start → destination (Apple has limited multi-stop support).
        var apple = r.From is not null
            ? $"https://maps.apple.com/?saddr={Coord(r.From)}&daddr={dest}&dirflg=d"
            : $"https://maps.apple.com/?daddr={dest}&dirflg=d";

        // OpenStreetMap directions: from;to (first waypoint..destination).
        var osmFrom = r.From is not null ? Coord(r.From) : (waypoints.FirstOrDefault() ?? dest);
        var osm = $"https://www.openstreetmap.org/directions?engine=fossgis_osrm_car&route={osmFrom}%3B{dest}";

        return new NavigationLinks(google, apple, osm);
    }

    private static string Coord(GeoPoint p) =>
        string.Create(CultureInfo.InvariantCulture, $"{p.Latitude},{p.Longitude}");
}
