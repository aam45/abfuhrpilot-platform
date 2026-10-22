namespace AbfuhrPilot.Application.Routing;

/// <summary>Which provider produced a routing result (provider-neutral marker).</summary>
public enum RoutingProviderType
{
    None = 0,
    LocalFallback = 1,
    GraphHopper = 2,
    Google = 3,
    Mapbox = 4,
    Here = 5,
    Osrm = 6
}

public record GeoPoint(double Latitude, double Longitude);

public record RoutingStopInput(Guid StopId, int SequenceNumber, double Latitude, double Longitude, string? Label = null);

/// <summary>
/// Provider-neutral description of the vehicle a route will be driven with.
/// Built from the assigned <c>Vehicle</c>; each provider maps as much of it as
/// it can actually enforce — and must warn about what it cannot.
/// </summary>
public sealed record VehicleRoutingProfile
{
    public string VehicleType { get; init; } = string.Empty;
    public double? HeightMeters { get; init; }
    public double? WidthMeters { get; init; }
    public double? LengthMeters { get; init; }
    public double? WeightTons { get; init; }
    public double? MaxLoadTons { get; init; }
    public bool IsContainerTruck { get; init; }
    /// <summary>True for every Müllwagen — a waste-collection route is never a car route.</summary>
    public bool RequiresTruckRouting { get; init; } = true;
    public IReadOnlyCollection<string> AllowedWasteCategories { get; init; } = Array.Empty<string>();
}

/// <summary>A structured, machine-readable routing warning.</summary>
public sealed record RoutingWarning(string Code, string Message, string Severity)
{
    public const string SeverityInfo = "Info";
    public const string SeverityWarning = "Warning";
}

/// <summary>Stable warning codes shared by all providers (frontend maps these to German).</summary>
public static class RoutingWarningCodes
{
    public const string ProviderNotConfigured = "PROVIDER_NOT_CONFIGURED";
    public const string ProviderUsingLocalFallback = "PROVIDER_USING_LOCAL_FALLBACK";
    public const string ProviderUsingCarFallback = "PROVIDER_USING_CAR_FALLBACK";
    public const string TruckRestrictionsNotEnforced = "TRUCK_RESTRICTIONS_NOT_ENFORCED";
    public const string TruckProfileNotFullySupported = "TRUCK_PROFILE_NOT_FULLY_SUPPORTED";
    public const string NoVehicleAssigned = "NO_VEHICLE_ASSIGNED";
    public const string MissingVehicleHeight = "MISSING_VEHICLE_HEIGHT";
    public const string MissingVehicleWeight = "MISSING_VEHICLE_WEIGHT";
    public const string MissingVehicleWidth = "MISSING_VEHICLE_WIDTH";
    public const string MissingVehicleLength = "MISSING_VEHICLE_LENGTH";
    public const string StopsWithoutCoordinates = "ROUTE_HAS_STOPS_WITHOUT_COORDINATES";
    public const string ProviderCallFailed = "PROVIDER_CALL_FAILED";
    public const string TurnByTurnStepsNotAvailable = "TURN_BY_TURN_STEPS_NOT_AVAILABLE";
}

// ---- calculation ----
public record RouteCalculationRequest(
    GeoPoint? Start,
    IReadOnlyList<RoutingStopInput> Stops,
    GeoPoint? End,
    VehicleRoutingProfile? Vehicle);

public record RouteLeg(Guid? FromStopId, Guid? ToStopId, double DistanceMeters, double DurationSeconds);

/// <summary>A route line as an ordered list of points (lat/lng).</summary>
public record RouteGeometry(IReadOnlyList<GeoPoint> Points);

public record RouteCalculationResult(
    RoutingProviderType Provider,
    bool ProviderConfigured,
    string? Warning,                       // legacy human-readable summary (kept for UI compat)
    double TotalDistanceMeters,
    double TotalDurationSeconds,
    IReadOnlyList<RouteLeg> Legs,
    RouteGeometry? Geometry,
    IReadOnlyList<Guid> ExcludedStopIds)
{
    /// <summary>Structured warnings (truck restrictions, missing dimensions, fallbacks …).</summary>
    public IReadOnlyList<RoutingWarning> Warnings { get; init; } = Array.Empty<RoutingWarning>();
    /// <summary>Profile the provider actually routed with (e.g. "car", "truck").</summary>
    public string? RoutingProfileUsed { get; init; }
    /// <summary>True only when the provider genuinely enforced truck restrictions.</summary>
    public bool TruckRestrictionsApplied { get; init; }
}

// ---- optimization ----
public record RouteOptimizationRequest(
    GeoPoint? Start,
    IReadOnlyList<RoutingStopInput> Stops,
    GeoPoint? End,
    VehicleRoutingProfile? Vehicle);

public record OptimizedStop(Guid StopId, int NewSequenceNumber);

public record RouteOptimizationResult(
    RoutingProviderType Provider,
    bool ProviderConfigured,
    string? Warning,                       // legacy human-readable summary (kept for UI compat)
    IReadOnlyList<OptimizedStop> Order,
    double TotalDistanceMeters,
    double TotalDurationSeconds,
    IReadOnlyList<Guid> ExcludedStopIds,
    RouteGeometry? Geometry)
{
    public IReadOnlyList<RoutingWarning> Warnings { get; init; } = Array.Empty<RoutingWarning>();
    public string? RoutingProfileUsed { get; init; }
    public bool TruckRestrictionsApplied { get; init; }
}

// ---- navigation links ----
public record NavigationLinkRequest(
    GeoPoint? From,
    IReadOnlyList<GeoPoint> Waypoints,
    GeoPoint Destination,
    string? DestinationLabel);

public record NavigationLinks(string GoogleMaps, string AppleMaps, string OpenStreetMap);

// ---- provider interfaces (provider-neutral; implementations live in Infra/App) ----
public interface IRoutingProvider
{
    RoutingProviderType Type { get; }
    bool IsConfigured { get; }
    Task<RouteCalculationResult> CalculateRouteAsync(RouteCalculationRequest request, CancellationToken ct);
}

public interface IRouteOptimizationProvider
{
    RoutingProviderType Type { get; }
    bool IsConfigured { get; }
    Task<RouteOptimizationResult> OptimizeRouteAsync(RouteOptimizationRequest request, CancellationToken ct);
}

public interface INavigationLinkBuilder
{
    NavigationLinks BuildNavigationLinks(NavigationLinkRequest request);
}
