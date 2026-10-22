namespace AbfuhrPilot.Application.Routing;

// ============================================================================
// In-app navigation contract (provider-neutral). The driver navigates inside
// AbfuhrPilot: geometry + legs + (real) maneuver steps. Honesty rules:
//  - steps come ONLY from a provider that really produced them;
//  - the local fallback returns stop-to-stop legs with EMPTY steps and an
//    explicit TURN_BY_TURN_STEPS_NOT_AVAILABLE warning — never fake maneuvers;
//  - truck-restriction honesty (TruckRestrictionsApplied + warnings) carries over.
// ============================================================================

/// <summary>One turn-by-turn instruction, provider-neutral.</summary>
public sealed record NavigationStep(
    int SequenceNumber,
    string Instruction,
    string ManeuverType,   // continue | turn | arrive | via | roundabout | unknown
    string? Modifier,      // left | right | slight_left | slight_right | sharp_left | sharp_right
    string? RoadName,
    double DistanceMeters,
    double DurationSeconds,
    double Latitude,
    double Longitude);

/// <summary>A drivable segment (depot→stop, stop→stop, stop→destination).</summary>
public sealed record NavigationLeg(
    Guid? FromStopId,
    Guid? ToStopId,
    string FromLabel,
    string ToLabel,
    double DistanceMeters,
    double DurationSeconds,
    IReadOnlyList<NavigationStep> Steps);

/// <summary>What a navigation-capable provider returns (before route context is merged).</summary>
public sealed record ProviderNavigation(
    RoutingProviderType Provider,
    bool ProviderConfigured,
    IReadOnlyList<GeoPoint> Geometry,
    double TotalDistanceMeters,
    double TotalDurationSeconds,
    IReadOnlyList<NavigationLeg> Legs,
    IReadOnlyList<RoutingWarning> Warnings,
    string? RoutingProfileUsed,
    bool TruckRestrictionsApplied);

/// <summary>Response of GET /api/v1/routes/{id}/in-app-navigation.</summary>
public sealed record InAppNavigationResult(
    Guid RouteId,
    RoutingProviderType Provider,
    bool ProviderConfigured,
    double TotalDistanceMeters,
    double TotalDurationSeconds,
    IReadOnlyList<GeoPoint> Geometry,
    IReadOnlyList<NavigationLeg> Legs,
    IReadOnlyList<RoutingWarning> Warnings,
    string? RoutingProfileUsed,
    bool TruckRestrictionsApplied);

/// <summary>Providers able to deliver in-app navigation data. OSRM is a planned
/// future implementation of this same interface (supports steps=true).</summary>
public interface INavigationProvider
{
    RoutingProviderType Type { get; }
    bool IsConfigured { get; }
    Task<ProviderNavigation> GetNavigationAsync(RouteCalculationRequest request, CancellationToken ct);
}
