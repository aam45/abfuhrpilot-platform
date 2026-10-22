using AbfuhrPilot.Application.Common;
using AbfuhrPilot.Application.Services;
using AbfuhrPilot.Domain.Entities;
using AbfuhrPilot.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace AbfuhrPilot.UnitTests;

/// <summary>
/// Proves company B's paginated/search list endpoints never surface company A's rows,
/// even when the search term exactly matches a distinctive value that only exists under
/// company A. Complements <see cref="CrossTenantIsolationTests"/> (by-id access), because a
/// leaked list entry does not require guessing an id: it is discoverable via ordinary listing
/// or search UI. Each test also asserts the same search under company A DOES return the row,
/// so the assertion isn't vacuously true (i.e. the search predicate itself works).
/// </summary>
public class CrossTenantListLeakTests
{
    private readonly FakeCurrentUser _companyA = new();
    private readonly FakeCurrentUser _companyB = new() { CompanyId = Guid.NewGuid() };

    // ---- Route ----

    [Fact]
    public async Task Company_B_route_search_does_not_return_company_A_route_by_matching_route_number()
    {
        using var db = TestDb.Create();
        var waste = new WasteType { CompanyId = _companyA.CompanyId, Name = "Hausmüll", Code = "HM", Category = WasteCategory.HouseholdWaste };
        var route = new Route { CompanyId = _companyA.CompanyId, RouteNumber = "RA-SECRET-1", Name = "Geheimroute", WasteTypeId = waste.Id };
        db.AddRange(waste, route);
        await db.SaveChangesAsync();

        var svcA = new RouteService(db, _companyA);
        var svcB = new RouteService(db, _companyB);
        var query = new PageQuery { Search = "RA-SECRET-1" };

        var resultA = await svcA.ListAsync(query, null, null, default);
        var resultB = await svcB.ListAsync(query, null, null, default);

        resultA.Items.Should().Contain(r => r.Id == route.Id);
        resultB.Items.Should().NotContain(r => r.Id == route.Id);
        resultB.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Company_B_route_search_does_not_return_company_A_route_by_matching_name()
    {
        using var db = TestDb.Create();
        var waste = new WasteType { CompanyId = _companyA.CompanyId, Name = "Hausmüll", Code = "HM", Category = WasteCategory.HouseholdWaste };
        var route = new Route { CompanyId = _companyA.CompanyId, RouteNumber = "RA-SECRET-2", Name = "NurFuerCompanyA", WasteTypeId = waste.Id };
        db.AddRange(waste, route);
        await db.SaveChangesAsync();

        var svcA = new RouteService(db, _companyA);
        var svcB = new RouteService(db, _companyB);
        var query = new PageQuery { Search = "NurFuerCompanyA" };

        var resultA = await svcA.ListAsync(query, null, null, default);
        var resultB = await svcB.ListAsync(query, null, null, default);

        resultA.Items.Should().Contain(r => r.Id == route.Id);
        resultB.Items.Should().NotContain(r => r.Id == route.Id);
        resultB.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Company_B_route_list_without_search_does_not_return_company_A_routes()
    {
        using var db = TestDb.Create();
        var waste = new WasteType { CompanyId = _companyA.CompanyId, Name = "Hausmüll", Code = "HM", Category = WasteCategory.HouseholdWaste };
        var route = new Route { CompanyId = _companyA.CompanyId, RouteNumber = "RA-SECRET-3", Name = "R", WasteTypeId = waste.Id };
        db.AddRange(waste, route);
        await db.SaveChangesAsync();

        var svcB = new RouteService(db, _companyB);
        var resultB = await svcB.ListAsync(new PageQuery(), null, null, default);

        resultB.Items.Should().NotContain(r => r.Id == route.Id);
        resultB.TotalCount.Should().Be(0);
    }

    // ---- Order ----

    [Fact]
    public async Task Company_B_order_search_does_not_return_company_A_order_by_matching_order_number()
    {
        using var db = TestDb.Create();
        var waste = new WasteType { CompanyId = _companyA.CompanyId, Name = "Hausmüll", Code = "HM", Category = WasteCategory.HouseholdWaste };
        var order = new Order
        {
            CompanyId = _companyA.CompanyId, OrderNumber = "OA-SECRET-1", OrderType = OrderType.RegularPickup, WasteTypeId = waste.Id,
            PickupType = PickupType.ContainerPickup, CustomerName = "Kunde A", Address = "Str 1", Status = OrderStatus.New
        };
        db.AddRange(waste, order);
        await db.SaveChangesAsync();

        var svcA = new OrderService(db, _companyA);
        var svcB = new OrderService(db, _companyB);
        var query = new PageQuery { Search = "OA-SECRET-1" };

        var resultA = await svcA.ListAsync(query, null, default);
        var resultB = await svcB.ListAsync(query, null, default);

        resultA.Items.Should().Contain(o => o.Id == order.Id);
        resultB.Items.Should().NotContain(o => o.Id == order.Id);
        resultB.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Company_B_order_search_does_not_return_company_A_order_by_matching_customer_name()
    {
        using var db = TestDb.Create();
        var waste = new WasteType { CompanyId = _companyA.CompanyId, Name = "Hausmüll", Code = "HM", Category = WasteCategory.HouseholdWaste };
        var order = new Order
        {
            CompanyId = _companyA.CompanyId, OrderNumber = "OA-SECRET-2", OrderType = OrderType.RegularPickup, WasteTypeId = waste.Id,
            PickupType = PickupType.ContainerPickup, CustomerName = "GeheimerKundeA", Address = "Str 1", Status = OrderStatus.New
        };
        db.AddRange(waste, order);
        await db.SaveChangesAsync();

        var svcA = new OrderService(db, _companyA);
        var svcB = new OrderService(db, _companyB);
        var query = new PageQuery { Search = "GeheimerKundeA" };

        var resultA = await svcA.ListAsync(query, null, default);
        var resultB = await svcB.ListAsync(query, null, default);

        resultA.Items.Should().Contain(o => o.Id == order.Id);
        resultB.Items.Should().NotContain(o => o.Id == order.Id);
        resultB.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Company_B_order_list_without_search_does_not_return_company_A_orders()
    {
        using var db = TestDb.Create();
        var waste = new WasteType { CompanyId = _companyA.CompanyId, Name = "Hausmüll", Code = "HM", Category = WasteCategory.HouseholdWaste };
        var order = new Order
        {
            CompanyId = _companyA.CompanyId, OrderNumber = "OA-SECRET-3", OrderType = OrderType.RegularPickup, WasteTypeId = waste.Id,
            PickupType = PickupType.ContainerPickup, CustomerName = "Kunde A", Address = "Str 1", Status = OrderStatus.New
        };
        db.AddRange(waste, order);
        await db.SaveChangesAsync();

        var svcB = new OrderService(db, _companyB);
        var resultB = await svcB.ListAsync(new PageQuery(), null, default);

        resultB.Items.Should().NotContain(o => o.Id == order.Id);
        resultB.TotalCount.Should().Be(0);
    }

    // ---- Container ----

    [Fact]
    public async Task Company_B_container_search_does_not_return_company_A_container_by_matching_container_number()
    {
        using var db = TestDb.Create();
        var waste = new WasteType { CompanyId = _companyA.CompanyId, Name = "Hausmüll", Code = "HM", Category = WasteCategory.HouseholdWaste };
        var container = new Container { CompanyId = _companyA.CompanyId, ContainerNumber = "CA-SECRET-1", WasteTypeId = waste.Id, Size = "7m3" };
        db.AddRange(waste, container);
        await db.SaveChangesAsync();

        var svcA = new ContainerService(db, _companyA);
        var svcB = new ContainerService(db, _companyB);
        var query = new PageQuery { Search = "CA-SECRET-1" };

        var resultA = await svcA.ListAsync(query, default);
        var resultB = await svcB.ListAsync(query, default);

        resultA.Items.Should().Contain(c => c.Id == container.Id);
        resultB.Items.Should().NotContain(c => c.Id == container.Id);
        resultB.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Company_B_container_list_without_search_does_not_return_company_A_containers()
    {
        using var db = TestDb.Create();
        var waste = new WasteType { CompanyId = _companyA.CompanyId, Name = "Hausmüll", Code = "HM", Category = WasteCategory.HouseholdWaste };
        var container = new Container { CompanyId = _companyA.CompanyId, ContainerNumber = "CA-SECRET-2", WasteTypeId = waste.Id, Size = "7m3" };
        db.AddRange(waste, container);
        await db.SaveChangesAsync();

        var svcB = new ContainerService(db, _companyB);
        var resultB = await svcB.ListAsync(new PageQuery(), default);

        resultB.Items.Should().NotContain(c => c.Id == container.Id);
        resultB.TotalCount.Should().Be(0);
    }
}
