using System.Text.Json;
using AbfuhrPilot.Application.Common;
using AbfuhrPilot.Application.Routing;
using AbfuhrPilot.Domain.Entities;
using AbfuhrPilot.Domain.Enums;
using AbfuhrPilot.Infrastructure.Persistence;
using AbfuhrPilot.Infrastructure.Routing;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using RouteService = AbfuhrPilot.Application.Services.RouteService;
using RoutingService = AbfuhrPilot.Application.Services.RoutingService;

namespace AbfuhrPilot.UnitTests;

public class InAppNavigationTests
{
    private readonly FakeCurrentUser _me = new();

    private RoutingService Build(AppDbContext db, FakeCurrentUser? me = null)
    {
        var user = me ?? _me;
        var local = new LocalFallbackProvider();
        return new RoutingService(db, user,
            new RouteService(db, user),
            new AbfuhrPilot.Application.Contracts.FleetReadService(db, user),
            new AbfuhrPilot.Application.Contracts.WasteCompatibilityQueryService(db, user),
            new List<IRoutingProvider> { local },
            new List<IRouteOptimizationProvider> { local },
            new List<INavigationProvider> { local },
            new NavigationLinkBuilder(),
            new RoutingOptions { Provider = "GraphHopper" },
            NullLogger<RoutingService>.Instance);
    }

    private async Task<Route> SeedRoute(AppDbContext db, Guid? driverId = null)
    {
        var waste = new WasteType { CompanyId = _me.CompanyId, Name = "Hausmüll", Code = "HM", Category = WasteCategory.HouseholdWaste };
        var route = new Route { CompanyId = _me.CompanyId, RouteNumber = "R1", Name = "Tour", WasteTypeId = waste.Id,
            Status = RouteStatus.Planned, DriverId = driverId };
        for (var i = 0; i < 3; i++)
            route.Stops.Add(new Stop { CompanyId = _me.CompanyId, RouteId = route.Id, SequenceNumber = i + 1,
                Address = $"Straße {i + 1}", WasteTypeId = waste.Id, Latitude = 52.5 + i * 0.01, Longitude = 13.4 + i * 0.01 });
        db.AddRange(waste, route);
        await db.SaveChangesAsync();
        return route;
    }

    [Fact]
    public async Task Driver_can_access_navigation_for_their_own_route()
    {
        using var db = TestDb.Create();
        var driverId = Guid.NewGuid();
        var route = await SeedRoute(db, driverId);
        var driver = new FakeCurrentUser { Role = UserRole.Driver, DriverId = driverId, CompanyId = _me.CompanyId };

        var nav = await Build(db, driver).GetInAppNavigationAsync(route.Id, default);

        nav.RouteId.Should().Be(route.Id);
        nav.Geometry.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Another_driver_cannot_access_that_route()
    {
        using var db = TestDb.Create();
        var route = await SeedRoute(db, Guid.NewGuid());
        var otherDriver = new FakeCurrentUser { Role = UserRole.Driver, DriverId = Guid.NewGuid(), CompanyId = _me.CompanyId };

        var act = async () => await Build(db, otherDriver).GetInAppNavigationAsync(route.Id, default);
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Admin_and_dispatcher_access_company_route_navigation()
    {
        using var db = TestDb.Create();
        var route = await SeedRoute(db, Guid.NewGuid());
        var dispatcher = new FakeCurrentUser { Role = UserRole.Dispatcher, CompanyId = _me.CompanyId };

        (await Build(db).GetInAppNavigationAsync(route.Id, default)).RouteId.Should().Be(route.Id);          // admin
        (await Build(db, dispatcher).GetInAppNavigationAsync(route.Id, default)).RouteId.Should().Be(route.Id);
    }

    [Fact]
    public async Task Local_fallback_returns_stop_to_stop_legs_with_EMPTY_steps_and_honest_warnings()
    {
        using var db = TestDb.Create();
        var route = await SeedRoute(db);

        var nav = await Build(db).GetInAppNavigationAsync(route.Id, default);

        nav.Provider.Should().Be(RoutingProviderType.LocalFallback);
        nav.Legs.Should().HaveCount(2); // 3 stops → 2 legs (no depot/end configured)
        nav.Legs.Should().OnlyContain(l => l.Steps.Count == 0);        // NEVER fake maneuvers
        nav.Legs[0].FromLabel.Should().Be("Straße 1");
        nav.Legs[0].ToLabel.Should().Be("Straße 2");
        var codes = nav.Warnings.Select(w => w.Code).ToList();
        codes.Should().Contain("TURN_BY_TURN_STEPS_NOT_AVAILABLE");
        codes.Should().Contain("TRUCK_RESTRICTIONS_NOT_ENFORCED");     // truck honesty carries over
        nav.Warnings.First(w => w.Code == "TURN_BY_TURN_STEPS_NOT_AVAILABLE").Message
            .Should().Contain("Keine echten Abbiegehinweise");
        nav.TruckRestrictionsApplied.Should().BeFalse();
        nav.TotalDistanceMeters.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GraphHopper_instructions_parse_into_neutral_steps()
    {
        var json = JsonDocument.Parse("""
        {
          "distance": 1200, "time": 180000,
          "instructions": [
            { "text": "Weiter auf Hauptstraße", "sign": 0, "street_name": "Hauptstraße", "distance": 300, "time": 40000, "interval": [0, 3] },
            { "text": "Rechts abbiegen auf Marktplatz", "sign": 2, "street_name": "Marktplatz", "distance": 180, "time": 30000, "interval": [3, 6] },
            { "text": "Ziel erreicht", "sign": 4, "distance": 0, "time": 0, "interval": [6, 6] }
          ]
        }
        """);
        var geometry = Enumerable.Range(0, 7).Select(i => new GeoPoint(52.5 + i * 0.001, 13.4 + i * 0.001)).ToList();

        var steps = GraphHopperProvider.ParseInstructions(json.RootElement, geometry);

        steps.Should().HaveCount(3);
        steps[0].ManeuverType.Should().Be("continue");
        steps[0].RoadName.Should().Be("Hauptstraße");
        steps[1].ManeuverType.Should().Be("turn");
        steps[1].Modifier.Should().Be("right");
        steps[1].Instruction.Should().Contain("Rechts abbiegen");
        steps[1].Latitude.Should().BeApproximately(52.503, 0.0001); // interval[0]=3
        steps[2].ManeuverType.Should().Be("arrive");
        steps.Select(s => s.SequenceNumber).Should().Equal(1, 2, 3);
    }

    [Fact]
    public void GraphHopper_missing_instructions_produce_no_steps()
    {
        var json = JsonDocument.Parse("""{ "distance": 100, "time": 1000 }""");
        GraphHopperProvider.ParseInstructions(json.RootElement, Array.Empty<GeoPoint>())
            .Should().BeEmpty(); // → caller adds TURN_BY_TURN_STEPS_NOT_AVAILABLE
    }
}
