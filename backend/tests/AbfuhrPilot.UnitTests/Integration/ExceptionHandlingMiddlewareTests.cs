using System.Net;
using System.Text.Json;
using AbfuhrPilot.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace AbfuhrPilot.UnitTests.Integration;

/// <summary>
/// An HTTP-level test pinning
/// <see cref="AbfuhrPilot.Api.Middleware.ExceptionHandlingMiddleware"/>'s exact
/// JSON envelope (<c>{ title, status, detail, correlationId }</c>) and the
/// <c>X-Correlation-Id</c> response header, for a couple of the mapped
/// exception types. 429 (TooManyRequestsException) and 500 are covered
/// indirectly by AuthServiceTests/JwtTokenServiceTests (login lockout) and are
/// awkward to trigger cleanly at the HTTP layer here, so this class focuses on
/// the two easiest, most representative cases: 404 (NotFoundException) and
/// 403 (ForbiddenException).
/// </summary>
public class ExceptionHandlingMiddlewareTests : IClassFixture<WebApiFactory>
{
    private readonly WebApiFactory _factory;

    public ExceptionHandlingMiddlewareTests(WebApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Not_found_route_returns_404_with_frozen_error_shape()
    {
        var scenario = await _factory.SeedScenarioAsync();
        var token = _factory.MintToken(scenario.CompanyId, scenario.DriverUserId, UserRole.Driver, scenario.DriverId);
        var client = _factory.CreateAuthenticatedClient(token);

        var response = await client.GetAsync($"api/v1/routes/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Headers.TryGetValues("X-Correlation-Id", out var correlationHeader).Should().BeTrue();
        correlationHeader!.Should().ContainSingle().Which.Should().NotBeNullOrEmpty();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        root.GetProperty("title").GetString().Should().Be("Not found");
        root.GetProperty("status").GetInt32().Should().Be(404);
        root.GetProperty("detail").GetString().Should().NotBeNullOrEmpty();
        root.GetProperty("correlationId").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Driver_acting_on_a_route_not_assigned_to_them_returns_403_with_frozen_error_shape()
    {
        var scenario = await _factory.SeedScenarioAsync();
        // A driver token whose driver_id is NOT the route's assigned driver.
        var otherDriverToken = _factory.MintToken(scenario.CompanyId, Guid.NewGuid(), UserRole.Driver, Guid.NewGuid());
        var client = _factory.CreateAuthenticatedClient(otherDriverToken);

        var response = await client.GetAsync($"api/v1/routes/{scenario.RouteId}/navigation-links");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        response.Headers.TryGetValues("X-Correlation-Id", out var correlationHeader).Should().BeTrue();
        correlationHeader!.Should().ContainSingle().Which.Should().NotBeNullOrEmpty();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        root.GetProperty("title").GetString().Should().Be("Forbidden");
        root.GetProperty("status").GetInt32().Should().Be(403);
        root.GetProperty("detail").GetString().Should().NotBeNullOrEmpty();
        root.GetProperty("correlationId").GetString().Should().NotBeNullOrEmpty();
    }
}
