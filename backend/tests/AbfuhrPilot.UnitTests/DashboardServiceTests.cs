using AbfuhrPilot.Application.Services;
using AbfuhrPilot.Domain.Entities;
using AbfuhrPilot.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace AbfuhrPilot.UnitTests;

public class DashboardServiceTests
{
    private readonly FakeCurrentUser _me = new();

    [Fact]
    public async Task Operations_stop_counts_are_scoped_to_todays_routes_only()
    {
        using var db = TestDb.Create();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var yesterday = today.AddDays(-1);
        var waste = new WasteType { CompanyId = _me.CompanyId, Name = "Hausmüll", Code = "HM", Category = WasteCategory.HouseholdWaste };

        var routeToday = new Route { CompanyId = _me.CompanyId, RouteNumber = "RT", Name = "R", WasteTypeId = waste.Id, Date = today };
        var routeYesterday = new Route { CompanyId = _me.CompanyId, RouteNumber = "RY", Name = "R", WasteTypeId = waste.Id, Date = yesterday };

        var stopsToday = new[]
        {
            new Stop { CompanyId = _me.CompanyId, RouteId = routeToday.Id, SequenceNumber = 1, Address = "A1", WasteTypeId = waste.Id, Status = StopStatus.Done },
            new Stop { CompanyId = _me.CompanyId, RouteId = routeToday.Id, SequenceNumber = 2, Address = "A2", WasteTypeId = waste.Id, Status = StopStatus.Open },
            new Stop { CompanyId = _me.CompanyId, RouteId = routeToday.Id, SequenceNumber = 3, Address = "A3", WasteTypeId = waste.Id, Status = StopStatus.Problem },
            // Neither counted in the three status buckets, but they MUST widen the honest day total.
            new Stop { CompanyId = _me.CompanyId, RouteId = routeToday.Id, SequenceNumber = 4, Address = "A4", WasteTypeId = waste.Id, Status = StopStatus.InProgress },
            new Stop { CompanyId = _me.CompanyId, RouteId = routeToday.Id, SequenceNumber = 5, Address = "A5", WasteTypeId = waste.Id, Status = StopStatus.Skipped },
        };
        var stopsYesterday = new[]
        {
            new Stop { CompanyId = _me.CompanyId, RouteId = routeYesterday.Id, SequenceNumber = 1, Address = "B1", WasteTypeId = waste.Id, Status = StopStatus.Done },
            new Stop { CompanyId = _me.CompanyId, RouteId = routeYesterday.Id, SequenceNumber = 2, Address = "B2", WasteTypeId = waste.Id, Status = StopStatus.Open },
            new Stop { CompanyId = _me.CompanyId, RouteId = routeYesterday.Id, SequenceNumber = 3, Address = "B3", WasteTypeId = waste.Id, Status = StopStatus.Problem },
        };

        db.AddRange(waste, routeToday, routeYesterday);
        db.Stops.AddRange(stopsToday);
        db.Stops.AddRange(stopsYesterday);
        await db.SaveChangesAsync();

        var svc = new DashboardService(db, _me, new RouteService(db, _me));
        var dto = await svc.GetOperationsAsync(default);

        dto.CompletedStops.Should().Be(1);
        dto.OpenStops.Should().Be(1);
        // Deliberate asymmetry: problemStops counts UNRESOLVED problems from ANY date
        // (today's + yesterday's), because a yesterday problem still needs dispatcher action.
        dto.ProblemStops.Should().Be(2);
        // All 5 of today's stops (incl. InProgress + Skipped), none of yesterday's.
        dto.TotalStops.Should().Be(5);
        // Per-route badge stays route-scoped: today's route has exactly its own 1 problem stop.
        dto.TodaysRoutes.Should().ContainSingle().Which.ProblemCount.Should().Be(1);
    }

    [Fact]
    public async Task Operations_problemStops_includes_unresolved_problems_from_any_date()
    {
        using var db = TestDb.Create();
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);
        var waste = new WasteType { CompanyId = _me.CompanyId, Name = "Hausmüll", Code = "HM", Category = WasteCategory.HouseholdWaste };
        var routeYesterday = new Route { CompanyId = _me.CompanyId, RouteNumber = "RY", Name = "R", WasteTypeId = waste.Id, Date = yesterday };
        db.AddRange(waste, routeYesterday);
        db.Stops.AddRange(
            new Stop { CompanyId = _me.CompanyId, RouteId = routeYesterday.Id, SequenceNumber = 1, Address = "B1", WasteTypeId = waste.Id, Status = StopStatus.Problem },
            new Stop { CompanyId = _me.CompanyId, RouteId = routeYesterday.Id, SequenceNumber = 2, Address = "B2", WasteTypeId = waste.Id, Status = StopStatus.Done },
            new Stop { CompanyId = _me.CompanyId, RouteId = routeYesterday.Id, SequenceNumber = 3, Address = "B3", WasteTypeId = waste.Id, Status = StopStatus.Open });
        // Another company's Problem stop must never be counted (raw CompanyId filter, no route join).
        var otherCompany = Guid.NewGuid();
        var otherRoute = new Route { CompanyId = otherCompany, RouteNumber = "RX", Name = "X", WasteTypeId = waste.Id, Date = yesterday };
        db.AddRange(otherRoute);
        db.Stops.Add(new Stop { CompanyId = otherCompany, RouteId = otherRoute.Id, SequenceNumber = 1, Address = "X1", WasteTypeId = waste.Id, Status = StopStatus.Problem });
        await db.SaveChangesAsync();

        var svc = new DashboardService(db, _me, new RouteService(db, _me));
        var dto = await svc.GetOperationsAsync(default);

        // The yesterday problem is still visible until resolved — it must not expire at midnight.
        dto.ProblemStops.Should().Be(1);
        // ...while the day-goal counts stay strictly today-scoped: nothing from yesterday leaks in.
        dto.CompletedStops.Should().Be(0);
        dto.OpenStops.Should().Be(0);
        dto.TotalStops.Should().Be(0);
    }
}
