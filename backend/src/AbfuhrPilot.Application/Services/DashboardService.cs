using AbfuhrPilot.Application.Common;
using AbfuhrPilot.Application.Dtos;
using AbfuhrPilot.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AbfuhrPilot.Application.Services;

public class DashboardService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _me;
    private readonly RouteService _routes;
    public DashboardService(IAppDbContext db, ICurrentUser me, RouteService routes)
    {
        _db = db; _me = me; _routes = routes;
    }

    public async Task<OperationsDashboardDto> GetOperationsAsync(CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var cid = _me.CompanyId;

        var routesToday = await _db.Routes.CountAsync(r => r.CompanyId == cid && r.Date == today, ct);
        var activeRoutes = await _db.Routes.CountAsync(r => r.CompanyId == cid && r.Status == RouteStatus.InProgress, ct);

        // Completed/open/total stop counts are today-scoped (route.Date == today), matching the
        // "Stopps heute" day-goal framing on the Leitstand. Stop has no Route navigation, so we
        // correlate on RouteId.
        var stopsToday = _db.Stops.Where(s => s.CompanyId == cid
            && _db.Routes.Any(r => r.Id == s.RouteId && r.CompanyId == cid && r.Date == today));
        var completedStops = await stopsToday.CountAsync(s => s.Status == StopStatus.Done, ct);
        var openStops = await stopsToday.CountAsync(s => s.Status == StopStatus.Open, ct);
        // DELIBERATE ASYMMETRY (product decision): problemStops is NOT today-scoped.
        // An unresolved Problem stop does not expire at midnight — it still needs dispatcher
        // action, and the Leitstand "Problemstellen" banner/tile is the only surface that puts
        // exceptions first. Do not "fix" this back to the stopsToday query; the day-goal counts
        // above stay today-scoped on purpose while this one is company-wide until resolved
        // (Stop.Start/Complete/Skip all transition out of StopStatus.Problem).
        var problemStops = await _db.Stops.CountAsync(
            s => s.CompanyId == cid && s.Status == StopStatus.Problem, ct);
        // Honest day-goal denominator: ALL of today's stops regardless of status, so
        // InProgress/Skipped/Cancelled stops can never inflate the completion reading.
        var totalStops = await stopsToday.CountAsync(ct);
        var openOrders = await _db.Orders.CountAsync(o => o.CompanyId == cid
            && (o.Status == OrderStatus.New || o.Status == OrderStatus.Scheduled), ct);
        var vehiclesInUse = await _db.Routes.Where(r => r.CompanyId == cid && r.Status == RouteStatus.InProgress
            && r.VehicleId != null).Select(r => r.VehicleId).Distinct().CountAsync(ct);

        var todays = await _routes.ListAsync(new PageQuery { PageSize = 50 }, today, null, ct);

        return new OperationsDashboardDto(routesToday, activeRoutes, completedStops, openStops,
            problemStops, totalStops, openOrders, vehiclesInUse, todays.Items.ToList());
    }

    public async Task<DriverDashboardDto> GetDriverAsync(CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var driverId = _me.DriverId;
        if (driverId is null)
            return new DriverDashboardDto(null, null, new(), 0, 0);

        var route = await _db.Routes.Include(r => r.Stops)
            .Where(r => r.CompanyId == _me.CompanyId && r.DriverId == driverId
                && (r.Date == today || r.Status == RouteStatus.InProgress))
            .OrderByDescending(r => r.Status == RouteStatus.InProgress).ThenBy(r => r.Date)
            .FirstOrDefaultAsync(ct);

        if (route is null)
            return new DriverDashboardDto(null, null, new(), 0, 0);

        var routeDto = (await _routes.GetAsync(route.Id, ct));
        var stops = route.Stops.OrderBy(s => s.SequenceNumber).Select(s => s.ToDto()).ToList();
        var next = stops.FirstOrDefault(s => s.Status is StopStatus.Open or StopStatus.InProgress);
        var done = stops.Count(s => s.Status == StopStatus.Done);
        return new DriverDashboardDto(routeDto, next, stops, done, stops.Count);
    }
}
