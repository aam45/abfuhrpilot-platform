using AbfuhrPilot.Domain.Common;
using AbfuhrPilot.Domain.Entities;
using AbfuhrPilot.Domain.Enums;
using AbfuhrPilot.Domain.Services;
using FluentAssertions;
using Xunit;

namespace AbfuhrPilot.UnitTests;

public class DomainRuleTests
{
    private static WasteType Wt(WasteCategory c, bool sep = false, bool special = false) =>
        new() { Id = Guid.NewGuid(), Name = c.ToString(), Category = c, RequiresSeparateCollection = sep, RequiresSpecialVehicle = special };

    // Rule #2
    [Fact]
    public void Vehicle_not_allowed_for_route_waste_type_is_rejected()
    {
        var vehicle = new Vehicle { Name = "Truck", AllowedWasteCategories = { WasteCategory.HouseholdWaste } };
        var route = Wt(WasteCategory.Plastic);

        var act = () => WasteCompatibilityService.EnsureVehicleCanServeRoute(vehicle, route);

        act.Should().Throw<DomainException>().WithMessage("*not allowed*");
    }

    // Rule #2 (positive)
    [Fact]
    public void Vehicle_allowed_for_route_waste_type_is_accepted()
    {
        var vehicle = new Vehicle { Name = "Truck", AllowedWasteCategories = { WasteCategory.Plastic } };
        var act = () => WasteCompatibilityService.EnsureVehicleCanServeRoute(vehicle, Wt(WasteCategory.Plastic));
        act.Should().NotThrow();
    }

    // Rule #3
    [Fact]
    public void Stop_waste_type_mismatch_is_rejected()
    {
        var route = Wt(WasteCategory.HouseholdWaste);
        var stop = Wt(WasteCategory.Metal);
        var act = () => WasteCompatibilityService.EnsureStopMatchesRoute(route, stop, allowMixed: false);
        act.Should().Throw<DomainException>().WithMessage("*does not match*");
    }

    // Rule #13
    [Fact]
    public void Separate_collection_waste_cannot_be_mixed_into_other_route()
    {
        var route = Wt(WasteCategory.HouseholdWaste);
        var stop = Wt(WasteCategory.Plastic, sep: true);
        var act = () => WasteCompatibilityService.EnsureStopMatchesRoute(route, stop, allowMixed: true);
        act.Should().Throw<DomainException>().WithMessage("*separate collection*");
    }

    // Rule #7
    [Fact]
    public void Stop_cannot_be_completed_before_route_started()
    {
        var stop = new Stop { Address = "X" };
        var act = () => stop.Complete(routeStarted: false);
        act.Should().Throw<DomainException>();
        stop.Status.Should().Be(StopStatus.Open);
    }

    // Rule #8
    [Fact]
    public void Completing_stop_sets_completion_time_and_status()
    {
        var stop = new Stop { Address = "X" };
        stop.Complete(routeStarted: true);
        stop.Status.Should().Be(StopStatus.Done);
        stop.CompletedAt.Should().NotBeNull();
    }

    // Rule #9
    [Fact]
    public void Reporting_problem_stores_problem_reason()
    {
        var stop = new Stop { Address = "X" };
        stop.ReportProblem(ProblemType.BlockedByCar);
        stop.Status.Should().Be(StopStatus.Problem);
        stop.ProblemReason.Should().Be(ProblemType.BlockedByCar);
    }

    // Rule #6
    [Fact]
    public void Only_assigned_driver_can_start_route()
    {
        var driverId = Guid.NewGuid();
        var route = new Route { RouteNumber = "R1", Name = "R", DriverId = driverId, VehicleId = Guid.NewGuid() };

        var wrong = () => route.Start(Guid.NewGuid());
        wrong.Should().Throw<DomainException>().WithMessage("*assigned driver*");

        route.Start(driverId);
        route.Status.Should().Be(RouteStatus.InProgress);
        route.ActualStartTime.Should().NotBeNull();
    }

    [Fact]
    public void Route_without_vehicle_cannot_start()
    {
        var driverId = Guid.NewGuid();
        var route = new Route { RouteNumber = "R1", Name = "R", DriverId = driverId };
        var act = () => route.Start(driverId);
        act.Should().Throw<DomainException>().WithMessage("*vehicle*");
    }

    // Rule #6
    [Fact]
    public void Only_assigned_driver_can_complete_route()
    {
        var driverId = Guid.NewGuid();
        var route = new Route { RouteNumber = "R1", Name = "R", DriverId = driverId, VehicleId = Guid.NewGuid() };
        route.Start(driverId);

        var wrong = () => route.Complete(Guid.NewGuid());
        wrong.Should().Throw<DomainException>().WithMessage("*assigned driver*");

        route.Complete(driverId);
        route.Status.Should().Be(RouteStatus.Completed);
        route.ActualEndTime.Should().NotBeNull();
    }
}
