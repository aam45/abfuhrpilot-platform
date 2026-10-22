using System.Net;
using System.Text.Json;
using AbfuhrPilot.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace AbfuhrPilot.UnitTests.Integration;

/// <summary>
/// HTTP-level tests for the diagnostic-only <c>GET /health/routing</c> minimal-API
/// endpoint (Program.cs): verifies the endpoint is gated behind JWT auth + the
/// Admin/Dispatcher role check, and that an authorized caller gets back the
/// expected <c>{ status, description }</c> shape, without asserting any specific
/// GraphHopper health outcome (no real key is configured in tests).
/// </summary>
public class HealthRoutingEndpointTests : IClassFixture<WebApiFactory>
{
    private readonly WebApiFactory _factory;

    public HealthRoutingEndpointTests(WebApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Anonymous_request_is_rejected_with_401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health/routing");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Admin_request_returns_200_with_status_and_description()
    {
        var token = _factory.MintToken(Guid.NewGuid(), Guid.NewGuid(), UserRole.Admin);
        var client = _factory.CreateAuthenticatedClient(token);

        var response = await client.GetAsync("/health/routing");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.TryGetProperty("status", out var status).Should().BeTrue();
        status.GetString().Should().NotBeNullOrEmpty();
        doc.RootElement.TryGetProperty("description", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Dispatcher_request_returns_200()
    {
        var token = _factory.MintToken(Guid.NewGuid(), Guid.NewGuid(), UserRole.Dispatcher);
        var client = _factory.CreateAuthenticatedClient(token);

        var response = await client.GetAsync("/health/routing");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Driver_request_is_rejected_with_403()
    {
        var token = _factory.MintToken(Guid.NewGuid(), Guid.NewGuid(), UserRole.Driver);
        var client = _factory.CreateAuthenticatedClient(token);

        var response = await client.GetAsync("/health/routing");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
