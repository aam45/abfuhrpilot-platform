using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AbfuhrPilot.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace AbfuhrPilot.UnitTests.Integration;

/// <summary>
/// An HTTP-level regression
/// test asserting that every <c>Idempotent(Required = true)</c> route rejects
/// a request that omits the <c>Idempotency-Key</c> header with 400 and the
/// frozen error shape (see <c>IdempotencyFilter.Error(...)</c>).
/// </summary>
public class RequiredIdempotencyKeyTests : IClassFixture<WebApiFactory>
{
    private readonly WebApiFactory _factory;

    public RequiredIdempotencyKeyTests(WebApiFactory factory) => _factory = factory;

    public static IEnumerable<object[]> RequiredRoutes()
    {
        yield return new object[] { "routes/{routeId}/start", false };
        yield return new object[] { "routes/{routeId}/complete", false };
        yield return new object[] { "stops/{stopId}/start", true };
        yield return new object[] { "stops/{stopId}/complete", true };
        yield return new object[] { "stops/{stopId}/skip", true };
        yield return new object[] { "stops/{stopId}/report-problem", true };
    }

    [Theory]
    [MemberData(nameof(RequiredRoutes))]
    public async Task Missing_key_on_required_endpoint_returns_400_with_error_shape(string routeTemplate, bool needsBody)
    {
        var scenario = await _factory.SeedInProgressScenarioAsync();
        var token = _factory.MintToken(scenario.CompanyId, scenario.DriverUserId, UserRole.Driver, scenario.DriverId);
        var client = _factory.CreateAuthenticatedClient(token);

        var path = "api/v1/" + routeTemplate
            .Replace("{routeId}", scenario.RouteId.ToString())
            .Replace("{stopId}", scenario.StopId.ToString());

        HttpResponseMessage response;
        if (needsBody && routeTemplate.EndsWith("report-problem"))
        {
            response = await client.PostAsJsonAsync(path, new { problemType = "RoadClosed" });
        }
        else
        {
            response = await client.PostAsync(path, content: null);
        }

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        root.GetProperty("title").GetString().Should().Be("Missing Idempotency-Key");
        root.GetProperty("status").GetInt32().Should().Be(400);
        root.GetProperty("detail").GetString().Should().NotBeNullOrEmpty();
        root.GetProperty("correlationId").GetString().Should().NotBeNullOrEmpty();
    }
}
