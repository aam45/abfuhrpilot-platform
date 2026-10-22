using AbfuhrPilot.Application.Common;
using AbfuhrPilot.Application.Contracts;
using AbfuhrPilot.Application.Dtos;
using AbfuhrPilot.Application.Routing;
using AbfuhrPilot.Domain.Common;
using AbfuhrPilot.Domain.Entities;
using AbfuhrPilot.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AbfuhrPilot.Application.Services;

/// <summary>
/// RoutingContext orchestration: calculation / optimization / navigation links.
/// Provider-neutral (configured provider or local fallback) and module-clean:
/// vehicle data comes from the Fleet contract (<see cref="IFleetReadService"/>)
/// and compatibility from <see cref="IWasteCompatibilityService"/> — this
/// context never reads Fleet/Waste tables directly. Never mutates stop order
/// except through the explicit apply-optimized-order action.
/// </summary>
public class RoutingService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _me;
    private readonly RouteService _routes;
    private readonly IFleetReadService _fleet;
    private readonly IWasteCompatibilityService _wasteCompatibility;
    private readonly IEnumerable<IRoutingProvider> _routingProviders;
    private readonly IEnumerable<IRouteOptimizationProvider> _optimizationProviders;
    private readonly IEnumerable<INavigationProvider> _navigationProviders;
    private readonly INavigationLinkBuilder _navBuilder;
    private readonly RoutingOptions _options;
    private readonly ILogger<RoutingService> _logger;

    public RoutingService(IAppDbContext db, ICurrentUser me, RouteService routes,
        IFleetReadService fleet, IWasteCompatibilityService wasteCompatibility,
        IEnumerable<IRoutingProvider> routingProviders, IEnumerable<IRouteOptimizationProvider> optimizationProviders,
        IEnumerable<INavigationProvider> navigationProviders,
        INavigationLinkBuilder navBuilder, RoutingOptions options, ILogger<RoutingService> logger)
    {
        _db = db;
        _me = me;
        _routes = routes;
        _fleet = fleet;
        _wasteCompatibility = wasteCompatibility;
        _routingProviders = routingProviders;
        _optimizationProviders = optimizationProviders;
        _navigationProviders = navigationProviders;
        _navBuilder = navBuilder;
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// In-app navigation data for the driver cockpit. Drivers may only access
    /// their own assigned route; Admin/Dispatcher/Viewer are company-scoped.
    /// Provider failures fall back to the honest local provider (no fake steps).
    /// </summary>
    public async Task<InAppNavigationResult> GetInAppNavigationAsync(Guid routeId, CancellationToken ct)
    {
        var ctx = await LoadContextAsync(routeId, ct);
        if (_me.Role == UserRole.Driver && ctx.Route.DriverId != _me.DriverId)
            throw new ForbiddenException("Sie können nur die Navigation Ihrer eigenen Route abrufen.");

        var req = new RouteCalculationRequest(ctx.Start, ctx.Inputs, ctx.End, ctx.VehicleProfile);
        var provider = _navigationProviders.FirstOrDefault(p => p.Type == _options.DesiredProvider && p.IsConfigured)
            ?? _navigationProviders.First(p => p.Type == RoutingProviderType.LocalFallback);

        ProviderNavigation nav;
        try
        {
            nav = await provider.GetNavigationAsync(req, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Navigation provider {Provider} failed; using local fallback.", provider.Type);
            nav = await _navigationProviders.First(p => p.Type == RoutingProviderType.LocalFallback)
                .GetNavigationAsync(req, ct);
            nav = nav with
            {
                Warnings = Prepend(nav.Warnings, new RoutingWarning(RoutingWarningCodes.ProviderCallFailed,
                    $"{provider.Type} call failed — local navigation fallback used.", RoutingWarning.SeverityWarning)),
            };
        }

        return new InAppNavigationResult(
            routeId, nav.Provider, nav.ProviderConfigured,
            nav.TotalDistanceMeters, nav.TotalDurationSeconds,
            nav.Geometry, nav.Legs,
            Merge(ctx.Warnings, nav.Warnings),
            nav.RoutingProfileUsed, nav.TruckRestrictionsApplied);
    }

    public async Task<RouteCalculationResult> CalculateAsync(Guid routeId, CancellationToken ct)
    {
        var ctx = await LoadContextAsync(routeId, ct);
        var req = new RouteCalculationRequest(ctx.Start, ctx.Inputs, ctx.End, ctx.VehicleProfile);
        var provider = SelectRoutingProvider();

        RouteCalculationResult result;
        try
        {
            result = await provider.CalculateRouteAsync(req, ct);
        }
        catch (Exception ex)
        {
            // Rule #10: a provider failure must never corrupt the route — fall back cleanly.
            _logger.LogWarning(ex, "Routing provider {Provider} failed; using local fallback.", provider.Type);
            result = await LocalRouting().CalculateRouteAsync(req, ct);
            result = result with
            {
                Warning = "Routing fehlgeschlagen (Anbieterfehler) – lokale Schätzung.",
                Warnings = Prepend(result.Warnings, new RoutingWarning(RoutingWarningCodes.ProviderCallFailed,
                    $"{provider.Type} call failed — local estimate used instead.", RoutingWarning.SeverityWarning)),
            };
        }
        return result with
        {
            ExcludedStopIds = ctx.ExcludedStopIds,
            Warnings = Merge(ctx.Warnings, result.Warnings),
        };
    }

    public async Task<RouteOptimizationResult> OptimizeAsync(Guid routeId, CancellationToken ct)
    {
        // Rule #6: drivers may never optimize (defence-in-depth; also controller-gated).
        if (_me.Role == UserRole.Driver)
            throw new ForbiddenException("Fahrer dürfen Routen nicht optimieren.");

        var ctx = await LoadContextAsync(routeId, ct);

        // Rule #5: optimization is planning — only before the route starts.
        if (ctx.Route.IsStarted)
            throw new DomainException("Die Route kann nach dem Start nicht mehr optimiert werden.");
        // Rule #4: vehicle must be compatible with the route waste type (via Waste/Fleet contract).
        await EnsureVehicleCompatibleAsync(ctx, ct);

        var req = new RouteOptimizationRequest(ctx.Start, ctx.Inputs, ctx.End, ctx.VehicleProfile);
        var provider = SelectOptimizationProvider();

        RouteOptimizationResult result;
        try
        {
            result = await provider.OptimizeRouteAsync(req, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Optimization provider {Provider} failed; using local fallback.", provider.Type);
            result = await LocalOptimization().OptimizeRouteAsync(req, ct);
            result = result with
            {
                Warning = "Optimierung fehlgeschlagen (Anbieterfehler) – Reihenfolge unverändert.",
                Warnings = Prepend(result.Warnings, new RoutingWarning(RoutingWarningCodes.ProviderCallFailed,
                    $"{provider.Type} call failed — existing order kept.", RoutingWarning.SeverityWarning)),
            };
        }
        result = result with
        {
            ExcludedStopIds = ctx.ExcludedStopIds,
            Warnings = Merge(ctx.Warnings, result.Warnings),
        };

        // Rule #9: optimization is auditable.
        _logger.LogInformation(
            "Route {RouteId} optimized by {Provider} (configured={Configured}); {Stops} stops, {Excluded} excluded, ~{Km:F1} km.",
            routeId, result.Provider, result.ProviderConfigured, result.Order.Count, result.ExcludedStopIds.Count,
            result.TotalDistanceMeters / 1000.0);
        return result;
    }

    /// <summary>Applies a confirmed order (all stop ids, optimized ones first). Delegates to the
    /// tested reorder path: validates full-set + not-started, reassigns SequenceNumber.</summary>
    public Task<List<StopDto>> ApplyOptimizedOrderAsync(Guid routeId, IReadOnlyList<Guid> orderedStopIds, CancellationToken ct)
        => _routes.ReorderStopsAsync(routeId, orderedStopIds, ct);

    public async Task<NavigationLinks> NavigationLinksAsync(Guid routeId, CancellationToken ct)
    {
        var ctx = await LoadContextAsync(routeId, ct);
        // Drivers may only get links for their own route.
        if (_me.Role == UserRole.Driver && ctx.Route.DriverId != _me.DriverId)
            throw new ForbiddenException("Sie können nur Navigationslinks für Ihre eigene Route abrufen.");

        var stopPoints = ctx.Inputs.Select(s => new GeoPoint(s.Latitude, s.Longitude)).ToList();
        GeoPoint? destination = ctx.End ?? (stopPoints.Count > 0 ? stopPoints[^1] : null);
        var from = ctx.Start ?? (stopPoints.Count > 0 ? stopPoints[0] : null);
        if (destination is null)
            throw new DomainException("Keine Koordinaten für die Navigation vorhanden.");

        // Waypoints are the stops between start and destination.
        var waypoints = ctx.End is not null ? stopPoints : stopPoints.Take(Math.Max(0, stopPoints.Count - 1)).ToList();
        if (from is not null && waypoints.Count > 0 && ctx.Start is null)
            waypoints = waypoints.Skip(1).ToList(); // first stop became the origin

        return _navBuilder.BuildNavigationLinks(new NavigationLinkRequest(from, waypoints, destination, ctx.Route.Name));
    }

    // ---- helpers ----
    private IRoutingProvider SelectRoutingProvider()
    {
        var desired = _options.DesiredProvider;
        return _routingProviders.FirstOrDefault(p => p.Type == desired && p.IsConfigured) ?? LocalRouting();
    }

    private IRouteOptimizationProvider SelectOptimizationProvider()
    {
        var desired = _options.DesiredProvider;
        return _optimizationProviders.FirstOrDefault(p => p.Type == desired && p.IsConfigured) ?? LocalOptimization();
    }

    private IRoutingProvider LocalRouting() =>
        _routingProviders.First(p => p.Type == RoutingProviderType.LocalFallback);
    private IRouteOptimizationProvider LocalOptimization() =>
        _optimizationProviders.First(p => p.Type == RoutingProviderType.LocalFallback);

    private async Task EnsureVehicleCompatibleAsync(RouteContext ctx, CancellationToken ct)
    {
        if (ctx.Route.VehicleId is not Guid vehicleId) return;
        var ok = await _wasteCompatibility.VehicleCanCollectWasteAsync(vehicleId, ctx.Route.WasteTypeId, ct);
        if (!ok)
            throw new DomainException("The assigned vehicle is not allowed to collect this route's waste type.");
    }

    private async Task<RouteContext> LoadContextAsync(Guid routeId, CancellationToken ct)
    {
        var route = await _db.Routes.Include(r => r.Stops)
            .FirstOrDefaultAsync(r => r.Id == routeId && r.CompanyId == _me.CompanyId, ct)
            ?? throw new NotFoundException("Route not found.");

        var ordered = route.Stops.OrderBy(s => s.SequenceNumber).ToList();
        var inputs = ordered
            .Where(s => s.Latitude != null && s.Longitude != null)
            .Select(s => new RoutingStopInput(s.Id, s.SequenceNumber, s.Latitude!.Value, s.Longitude!.Value, s.Address))
            .ToList();
        var excluded = ordered.Where(s => s.Latitude == null || s.Longitude == null).Select(s => s.Id).ToList();

        var start = await DestinationPoint(route.StartDepotId, ct);
        var end = await DestinationPoint(route.EndDestinationId, ct);

        // Fleet data comes through the module contract (no direct Vehicles query).
        var profile = route.VehicleId is Guid vehicleId
            ? await _fleet.GetVehicleRoutingProfileAsync(vehicleId, ct)
            : null;
        var warnings = DeriveContextWarnings(profile, hasVehicleAssigned: route.VehicleId is not null, excluded.Count);

        return new RouteContext(route, inputs, excluded, start, end, profile, warnings);
    }

    /// <summary>Honest warnings about anything routing cannot fully account for.</summary>
    internal static IReadOnlyList<RoutingWarning> DeriveContextWarnings(
        VehicleRoutingProfile? profile, bool hasVehicleAssigned, int excludedStopCount)
    {
        var warnings = new List<RoutingWarning>();

        if (excludedStopCount > 0)
            warnings.Add(new RoutingWarning(RoutingWarningCodes.StopsWithoutCoordinates,
                $"{excludedStopCount} stop(s) without coordinates were excluded from routing.",
                RoutingWarning.SeverityWarning));

        if (!hasVehicleAssigned || profile is null)
        {
            warnings.Add(new RoutingWarning(RoutingWarningCodes.NoVehicleAssigned,
                "No vehicle assigned — route was calculated without vehicle restrictions.",
                RoutingWarning.SeverityWarning));
            return warnings;
        }

        if (profile.HeightMeters is null)
            warnings.Add(new RoutingWarning(RoutingWarningCodes.MissingVehicleHeight,
                "Vehicle height missing — truck restrictions may be incomplete.",
                RoutingWarning.SeverityWarning));
        if (profile.WeightTons is null)
            warnings.Add(new RoutingWarning(RoutingWarningCodes.MissingVehicleWeight,
                "Vehicle weight missing — weight-restricted roads may not be avoided.",
                RoutingWarning.SeverityWarning));
        if (profile.WidthMeters is null)
            warnings.Add(new RoutingWarning(RoutingWarningCodes.MissingVehicleWidth,
                "Vehicle width missing — narrow-road restrictions may be incomplete.",
                RoutingWarning.SeverityInfo));
        if (profile.LengthMeters is null)
            warnings.Add(new RoutingWarning(RoutingWarningCodes.MissingVehicleLength,
                "Vehicle length missing — turn/length restrictions may be incomplete.",
                RoutingWarning.SeverityInfo));

        return warnings;
    }

    private static IReadOnlyList<RoutingWarning> Merge(IReadOnlyList<RoutingWarning> a, IReadOnlyList<RoutingWarning> b) =>
        a.Concat(b).GroupBy(w => w.Code).Select(g => g.First()).ToList();

    private static IReadOnlyList<RoutingWarning> Prepend(IReadOnlyList<RoutingWarning> list, RoutingWarning warning) =>
        new[] { warning }.Concat(list).ToList();

    private async Task<GeoPoint?> DestinationPoint(Guid? id, CancellationToken ct)
    {
        if (id is null) return null;
        var d = await _db.Destinations.FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == _me.CompanyId, ct);
        return d is { Latitude: not null, Longitude: not null } ? new GeoPoint(d.Latitude.Value, d.Longitude.Value) : null;
    }

    private sealed record RouteContext(
        Route Route, IReadOnlyList<RoutingStopInput> Inputs, IReadOnlyList<Guid> ExcludedStopIds,
        GeoPoint? Start, GeoPoint? End,
        VehicleRoutingProfile? VehicleProfile, IReadOnlyList<RoutingWarning> Warnings);
}
