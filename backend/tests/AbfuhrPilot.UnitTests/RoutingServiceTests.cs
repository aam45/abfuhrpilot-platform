using AbfuhrPilot.Application.Common;
using AbfuhrPilot.Application.Routing;
using AbfuhrPilot.Application.Services;
using AbfuhrPilot.Domain.Common;
using AbfuhrPilot.Domain.Entities;
using AbfuhrPilot.Domain.Enums;
using AbfuhrPilot.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AbfuhrPilot.UnitTests;

public class RoutingServiceTests
{
    private readonly FakeCurrentUser _me = new();

    /// <summary>Optimization provider that always throws (simulates GraphHopper failure).</summary>
    private sealed class ThrowingOptimizer : IRouteOptimizationProvider
    {
        public RoutingProviderType Type => RoutingProviderType.GraphHopper;
        public bool IsConfigured => true;
        public Task<RouteOptimizationResult> OptimizeRouteAsync(RouteOptimizationRequest r, CancellationToken ct)
            => throw new HttpRequestException("boom");
    }

    /// <summary>Routing provider that records the request it received (no HTTP).</summary>
    private sealed class CapturingRouter : IRoutingProvider
    {
        public RouteCalculationRequest? LastRequest;
        public RoutingProviderType Type => RoutingProviderType.GraphHopper;
        public bool IsConfigured => true;
        public Task<RouteCalculationResult> CalculateRouteAsync(RouteCalculationRequest r, CancellationToken ct)
        {
            LastRequest = r;
            return Task.FromResult(new RouteCalculationResult(Type, true, null, 1000, 100,
                Array.Empty<RouteLeg>(), null, Array.Empty<Guid>()));
        }
    }

    private RoutingService Build(AppDbContext db, FakeCurrentUser? me = null,
        IRouteOptimizationProvider? extraOptimizer = null, IRoutingProvider? extraRouter = null,
        string provider = "GraphHopper")
    {
        var user = me ?? _me;
        var local = new LocalFallbackProvider();
        var routers = new List<IRoutingProvider> { local };
        var optimizers = new List<IRouteOptimizationProvider> { local };
        if (extraRouter is not null) routers.Add(extraRouter);
        if (extraOptimizer is not null) optimizers.Add(extraOptimizer);
        var routes = new RouteService(db, user);
        var options = new RoutingOptions { Provider = provider };
        // Module contracts: in-process implementations, exactly as in production DI.
        var fleet = new AbfuhrPilot.Application.Contracts.FleetReadService(db, user);
        var wasteCompat = new AbfuhrPilot.Application.Contracts.WasteCompatibilityQueryService(db, user);
        var navProviders = new List<INavigationProvider> { local };
        return new RoutingService(db, user, routes, fleet, wasteCompat, routers, optimizers, navProviders,
            new NavigationLinkBuilder(), options, NullLogger<RoutingService>.Instance);
    }

    private async Task<Route> SeedRoute(AppDbContext db, RouteStatus status = RouteStatus.Planned,
        WasteCategory cat = WasteCategory.HouseholdWaste, Vehicle? vehicle = null)
    {
        var waste = new WasteType { CompanyId = _me.CompanyId, Name = "W", Code = "W", Category = cat };
        var route = new Route { CompanyId = _me.CompanyId, RouteNumber = "R1", Name = "R", WasteTypeId = waste.Id,
            Status = status, VehicleId = vehicle?.Id };
        for (var i = 0; i < 3; i++)
            route.Stops.Add(new Stop { CompanyId = _me.CompanyId, RouteId = route.Id, SequenceNumber = i + 1,
                Address = $"Str {i}", WasteTypeId = waste.Id, Latitude = 52.5 + i * 0.01, Longitude = 13.4 + i * 0.01 });
        db.AddRange(waste, route);
        if (vehicle is not null) db.Add(vehicle);
        await db.SaveChangesAsync();
        return route;
    }

