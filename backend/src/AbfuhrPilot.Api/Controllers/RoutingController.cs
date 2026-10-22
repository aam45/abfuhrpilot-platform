using AbfuhrPilot.Api.Idempotency;
using AbfuhrPilot.Application.Dtos;
using AbfuhrPilot.Application.Routing;
using AbfuhrPilot.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AbfuhrPilot.Api.Controllers;

/// <summary>
/// Route calculation, optimization and external-navigation links. Calculate is
/// read-like but POST (it carries options). Optimize never changes the route;
/// apply-optimized-order is the explicit, separate mutation (Admin/Dispatcher).
/// </summary>
[ApiController]
[Route("api/v1/routes")]
[Authorize]
public class RoutingController : ControllerBase
{
    private readonly RoutingService _routing;
    public RoutingController(RoutingService routing) => _routing = routing;

    [HttpPost("{id:guid}/calculate"), Authorize(Roles = "Admin,Dispatcher,Viewer")]
    public Task<RouteCalculationResult> Calculate(Guid id, CancellationToken ct) => _routing.CalculateAsync(id, ct);

    [HttpPost("{id:guid}/optimize"), Authorize(Roles = "Admin,Dispatcher")]
    public Task<RouteOptimizationResult> Optimize(Guid id, CancellationToken ct) => _routing.OptimizeAsync(id, ct);

    [HttpPost("{id:guid}/apply-optimized-order"), Authorize(Roles = "Admin,Dispatcher"), Idempotent]
    public Task<List<StopDto>> ApplyOptimizedOrder(Guid id, ReorderStopsRequest req, CancellationToken ct) =>
        _routing.ApplyOptimizedOrderAsync(id, req.StopIds, ct);

    // Drivers may request links for their own route (enforced in the service).
    // Kept as the EXTERNAL-maps fallback; in-app navigation below is primary.
    [HttpGet("{id:guid}/navigation-links")]
    public Task<NavigationLinks> NavigationLinks(Guid id, CancellationToken ct) => _routing.NavigationLinksAsync(id, ct);

    /// <summary>In-app navigation data (geometry, legs, honest maneuver steps).
    /// Drivers: own route only (enforced in the service); others company-scoped.</summary>
    [HttpGet("{id:guid}/in-app-navigation")]
    public Task<InAppNavigationResult> InAppNavigation(Guid id, CancellationToken ct) =>
        _routing.GetInAppNavigationAsync(id, ct);
}
