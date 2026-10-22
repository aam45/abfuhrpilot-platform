namespace AbfuhrPilot.Application.Routing;

/// <summary>Provider-neutral routing configuration (bound from the "Routing" section).</summary>
public class RoutingOptions
{
    /// <summary>Desired provider, e.g. "GraphHopper". Falls back to local if not configured.</summary>
    public string Provider { get; set; } = "GraphHopper";
    public GraphHopperOptions GraphHopper { get; set; } = new();
    public string FallbackProvider { get; set; } = "None";

    public RoutingProviderType DesiredProvider =>
        Enum.TryParse<RoutingProviderType>(Provider, ignoreCase: true, out var t) ? t : RoutingProviderType.None;
}

public class GraphHopperOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://graphhopper.com/api/1";
    public int TimeoutSeconds { get; set; } = 15;

    /// <summary>
    /// GraphHopper routing profile: "car" (default), "truck", "small_truck" or
    /// "custom". Truck restrictions are only claimed as active when a
    /// truck-capable profile is configured here — never implied.
    /// </summary>
    public string Profile { get; set; } = "car";

    public bool IsTruckProfile =>
        Profile.Equals("truck", StringComparison.OrdinalIgnoreCase) ||
        Profile.Equals("small_truck", StringComparison.OrdinalIgnoreCase) ||
        Profile.Equals("custom", StringComparison.OrdinalIgnoreCase);
}