    [Fact]
    public async Task Missing_provider_key_returns_local_fallback_not_a_crash()
    {
        using var db = TestDb.Create();
        var route = await SeedRoute(db);
        var svc = Build(db); // no GraphHopper provider registered → falls back

        var result = await svc.CalculateAsync(route.Id, default);

        result.Provider.Should().Be(RoutingProviderType.LocalFallback);
        result.ProviderConfigured.Should().BeFalse();
        result.Warning.Should().Be("Routing provider not configured");
        result.TotalDistanceMeters.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Optimize_does_not_change_the_stored_stop_sequence()
    {
        using var db = TestDb.Create();
        var route = await SeedRoute(db);
        var svc = Build(db);

        await svc.OptimizeAsync(route.Id, default);

        var seq = await db.Stops.Where(s => s.RouteId == route.Id).OrderBy(s => s.SequenceNumber)
            .Select(s => s.SequenceNumber).ToListAsync();
        seq.Should().Equal(1, 2, 3); // unchanged
    }

    [Fact]
    public async Task Apply_optimized_order_updates_the_sequence()
    {
        using var db = TestDb.Create();
        var route = await SeedRoute(db);
        var svc = Build(db);
        var reversed = (await db.Stops.Where(s => s.RouteId == route.Id).OrderByDescending(s => s.SequenceNumber)
            .Select(s => s.Id).ToListAsync());

        var updated = await svc.ApplyOptimizedOrderAsync(route.Id, reversed, default);

        updated.Select(s => s.Id).Should().Equal(reversed);
        updated.Select(s => s.SequenceNumber).Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task Driver_cannot_optimize()
    {
        using var db = TestDb.Create();
        var route = await SeedRoute(db);
        var driver = new FakeCurrentUser { Role = UserRole.Driver, DriverId = Guid.NewGuid(), CompanyId = _me.CompanyId };
        var svc = Build(db, driver);

        var act = async () => await svc.OptimizeAsync(route.Id, default);
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Optimize_is_blocked_after_the_route_starts()
    {
        using var db = TestDb.Create();
        var route = await SeedRoute(db, RouteStatus.InProgress);
        var svc = Build(db);

        var act = async () => await svc.OptimizeAsync(route.Id, default);
        await act.Should().ThrowAsync<DomainException>().WithMessage("*nach dem Start*");
    }

    [Fact]
    public async Task Optimize_enforces_vehicle_waste_compatibility()
    {
        using var db = TestDb.Create();
        var truck = new Vehicle { CompanyId = _me.CompanyId, Name = "Hausmüll", LicensePlate = "X",
            VehicleType = VehicleType.HouseholdWasteTruck, AllowedWasteCategories = { WasteCategory.HouseholdWaste } };
        var route = await SeedRoute(db, cat: WasteCategory.Plastic, vehicle: truck); // plastic route, household-only truck
        var svc = Build(db);

        var act = async () => await svc.OptimizeAsync(route.Id, default);
        await act.Should().ThrowAsync<DomainException>().WithMessage("*not allowed*");
    }

    [Fact]
    public async Task Provider_failure_falls_back_without_corrupting_route_order()
    {
        using var db = TestDb.Create();
        var route = await SeedRoute(db);
        var svc = Build(db, extraOptimizer: new ThrowingOptimizer());

        var result = await svc.OptimizeAsync(route.Id, default);

        result.Provider.Should().Be(RoutingProviderType.LocalFallback);
        result.Warning.Should().Contain("fehlgeschlagen");
        var seq = await db.Stops.Where(s => s.RouteId == route.Id).OrderBy(s => s.SequenceNumber)
            .Select(s => s.SequenceNumber).ToListAsync();
        seq.Should().Equal(1, 2, 3); // untouched
    }

    // ---- truck-aware routing ----

    [Fact]
    public async Task Vehicle_dimensions_are_passed_to_the_routing_provider()
    {
        using var db = TestDb.Create();
        var truck = new Vehicle { CompanyId = _me.CompanyId, Name = "Presswagen", LicensePlate = "T",
            VehicleType = VehicleType.HouseholdWasteTruck,
            HeightMeters = 3.6, WidthMeters = 2.55, LengthMeters = 10.5, WeightTons = 18, MaxLoadTons = 11,
            AllowedWasteCategories = { WasteCategory.HouseholdWaste } };
        var route = await SeedRoute(db, vehicle: truck);
        var capturing = new CapturingRouter();
        var svc = Build(db, extraRouter: capturing);

        await svc.CalculateAsync(route.Id, default);

        var v = capturing.LastRequest!.Vehicle!;
        v.HeightMeters.Should().Be(3.6);
        v.WidthMeters.Should().Be(2.55);
        v.LengthMeters.Should().Be(10.5);
        v.WeightTons.Should().Be(18);
        v.MaxLoadTons.Should().Be(11);
        v.RequiresTruckRouting.Should().BeTrue();
        v.VehicleType.Should().Be("HouseholdWasteTruck");
    }

    [Fact]
    public async Task Missing_vehicle_dimensions_produce_explicit_warnings()
    {
        using var db = TestDb.Create();
        var truck = new Vehicle { CompanyId = _me.CompanyId, Name = "Alt", LicensePlate = "A",
            VehicleType = VehicleType.MixedUtilityTruck, AllowedWasteCategories = { WasteCategory.HouseholdWaste } };
        var route = await SeedRoute(db, vehicle: truck);
        var svc = Build(db);

        var result = await svc.CalculateAsync(route.Id, default);

        var codes = result.Warnings.Select(w => w.Code).ToList();
        codes.Should().Contain("MISSING_VEHICLE_HEIGHT");
        codes.Should().Contain("MISSING_VEHICLE_WEIGHT");
    }

    [Fact]
    public async Task Route_without_vehicle_returns_clear_warning()
    {
        using var db = TestDb.Create();
        var route = await SeedRoute(db); // no vehicle assigned
        var svc = Build(db);

        var result = await svc.CalculateAsync(route.Id, default);

        result.Warnings.Should().Contain(w => w.Code == "NO_VEHICLE_ASSIGNED");
    }

    [Fact]
    public async Task Local_fallback_never_claims_truck_safety()
    {
        using var db = TestDb.Create();
        var route = await SeedRoute(db);
        var svc = Build(db); // no external provider

        var result = await svc.CalculateAsync(route.Id, default);

        result.TruckRestrictionsApplied.Should().BeFalse();
        var codes = result.Warnings.Select(w => w.Code).ToList();
        codes.Should().Contain("PROVIDER_USING_LOCAL_FALLBACK");
        codes.Should().Contain("TRUCK_RESTRICTIONS_NOT_ENFORCED");
    }

    [Fact]
    public void GraphHopper_car_profile_warns_that_truck_rules_are_not_enforced()
    {
        var options = new GraphHopperOptions { Profile = "car" };
        var vehicle = new VehicleRoutingProfile { RequiresTruckRouting = true };

        var warnings = AbfuhrPilot.Infrastructure.Routing.GraphHopperProvider.BuildProfileWarnings(options, vehicle);

        var codes = warnings.Select(w => w.Code).ToList();
        codes.Should().Contain("PROVIDER_USING_CAR_FALLBACK");
        codes.Should().Contain("TRUCK_RESTRICTIONS_NOT_ENFORCED");
        warnings.First(w => w.Code == "PROVIDER_USING_CAR_FALLBACK").Message
            .Should().Contain("car profile");
    }

    [Fact]
    public void GraphHopper_truck_profile_reports_partial_support_honestly()
    {
        var options = new GraphHopperOptions { Profile = "truck" };
        var vehicle = new VehicleRoutingProfile { RequiresTruckRouting = true, HeightMeters = 3.6 };

        var warnings = AbfuhrPilot.Infrastructure.Routing.GraphHopperProvider.BuildProfileWarnings(options, vehicle);

        warnings.Select(w => w.Code).Should().Contain("TRUCK_PROFILE_NOT_FULLY_SUPPORTED");
        warnings.Select(w => w.Code).Should().NotContain("PROVIDER_USING_CAR_FALLBACK");
        options.IsTruckProfile.Should().BeTrue();
    }

    [Fact]
    public async Task Navigation_links_are_generated_for_the_route()
    {
        using var db = TestDb.Create();
        var route = await SeedRoute(db);
        var svc = Build(db);

        var links = await svc.NavigationLinksAsync(route.Id, default);

        links.GoogleMaps.Should().StartWith("https://www.google.com/maps/dir/");
        links.AppleMaps.Should().Contain("maps.apple.com");
        links.OpenStreetMap.Should().Contain("openstreetmap.org");
    }
}
