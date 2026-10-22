using System.Net;
using AbfuhrPilot.Application.Routing;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AbfuhrPilot.Api.Startup;

/// <summary>
/// Optional, non-fatal live-key check for <c>Routing:GraphHopper:ApiKey</c>. Issues a single
/// best-effort GET against GraphHopper's "/route" endpoint with two trivial hardcoded points
/// (no retries, short bounded timeout independent of the provider's own reliability settings —
/// a health check must never burn extra paid API quota). Never throws and never logs the API
/// key or full request URL; only status codes / exception types are surfaced.
/// Exposed at <c>GET /health/routing</c>; restricted to the Admin and Dispatcher roles.
/// Distinct from the anonymous <c>/health</c> liveness probe, which requires no authentication.
/// </summary>
public class GraphHopperHealthCheck : IHealthCheck
{
    // Berlin-area coordinates, same ones already used in GraphHopperProviderTests — chosen only
    // because they are trivially valid points, not because the route itself matters.
    private const double Point1Lat = 52.5;
    private const double Point1Lng = 13.4;
    private const double Point2Lat = 52.51;
    private const double Point2Lng = 13.41;
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly RoutingOptions _routingOptions;
    private readonly ILogger<GraphHopperHealthCheck> _logger;

    public GraphHopperHealthCheck(
        IHttpClientFactory httpClientFactory,
        RoutingOptions routingOptions,
        ILogger<GraphHopperHealthCheck> logger)
    {
        _httpClientFactory = httpClientFactory;
        _routingOptions = routingOptions;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var options = _routingOptions.GraphHopper;

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            return HealthCheckResult.Healthy(
                "GraphHopper nicht konfiguriert – Routing nutzt lokalen Fallback.");
        }

        var url = $"{options.BaseUrl.TrimEnd('/')}/route" +
                   $"?point={Point1Lat.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                   $"{Point1Lng.ToString(System.Globalization.CultureInfo.InvariantCulture)}" +
                   $"&point={Point2Lat.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                   $"{Point2Lng.ToString(System.Globalization.CultureInfo.InvariantCulture)}" +
                   "&profile=car" +
                   $"&key={Uri.EscapeDataString(options.ApiKey)}";

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(RequestTimeout);

        try
        {
            var http = _httpClientFactory.CreateClient(nameof(GraphHopperHealthCheck));
            using var response = await http.GetAsync(url, cts.Token);

            if (response.IsSuccessStatusCode)
                return HealthCheckResult.Healthy("GraphHopper erreichbar.");

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                _logger.LogWarning(
                    "GraphHopper health check received {StatusCode} — API key may be invalid.",
                    (int)response.StatusCode);
                return HealthCheckResult.Degraded("GraphHopper API key scheint ungültig zu sein.");
            }

            _logger.LogWarning(
                "GraphHopper health check received unexpected status {StatusCode}.",
                (int)response.StatusCode);
            return HealthCheckResult.Degraded("GraphHopper ist aktuell nicht erreichbar.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Our own bounded timeout elapsed, not the caller's cancellation.
            _logger.LogWarning("GraphHopper health check timed out after {Timeout}.", RequestTimeout);
            return HealthCheckResult.Degraded("GraphHopper ist aktuell nicht erreichbar.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "GraphHopper health check failed with a network error.");
            return HealthCheckResult.Degraded("GraphHopper ist aktuell nicht erreichbar.");
        }
        catch (Exception ex) when (!(ex is OperationCanceledException) || !cancellationToken.IsCancellationRequested)
        {
            // Defense-in-depth: this component's entire contract is "must never throw" (it feeds
            // a non-fatal diagnostic surface, not app readiness). Anything unexpected — e.g. a
            // malformed BaseUrl producing a UriFormatException/ArgumentException instead of an
            // HttpRequestException — degrades instead of propagating.
            _logger.LogWarning(ex, "GraphHopper health check failed unexpectedly.");
            return HealthCheckResult.Degraded("GraphHopper ist aktuell nicht erreichbar.");
        }
    }
}
