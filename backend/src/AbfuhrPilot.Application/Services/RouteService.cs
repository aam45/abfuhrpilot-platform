using AbfuhrPilot.Application.Common;
using AbfuhrPilot.Application.Dtos;
using AbfuhrPilot.Domain.Common;
using AbfuhrPilot.Domain.Entities;
using AbfuhrPilot.Domain.Enums;
using AbfuhrPilot.Domain.Services;
using Microsoft.EntityFrameworkCore;

namespace AbfuhrPilot.Application.Services;

public class RouteService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _me;
    public RouteService(IAppDbContext db, ICurrentUser me) { _db = db; _me = me; }

    public async Task<PagedResult<RouteDto>> ListAsync(PageQuery q, DateOnly? date, RouteStatus? status, CancellationToken ct)
    {
        var query = _db.Routes.Include(r => r.Stops).Where(r => r.CompanyId == _me.CompanyId);
        if (date is not null) query = query.Where(r => r.Date == date);
        if (status is not null) query = query.Where(r => r.Status == status);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim().ToLower();
            query = query.Where(r => r.Name.ToLower().Contains(s) || r.RouteNumber.ToLower().Contains(s));
        }
        var total = await query.CountAsync(ct);
        var routes = await query.OrderByDescending(r => r.Date).ThenBy(r => r.RouteNumber)
            .Skip((q.Page - 1) * q.PageSize).Take(q.PageSize).ToListAsync(ct);
        var dtos = await ToDtosAsync(routes, ct);
        return new PagedResult<RouteDto> { Items = dtos, Page = q.Page, PageSize = q.PageSize, TotalCount = total };
    }

    public async Task<RouteDto> GetAsync(Guid id, CancellationToken ct)
    {
        var route = await FindWithStops(id, ct);
        return (await ToDtosAsync(new[] { route }, ct))[0];
    }

    public async Task<RouteDto> CreateAsync(RouteUpsert dto, CancellationToken ct)
    {
        var route = new Route { CompanyId = _me.CompanyId, Status = RouteStatus.Draft };
        await ApplyAndValidate(route, dto, ct);
        _db.Routes.Add(route);
        await _db.SaveChangesAsync(ct);
        return await GetAsync(route.Id, ct);
    }

    public async Task<RouteDto> UpdateAsync(Guid id, RouteUpsert dto, CancellationToken ct)
    {
        var route = await FindWithStops(id, ct);
        if (route.IsStarted)
            throw new DomainException("Planning data cannot be changed after the route has started.");
        await ApplyAndValidate(route, dto, ct);
        route.Touch();
        await _db.SaveChangesAsync(ct);
        return await GetAsync(route.Id, ct);
    }

    public async Task<RouteDto> AssignAsync(Guid id, AssignRouteRequest req, CancellationToken ct)
    {
        var route = await FindWithStops(id, ct);
        var wasteType = await GetWasteType(route.WasteTypeId, ct);

        if (req.VehicleId is not null)
        {
            var vehicle = await _db.Vehicles.FirstOrDefaultAsync(v => v.Id == req.VehicleId && v.CompanyId == _me.CompanyId, ct)
                ?? throw new NotFoundException("Vehicle not found.");
            WasteCompatibilityService.EnsureVehicleCanServeRoute(vehicle, wasteType); // Rule #2
            // Rule #14: special waste needs an equipped vehicle AND explicit confirmation.
            if (wasteType.RequiresSpecialVehicle && !req.ConfirmSpecialWaste)
                throw new DomainException("This waste type requires explicit confirmation before assignment.");
            route.VehicleId = vehicle.Id;
        }
        if (req.DriverId is not null)
        {
            var driver = await _db.Drivers.FirstOrDefaultAsync(d => d.Id == req.DriverId && d.CompanyId == _me.CompanyId, ct)
                ?? throw new NotFoundException("Driver not found.");
            route.DriverId = driver.Id;
        }
        if (route.Status == RouteStatus.Draft && route is { VehicleId: not null, DriverId: not null })
            route.Status = RouteStatus.Planned;
        route.Touch();
        await _db.SaveChangesAsync(ct);
        return await GetAsync(route.Id, ct);
    }

    public async Task<RouteDto> StartAsync(Guid id, CancellationToken ct)
    {
        var route = await FindWithStops(id, ct);
        // Rule #6: only the assigned driver may start (admins/dispatchers may start on their behalf).
        var actingDriverId = _me.Role == UserRole.Driver ? (_me.DriverId ?? Guid.Empty) : route.DriverId ?? Guid.Empty;
        route.Start(actingDriverId);
        await _db.SaveChangesAsync(ct);
        return await GetAsync(route.Id, ct);
    }

    public async Task<RouteDto> CompleteAsync(Guid id, CancellationToken ct)
    {
        var route = await FindWithStops(id, ct);
        // Rule #6: only the assigned driver may complete (admins/dispatchers may complete on their behalf).
        var actingDriverId = _me.Role == UserRole.Driver ? (_me.DriverId ?? Guid.Empty) : route.DriverId ?? Guid.Empty;
        route.Complete(actingDriverId);
        await _db.SaveChangesAsync(ct);
        return await GetAsync(route.Id, ct);
    }

    // ---- Stops ----
    public async Task<List<StopDto>> ListStopsAsync(Guid routeId, CancellationToken ct)
    {
        var route = await FindWithStops(routeId, ct);
        return route.Stops.OrderBy(s => s.SequenceNumber).Select(s => s.ToDto()).ToList();
    }

    /// <summary>
    /// Reorders a route's stops. <paramref name="stopIds"/> must contain exactly the
    /// route's stop ids, in the desired order. Planning-only: not allowed once the
    /// route has started (rule: planning data is frozen after start).
    /// </summary>
    public async Task<List<StopDto>> ReorderStopsAsync(Guid routeId, IReadOnlyList<Guid> stopIds, CancellationToken ct)
    {
        var route = await FindWithStops(routeId, ct);
        if (route.IsStarted)
            throw new DomainException("Stops cannot be reordered after the route has started.");

        var current = route.Stops.Select(s => s.Id).ToHashSet();
        if (stopIds.Count != current.Count || !stopIds.All(current.Contains) || stopIds.Distinct().Count() != stopIds.Count)
            throw new DomainException("The reorder request must list every stop of the route exactly once.");

        for (var i = 0; i < stopIds.Count; i++)
        {
            var stop = route.Stops.First(s => s.Id == stopIds[i]);
            stop.SequenceNumber = i + 1;
            stop.Touch();
        }
        route.Touch();
        route.Raise(new Domain.Events.OptimizedRouteAppliedDomainEvent(route.CompanyId, route.Id, stopIds.Count));
        await _db.SaveChangesAsync(ct);
        return route.Stops.OrderBy(s => s.SequenceNumber).Select(s => s.ToDto()).ToList();
    }

    public async Task<StopDto> AddStopAsync(Guid routeId, StopUpsert dto, CancellationToken ct)
    {
        var route = await FindWithStops(routeId, ct);
        var routeWaste = await GetWasteType(route.WasteTypeId, ct);
        var stopWaste = await GetWasteType(dto.WasteTypeId, ct);
        WasteCompatibilityService.EnsureStopMatchesRoute(routeWaste, stopWaste, dto.AllowMixedWaste); // Rules #3/#13

        var stop = new Stop { CompanyId = _me.CompanyId, RouteId = route.Id };
        ApplyStop(stop, dto);
        _db.Stops.Add(stop);
        await _db.SaveChangesAsync(ct);
        return stop.ToDto();
    }

    // ---- helpers ----
    private async Task ApplyAndValidate(Route route, RouteUpsert dto, CancellationToken ct)
    {
        var wasteType = await GetWasteType(dto.WasteTypeId, ct);
        route.RouteNumber = dto.RouteNumber; route.Name = dto.Name; route.Date = dto.Date;
        route.WasteTypeId = dto.WasteTypeId; route.StartDepotId = dto.StartDepotId;
        route.EndDestinationId = dto.EndDestinationId;
        route.PlannedStartTime = dto.PlannedStartTime; route.PlannedEndTime = dto.PlannedEndTime;

        if (dto.VehicleId is not null)
        {
            var vehicle = await _db.Vehicles.FirstOrDefaultAsync(v => v.Id == dto.VehicleId && v.CompanyId == _me.CompanyId, ct)
                ?? throw new NotFoundException("Vehicle not found.");
            WasteCompatibilityService.EnsureVehicleCanServeRoute(vehicle, wasteType); // Rule #2
            route.VehicleId = vehicle.Id;
        }
        route.DriverId = dto.DriverId;
    }

    private static void ApplyStop(Stop s, StopUpsert d)
    {
        s.SequenceNumber = d.SequenceNumber; s.Address = d.Address; s.Latitude = d.Latitude;
        s.Longitude = d.Longitude; s.WasteTypeId = d.WasteTypeId; s.PickupType = d.PickupType;
        s.ContainerId = d.ContainerId; s.CustomerName = d.CustomerName; s.Notes = d.Notes;
        s.PlannedArrivalTime = d.PlannedArrivalTime;
    }

    private async Task<WasteType> GetWasteType(Guid id, CancellationToken ct) =>
        await _db.WasteTypes.FirstOrDefaultAsync(w => w.Id == id && w.CompanyId == _me.CompanyId, ct)
        ?? throw new NotFoundException("Waste type not found.");

    private async Task<Route> FindWithStops(Guid id, CancellationToken ct) =>
        await _db.Routes.Include(r => r.Stops).FirstOrDefaultAsync(r => r.Id == id && r.CompanyId == _me.CompanyId, ct)
        ?? throw new NotFoundException("Route not found.");

    private async Task<List<RouteDto>> ToDtosAsync(IReadOnlyCollection<Route> routes, CancellationToken ct)
    {
        var wasteIds = routes.Select(r => r.WasteTypeId).ToHashSet();
        var vehicleIds = routes.Where(r => r.VehicleId != null).Select(r => r.VehicleId!.Value).ToHashSet();
        var driverIds = routes.Where(r => r.DriverId != null).Select(r => r.DriverId!.Value).ToHashSet();

        var wastes = await _db.WasteTypes.Where(w => wasteIds.Contains(w.Id)).ToDictionaryAsync(w => w.Id, w => w.Name, ct);
        var vehicles = await _db.Vehicles.Where(v => vehicleIds.Contains(v.Id)).ToDictionaryAsync(v => v.Id, v => v.Name, ct);
        var drivers = await _db.Drivers.Where(d => driverIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.FullName, ct);

        return routes.Select(r => r.ToDto(
            wastes.GetValueOrDefault(r.WasteTypeId),
            r.VehicleId is Guid v ? vehicles.GetValueOrDefault(v) : null,
            r.DriverId is Guid d ? drivers.GetValueOrDefault(d) : null)).ToList();
    }
}
