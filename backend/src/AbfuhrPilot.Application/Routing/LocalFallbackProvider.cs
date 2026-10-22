namespace AbfuhrPilot.Application.Routing;

/// <summary>
/// Deterministic, dependency-free routing/optimization used when no external
/// provider is configured (or when a provider call fails). It never reorders
/// stops — it keeps the existing sequence and estimates distance/duration from
/// straight-line (great-circle) geometry, so it can never corrupt a route.
/// It NEVER pretends to be truck-safe: every result carries explicit warnings.
/// </summary>
public class LocalFallbackProvider : IRoutingProvider, IRouteOptimizationProvider, INavigationProvider
{
    // Rough urban average speed for the duration estimate (m/s ≈ 30 km/h).
    private const double AvgSpeedMps = 30_000.0 / 3600.0;
    private const string NotConfigured = "Routing provider not configured";

    public RoutingProviderType Type => RoutingProviderType.LocalFallback;
    public bool IsConfigured => true; // always available

    /// <summary>The fallback estimates straight lines — it enforces no road rules at all.</summary>
    public static IReadOnlyList<RoutingWarning> TruckWarnings { get; } = new[]
    {
        new RoutingWarning(RoutingWarningCodes.ProviderUsingLocalFallback,
            "Routing provider not configured — using local straight-line estimate.",
            RoutingWarning.SeverityInfo),
        new RoutingWarning(RoutingWarningCodes.TruckRestrictionsNotEnforced,
            "Local fallback does not enforce truck restrictions.",
            RoutingWarning.SeverityWarning),
    };

    public Task<RouteCalculationResult> CalculateRouteAsync(RouteCalculationRequest request, CancellationToken ct)
    {
        var ordered = request.Stops.OrderBy(s => s.SequenceNumber).ToList();
        var points = BuildPointPath(request.Start, ordered, request.End, out var legs, out var totalDistance);
        var duration = totalDistance / AvgSpeedMps;

        var result = new RouteCalculationResult(
            RoutingProviderType.LocalFallback, ProviderConfigured: false, Warning: NotConfigured,
            totalDistance, duration, legs, new RouteGeometry(points), Array.Empty<Guid>())
        {
            Warnings = TruckWarnings,
            RoutingProfileUsed = "straight-line",
            TruckRestrictionsApplied = false,
        };
        return Task.FromResult(result);
    }

    public Task<RouteOptimizationResult> OptimizeRouteAsync(RouteOptimizationRequest request, CancellationToken ct)
    {
        // Fallback keeps the existing order (safe: no reordering without a real optimizer).
        var ordered = request.Stops.OrderBy(s => s.SequenceNumber).ToList();
        _ = BuildPointPath(request.Start, ordered, request.End, out _, out var totalDistance);
        var order = ordered.Select((s, i) => new OptimizedStop(s.StopId, i + 1)).ToList();

        var result = new RouteOptimizationResult(
            RoutingProviderType.LocalFallback, ProviderConfigured: false, Warning: NotConfigured,
            order, totalDistance, totalDistance / AvgSpeedMps, Array.Empty<Guid>(), null)
        {
            Warnings = TruckWarnings,
            RoutingProfileUsed = "straight-line",
            TruckRestrictionsApplied = false,
        };
        return Task.FromResult(result);
    }

    /// <summary>
    /// In-app navigation without a provider: stop-to-stop legs with straight-line
    /// distances and **empty step lists** — the fallback never invents maneuvers.
    /// </summary>
    public Task<ProviderNavigation> GetNavigationAsync(RouteCalculationRequest request, CancellationToken ct)
    {
        var ordered = request.Stops.OrderBy(s => s.SequenceNumber).ToList();
        var points = BuildPointPath(request.Start, ordered, request.End, out _, out var totalDistance);

        var legs = new List<NavigationLeg>();
        GeoPoint? prev = request.Start;
        Guid? prevId = null;
        var prevLabel = request.Start is not null ? "Start / Betriebshof" : string.Empty;
        foreach (var s in ordered)
        {
            var here = new GeoPoint(s.Latitude, s.Longitude);
            var label = s.Label ?? $"Stopp {s.SequenceNumber}";
            if (prev is not null)
            {
                var d = GeoMath.HaversineMeters(prev, here);
                legs.Add(new NavigationLeg(prevId, s.StopId, prevLabel, label, d, d / AvgSpeedMps,
                    Array.Empty<NavigationStep>()));
            }
            prev = here; prevId = s.StopId; prevLabel = label;
        }
        if (request.End is not null && prev is not null)
        {
            var d = GeoMath.HaversineMeters(prev, request.End);
            legs.Add(new NavigationLeg(prevId, null, prevLabel, "Ziel / Recyclinghof", d, d / AvgSpeedMps,
                Array.Empty<NavigationStep>()));
        }

        var warnings = TruckWarnings.Concat(new[]
        {
            new RoutingWarning(RoutingWarningCodes.TurnByTurnStepsNotAvailable,
                "Lokaler Fallback: Keine echten Abbiegehinweise verfügbar.",
                RoutingWarning.SeverityWarning),
        }).ToList();

        return Task.FromResult(new ProviderNavigation(
            RoutingProviderType.LocalFallback, ProviderConfigured: false, points,
            totalDistance, totalDistance / AvgSpeedMps, legs, warnings,
            "straight-line", TruckRestrictionsApplied: false));
    }

    private static List<GeoPoint> BuildPointPath(GeoPoint? start, List<RoutingStopInput> stops, GeoPoint? end,
        out List<RouteLeg> legs, out double totalDistance)
    {
        legs = new List<RouteLeg>();
        totalDistance = 0;
        var points = new List<GeoPoint>();

        GeoPoint? prev = start;
        Guid? prevId = null;
        if (start is not null) points.Add(start);

        foreach (var s in stops)
        {
            var here = new GeoPoint(s.Latitude, s.Longitude);
            points.Add(here);
            if (prev is not null)
            {
                var d = GeoMath.HaversineMeters(prev, here);
                totalDistance += d;
                legs.Add(new RouteLeg(prevId, s.StopId, d, d / AvgSpeedMps));
            }
            prev = here;
            prevId = s.StopId;
        }

        if (end is not null)
        {
            points.Add(end);
            if (prev is not null)
            {
                var d = GeoMath.HaversineMeters(prev, end);
                totalDistance += d;
                legs.Add(new RouteLeg(prevId, null, d, d / AvgSpeedMps));
            }
        }
        return points;
    }
}
