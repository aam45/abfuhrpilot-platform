using AbfuhrPilot.Application.Dtos;
using AbfuhrPilot.Application.Services;
using AbfuhrPilot.Domain.Common;
using AbfuhrPilot.Domain.Entities;
using AbfuhrPilot.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace AbfuhrPilot.UnitTests;

public class ServiceRuleTests
{
    private readonly FakeCurrentUser _me = new();

    // Rule #5
    [Fact]
    public async Task Container_number_must_be_unique_per_company()
    {
        using var db = TestDb.Create();
        var svc = new ContainerService(db, _me);
        var wasteTypeId = Guid.NewGuid();

        await svc.CreateAsync(new ContainerUpsert("C-1", null, null, null, wasteTypeId, "7m3"), default);
        var act = async () => await svc.CreateAsync(new ContainerUpsert("C-1", null, null, null, wasteTypeId, "10m3"), default);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*already in use*");
    }

    // Rule #2 via RouteService.AssignAsync
    [Fact]
    public async Task Assigning_incompatible_vehicle_to_route_is_rejected()
    {
        using var db = TestDb.Create();
        var waste = new WasteType { CompanyId = _me.CompanyId, Name = "Kunststoff", Code = "KS", Category = WasteCategory.Plastic };
        var vehicle = new Vehicle { CompanyId = _me.CompanyId, Name = "Hausmülltruck", LicensePlate = "X",
            VehicleType = VehicleType.HouseholdWasteTruck, AllowedWasteCategories = { WasteCategory.HouseholdWaste } };
        var route = new Route { CompanyId = _me.CompanyId, RouteNumber = "R1", Name = "R", WasteTypeId = waste.Id,
            Date = DateOnly.FromDateTime(DateTime.UtcNow) };
        db.AddRange(waste, vehicle, route);
        await db.SaveChangesAsync();

        var svc = new RouteService(db, _me);
        var act = async () => await svc.AssignAsync(route.Id, new AssignRouteRequest(null, vehicle.Id), default);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*not allowed*");
    }

    // Rule #14
    [Fact]
    public async Task Special_waste_requires_explicit_confirmation()
    {
        using var db = TestDb.Create();
        var waste = new WasteType { CompanyId = _me.CompanyId, Name = "Sonderabfall", Code = "SO",
            Category = WasteCategory.SpecialWaste, RequiresSpecialVehicle = true };
        var vehicle = new Vehicle { CompanyId = _me.CompanyId, Name = "Spezial", LicensePlate = "S",
            VehicleType = VehicleType.MixedUtilityTruck, AllowedWasteCategories = { WasteCategory.SpecialWaste } };
        var route = new Route { CompanyId = _me.CompanyId, RouteNumber = "R2", Name = "R", WasteTypeId = waste.Id };
        db.AddRange(waste, vehicle, route);
        await db.SaveChangesAsync();

        var svc = new RouteService(db, _me);
        var withoutConfirm = async () => await svc.AssignAsync(route.Id, new AssignRouteRequest(null, vehicle.Id, ConfirmSpecialWaste: false), default);
        await withoutConfirm.Should().ThrowAsync<DomainException>().WithMessage("*confirmation*");

        var withConfirm = await svc.AssignAsync(route.Id, new AssignRouteRequest(null, vehicle.Id, ConfirmSpecialWaste: true), default);
        withConfirm.VehicleId.Should().Be(vehicle.Id);
    }

    // Rule #12
    [Fact]
    public async Task Driver_cannot_complete_stop_on_route_not_assigned_to_them()
    {
        using var db = TestDb.Create();
        var driverMe = new FakeCurrentUser { Role = UserRole.Driver, DriverId = Guid.NewGuid(), CompanyId = _me.CompanyId };
        var route = new Route { CompanyId = _me.CompanyId, RouteNumber = "R3", Name = "R",
            DriverId = Guid.NewGuid(), VehicleId = Guid.NewGuid(), Status = RouteStatus.InProgress };
        var stop = new Stop { CompanyId = _me.CompanyId, RouteId = route.Id, Address = "A", SequenceNumber = 1 };
        db.AddRange(route, stop);
        await db.SaveChangesAsync();

        var svc = new StopService(db, driverMe);
        var act = async () => await svc.CompleteAsync(stop.Id, default);
        await act.Should().ThrowAsync<Application.Common.ForbiddenException>();
    }

    // Phase 3 — reorder
    private async Task<(RouteService svc, Route route, List<Stop> stops)> SeedRouteWithStops(
        AbfuhrPilot.Infrastructure.Persistence.AppDbContext db, RouteStatus status = RouteStatus.Planned)
    {
        var waste = new WasteType { CompanyId = _me.CompanyId, Name = "Hausmüll", Code = "HM", Category = WasteCategory.HouseholdWaste };
        var route = new Route { CompanyId = _me.CompanyId, RouteNumber = "R9", Name = "R", WasteTypeId = waste.Id, Status = status };
        var stops = Enumerable.Range(1, 3).Select(i => new Stop
        { CompanyId = _me.CompanyId, RouteId = route.Id, SequenceNumber = i, Address = $"Str {i}", WasteTypeId = waste.Id }).ToList();
        route.Stops.AddRange(stops);
        db.AddRange(waste, route);
        await db.SaveChangesAsync();
        return (new RouteService(db, _me), route, stops);
    }

