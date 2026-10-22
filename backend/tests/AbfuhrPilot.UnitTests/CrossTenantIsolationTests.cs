using AbfuhrPilot.Application.Common;
using AbfuhrPilot.Application.Dtos;
using AbfuhrPilot.Application.Services;
using AbfuhrPilot.Domain.Entities;
using AbfuhrPilot.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace AbfuhrPilot.UnitTests;

/// <summary>
/// Proves company B cannot read or mutate company A's Routes/Orders/Stops/Containers by id.
/// All four Application services scope find/list queries by CompanyId == _me.CompanyId and
/// throw NotFoundException (never a different exception, and never success) when the id belongs
/// to a different company. This intentionally masks cross-tenant existence (404, not 403).
/// </summary>
public class CrossTenantIsolationTests
{
    private readonly FakeCurrentUser _companyA = new();
    private readonly FakeCurrentUser _companyB = new() { CompanyId = Guid.NewGuid() };

    // ---- Route ----

    [Fact]
    public async Task Company_B_cannot_read_company_A_route_by_id()
    {
        using var db = TestDb.Create();
        var waste = new WasteType { CompanyId = _companyA.CompanyId, Name = "Hausmüll", Code = "HM", Category = WasteCategory.HouseholdWaste };
        var route = new Route { CompanyId = _companyA.CompanyId, RouteNumber = "RA1", Name = "R", WasteTypeId = waste.Id };
        db.AddRange(waste, route);
        await db.SaveChangesAsync();

        var svcB = new RouteService(db, _companyB);
        var act = async () => await svcB.GetAsync(route.Id, default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Company_B_cannot_update_company_A_route_by_id()
    {
        using var db = TestDb.Create();
        var waste = new WasteType { CompanyId = _companyA.CompanyId, Name = "Hausmüll", Code = "HM", Category = WasteCategory.HouseholdWaste };
        var route = new Route { CompanyId = _companyA.CompanyId, RouteNumber = "RA2", Name = "R", WasteTypeId = waste.Id };
        db.AddRange(waste, route);
        await db.SaveChangesAsync();

        var svcB = new RouteService(db, _companyB);
        var upsert = new RouteUpsert("RA2", "Renamed", DateOnly.FromDateTime(DateTime.UtcNow), waste.Id,
            null, null, null, null, null, null);
        var act = async () => await svcB.UpdateAsync(route.Id, upsert, default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ---- Order ----

    [Fact]
    public async Task Company_B_cannot_read_company_A_order_by_id()
    {
        using var db = TestDb.Create();
        var waste = new WasteType { CompanyId = _companyA.CompanyId, Name = "Hausmüll", Code = "HM", Category = WasteCategory.HouseholdWaste };
        var order = new Order
        {
            CompanyId = _companyA.CompanyId, OrderNumber = "OA1", OrderType = OrderType.RegularPickup, WasteTypeId = waste.Id,
            PickupType = PickupType.ContainerPickup, CustomerName = "Kunde A", Address = "Str 1", Status = OrderStatus.New
        };
        db.AddRange(waste, order);
        await db.SaveChangesAsync();

        var svcB = new OrderService(db, _companyB);
        var act = async () => await svcB.GetAsync(order.Id, default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Company_B_cannot_update_company_A_order_by_id()
    {
        using var db = TestDb.Create();
        var waste = new WasteType { CompanyId = _companyA.CompanyId, Name = "Hausmüll", Code = "HM", Category = WasteCategory.HouseholdWaste };
        var order = new Order
        {
            CompanyId = _companyA.CompanyId, OrderNumber = "OA2", OrderType = OrderType.RegularPickup, WasteTypeId = waste.Id,
            PickupType = PickupType.ContainerPickup, CustomerName = "Kunde A", Address = "Str 1", Status = OrderStatus.New
        };
        db.AddRange(waste, order);
        await db.SaveChangesAsync();

        var svcB = new OrderService(db, _companyB);
        var upsert = new OrderUpsert("OA2", OrderType.RegularPickup, waste.Id, PickupType.ContainerPickup, null, "Kunde B",
            null, "Str 2", null, null, null, null);
        var act = async () => await svcB.UpdateAsync(order.Id, upsert, default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ---- Stop ----

    [Fact]
    public async Task Company_B_cannot_update_company_A_stop_by_id()
    {
        using var db = TestDb.Create();
        var waste = new WasteType { CompanyId = _companyA.CompanyId, Name = "Hausmüll", Code = "HM", Category = WasteCategory.HouseholdWaste };
        var route = new Route { CompanyId = _companyA.CompanyId, RouteNumber = "RA3", Name = "R", WasteTypeId = waste.Id };
        var stop = new Stop { CompanyId = _companyA.CompanyId, RouteId = route.Id, Address = "A", SequenceNumber = 1, WasteTypeId = waste.Id };
        db.AddRange(waste, route, stop);
        await db.SaveChangesAsync();

        var svcB = new StopService(db, _companyB); // _companyB defaults to Role = UserRole.Admin
        var upsert = new StopUpsert(1, "B", null, null, waste.Id, PickupType.ContainerPickup, null, null, null, null);
        var act = async () => await svcB.UpdateAsync(stop.Id, upsert, default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Company_B_cannot_start_company_A_stop_by_id()
    {
        using var db = TestDb.Create();
        var waste = new WasteType { CompanyId = _companyA.CompanyId, Name = "Hausmüll", Code = "HM", Category = WasteCategory.HouseholdWaste };
        var route = new Route
        {
            CompanyId = _companyA.CompanyId, RouteNumber = "RA4", Name = "R", WasteTypeId = waste.Id,
            DriverId = Guid.NewGuid(), VehicleId = Guid.NewGuid(), Status = RouteStatus.InProgress
        };
        var stop = new Stop { CompanyId = _companyA.CompanyId, RouteId = route.Id, Address = "A", SequenceNumber = 1, WasteTypeId = waste.Id };
        db.AddRange(waste, route, stop);
        await db.SaveChangesAsync();

        var svcB = new StopService(db, _companyB);
        var act = async () => await svcB.StartAsync(stop.Id, default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ---- Container ----

    [Fact]
    public async Task Company_B_cannot_read_company_A_container_by_id()
    {
        using var db = TestDb.Create();
        var waste = new WasteType { CompanyId = _companyA.CompanyId, Name = "Hausmüll", Code = "HM", Category = WasteCategory.HouseholdWaste };
        var container = new Container { CompanyId = _companyA.CompanyId, ContainerNumber = "CA1", WasteTypeId = waste.Id, Size = "7m3" };
        db.AddRange(waste, container);
        await db.SaveChangesAsync();

        var svcB = new ContainerService(db, _companyB);
        var act = async () => await svcB.GetAsync(container.Id, default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Company_B_cannot_update_company_A_container_by_id()
    {
        using var db = TestDb.Create();
        var waste = new WasteType { CompanyId = _companyA.CompanyId, Name = "Hausmüll", Code = "HM", Category = WasteCategory.HouseholdWaste };
        var container = new Container { CompanyId = _companyA.CompanyId, ContainerNumber = "CA2", WasteTypeId = waste.Id, Size = "7m3" };
        db.AddRange(waste, container);
        await db.SaveChangesAsync();

        var svcB = new ContainerService(db, _companyB);
        var upsert = new ContainerUpsert("CA2", null, null, null, waste.Id, "10m3");
        var act = async () => await svcB.UpdateAsync(container.Id, upsert, default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Company_B_cannot_delete_company_A_container_by_id()
    {
        using var db = TestDb.Create();
        var waste = new WasteType { CompanyId = _companyA.CompanyId, Name = "Hausmüll", Code = "HM", Category = WasteCategory.HouseholdWaste };
        var container = new Container { CompanyId = _companyA.CompanyId, ContainerNumber = "CA3", WasteTypeId = waste.Id, Size = "7m3" };
        db.AddRange(waste, container);
        await db.SaveChangesAsync();

        var svcB = new ContainerService(db, _companyB);
        var act = async () => await svcB.DeleteAsync(container.Id, default);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
