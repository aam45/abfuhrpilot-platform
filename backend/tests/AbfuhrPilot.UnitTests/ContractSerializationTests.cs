using System.Text.Json;
using System.Text.Json.Serialization;
using AbfuhrPilot.Application.Dtos;
using AbfuhrPilot.Application.Routing;
using AbfuhrPilot.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace AbfuhrPilot.UnitTests;

/// <summary>
/// Pins the driver-facing wire format at the JSON level: enums must serialize as
/// PascalCase strings (via <see cref="JsonStringEnumConverter"/>, registered in
/// Program.cs), not as numbers. A native client parses these by name, so an
/// accidental removal of the converter would silently break the app in the field.
/// </summary>
public class ContractSerializationTests
{
    // Same options as Program.cs's AddJsonOptions — enums serialize as strings.
    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public void Driver_facing_enums_serialize_as_PascalCase_strings()
    {
        var route = new RouteDto(Guid.NewGuid(), "R1", "Route 1", new DateOnly(2026, 7, 6), Guid.NewGuid(), "Restmüll",
            Guid.NewGuid(), "Vehicle 1", Guid.NewGuid(), "Driver 1", null, null,
            RouteStatus.InProgress, null, null, null, null, 3, 1, 0);
        var routeJson = JsonSerializer.Serialize(route, Options);
        routeJson.Should().Contain("\"InProgress\"");
        routeJson.Should().NotContain("\"Status\":2"); // must not regress to a numeric enum

        var stop = new StopDto(Guid.NewGuid(), Guid.NewGuid(), 1, "Musterstraße 1", null, null,
            Guid.NewGuid(), PickupType.RegularBinPickup, null, "Kunde", null, StopStatus.Problem, null, null, null, ProblemType.RoadClosed);
        var stopJson = JsonSerializer.Serialize(stop, Options);
        stopJson.Should().Contain("\"Problem\"");
        stopJson.Should().Contain("\"RegularBinPickup\"");
        stopJson.Should().Contain("\"RoadClosed\"");

        var report = new ReportProblemRequest(ProblemType.BlockedByCar, "Auto blockiert die Zufahrt");
        var reportJson = JsonSerializer.Serialize(report, Options);
        reportJson.Should().Contain("\"BlockedByCar\"");

        var navigation = new InAppNavigationResult(Guid.NewGuid(), RoutingProviderType.GraphHopper, true,
            1200.0, 300.0, new List<GeoPoint>(), new List<NavigationLeg>(), new List<RoutingWarning>(),
            "truck", true);
        var navigationJson = JsonSerializer.Serialize(navigation, Options);
        navigationJson.Should().Contain("\"GraphHopper\"");
    }
}