    [Fact]
    public async Task Reorder_stops_reassigns_sequence_numbers_in_given_order()
    {
        using var db = TestDb.Create();
        var (svc, route, stops) = await SeedRouteWithStops(db);
        var reversed = stops.Select(s => s.Id).Reverse().ToList();

        var result = await svc.ReorderStopsAsync(route.Id, reversed, default);

        result.Select(s => s.Id).Should().Equal(reversed);
        result.Select(s => s.SequenceNumber).Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task Reorder_rejects_list_that_does_not_match_route_stops()
    {
        using var db = TestDb.Create();
        var (svc, route, stops) = await SeedRouteWithStops(db);
        var bad = new List<Guid> { stops[0].Id, stops[1].Id }; // missing one

        var act = async () => await svc.ReorderStopsAsync(route.Id, bad, default);
        await act.Should().ThrowAsync<DomainException>().WithMessage("*every stop*");
    }

    [Fact]
    public async Task Reorder_not_allowed_after_route_started()
    {
        using var db = TestDb.Create();
        var (svc, route, stops) = await SeedRouteWithStops(db, RouteStatus.InProgress);
        var ids = stops.Select(s => s.Id).Reverse().ToList();

        var act = async () => await svc.ReorderStopsAsync(route.Id, ids, default);
        await act.Should().ThrowAsync<DomainException>().WithMessage("*after the route has started*");
    }

    // Rule #6 — dispatcher/admin may start a route on behalf of its assigned driver.
    [Fact]
    public async Task Dispatcher_can_start_route_on_behalf_of_assigned_driver()
    {
        using var db = TestDb.Create();
        var route = new Route
        {
            CompanyId = _me.CompanyId, RouteNumber = "R10", Name = "R",
            DriverId = Guid.NewGuid(), VehicleId = Guid.NewGuid(), Status = RouteStatus.Planned
        };
        db.Add(route);
        await db.SaveChangesAsync();

        var svc = new RouteService(db, _me); // _me defaults to Role = UserRole.Admin
        var result = await svc.StartAsync(route.Id, default);

        result.Status.Should().Be(RouteStatus.InProgress);
    }

    // Rule #6 — dispatcher/admin may complete a route on behalf of its assigned driver.
    [Fact]
    public async Task Dispatcher_can_complete_route_on_behalf_of_assigned_driver()
    {
        using var db = TestDb.Create();
        var route = new Route
        {
            CompanyId = _me.CompanyId, RouteNumber = "R11", Name = "R",
            DriverId = Guid.NewGuid(), VehicleId = Guid.NewGuid(), Status = RouteStatus.InProgress
        };
        db.Add(route);
        await db.SaveChangesAsync();

        var svc = new RouteService(db, _me); // _me defaults to Role = UserRole.Admin
        var result = await svc.CompleteAsync(route.Id, default);

        result.Status.Should().Be(RouteStatus.Completed);
    }

    // Rule #6 — an admin/dispatcher must not be able to start an unassigned route
    // (actingDriverId must not silently fall back to Guid.Empty and succeed).
    [Fact]
    public async Task Dispatcher_cannot_start_route_with_no_assigned_driver()
    {
        using var db = TestDb.Create();
        var route = new Route
        {
            CompanyId = _me.CompanyId, RouteNumber = "R12", Name = "R",
            DriverId = null, VehicleId = Guid.NewGuid(), Status = RouteStatus.Planned
        };
        db.Add(route);
        await db.SaveChangesAsync();

        var svc = new RouteService(db, _me); // _me defaults to Role = UserRole.Admin
        var act = async () => await svc.StartAsync(route.Id, default);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*assigned driver can start*");
    }

    // Rule #6 — an admin/dispatcher must not be able to complete an unassigned route.
    [Fact]
    public async Task Dispatcher_cannot_complete_route_with_no_assigned_driver()
    {
        using var db = TestDb.Create();
        var route = new Route
        {
            CompanyId = _me.CompanyId, RouteNumber = "R13", Name = "R",
            DriverId = null, VehicleId = Guid.NewGuid(), Status = RouteStatus.InProgress
        };
        db.Add(route);
        await db.SaveChangesAsync();

        var svc = new RouteService(db, _me); // _me defaults to Role = UserRole.Admin
        var act = async () => await svc.CompleteAsync(route.Id, default);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*assigned driver can complete*");
    }

    // Rule #6 — a driver cannot start a route assigned to a different driver.
    [Fact]
    public async Task Driver_cannot_start_route_not_assigned_to_them()
    {
        using var db = TestDb.Create();
        var driverMe = new FakeCurrentUser { Role = UserRole.Driver, DriverId = Guid.NewGuid(), CompanyId = _me.CompanyId };
        var route = new Route
        {
            CompanyId = _me.CompanyId, RouteNumber = "R14", Name = "R",
            DriverId = Guid.NewGuid(), VehicleId = Guid.NewGuid(), Status = RouteStatus.Planned
        };
        db.Add(route);
        await db.SaveChangesAsync();

        var svc = new RouteService(db, driverMe);
        var act = async () => await svc.StartAsync(route.Id, default);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*assigned driver can start*");
    }

    // Rule #6 — a driver cannot complete a route assigned to a different driver.
    [Fact]
    public async Task Driver_cannot_complete_route_not_assigned_to_them()
    {
        using var db = TestDb.Create();
        var driverMe = new FakeCurrentUser { Role = UserRole.Driver, DriverId = Guid.NewGuid(), CompanyId = _me.CompanyId };
        var route = new Route
        {
            CompanyId = _me.CompanyId, RouteNumber = "R15", Name = "R",
            DriverId = Guid.NewGuid(), VehicleId = Guid.NewGuid(), Status = RouteStatus.InProgress
        };
        db.Add(route);
        await db.SaveChangesAsync();

        var svc = new RouteService(db, driverMe);
        var act = async () => await svc.CompleteAsync(route.Id, default);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*assigned driver can complete*");
    }
}
