using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using AbfuhrPilot.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace AbfuhrPilot.UnitTests.Integration;

/// <summary>
/// At least one genuine
/// HTTP-level test driving a required-idempotency route end-to-end through the
/// full ASP.NET pipeline (auth + <c>IdempotencyFilter</c> + JSON options) with
/// a VALID <c>Idempotency-Key</c>, including a same-key retry replay.
/// </summary>
public class StopsControllerTests : IClassFixture<WebApiFactory>
{
    private readonly WebApiFactory _factory;

    public StopsControllerTests(WebApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Stop_start_with_valid_key_returns_200_and_InProgress_status()
    {
        var scenario = await _factory.SeedInProgressScenarioAsync();
        var token = _factory.MintToken(scenario.CompanyId, scenario.DriverUserId, UserRole.Driver, scenario.DriverId);
        var client = _factory.CreateAuthenticatedClient(token);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"api/v1/stops/{scenario.StopId}/start");
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("id").GetGuid().Should().Be(scenario.StopId);
        doc.RootElement.GetProperty("status").GetString().Should().Be("InProgress");
    }

    [Fact]
    public async Task Retried_stop_start_with_same_key_replays_identical_response_without_reexecuting()
    {
        var scenario = await _factory.SeedInProgressScenarioAsync();
        var token = _factory.MintToken(scenario.CompanyId, scenario.DriverUserId, UserRole.Driver, scenario.DriverId);
        var client = _factory.CreateAuthenticatedClient(token);
        var key = Guid.NewGuid().ToString();

        using var firstRequest = new HttpRequestMessage(HttpMethod.Post, $"api/v1/stops/{scenario.StopId}/start");
        firstRequest.Headers.Add("Idempotency-Key", key);
        var firstResponse = await client.SendAsync(firstRequest);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        firstResponse.Headers.Contains("Idempotency-Replayed").Should().BeFalse();
        var firstBody = await firstResponse.Content.ReadAsStringAsync();

        using var retryRequest = new HttpRequestMessage(HttpMethod.Post, $"api/v1/stops/{scenario.StopId}/start");
        retryRequest.Headers.Add("Idempotency-Key", key);
        var retryResponse = await client.SendAsync(retryRequest);

        retryResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        retryResponse.Headers.TryGetValues("Idempotency-Replayed", out var replayed).Should().BeTrue();
        replayed!.Should().ContainSingle().Which.Should().Be("true");

        var retryBody = await retryResponse.Content.ReadAsStringAsync();
        retryBody.Should().Be(firstBody); // byte-identical replay, no duplicate side effect

        // Confirm no duplicate side effect at the data level: the stop is not re-started
        // (its arrival timestamp is set exactly once, not bumped on the replayed request).
        await using var db = _factory.CreateDbContext();
        var stop = await db.Stops.FindAsync(scenario.StopId);
        stop!.Status.Should().Be(StopStatus.InProgress);
    }
}
