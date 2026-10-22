using System.Net;
using AbfuhrPilot.Api.Startup;
using AbfuhrPilot.Application.Routing;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AbfuhrPilot.UnitTests;

/// <summary>
/// Covers <see cref="GraphHopperHealthCheck"/>: a non-fatal, best-effort live-key check that
/// must never throw and never retry. Uses a fake <see cref="HttpMessageHandler"/> (same idiom
/// as <c>GraphHopperProviderTests</c>) so no live network call is made.
/// </summary>
public class GraphHopperHealthCheckTests
{
    private sealed class SingleResponseHandler : HttpMessageHandler
    {
        private readonly Func<HttpResponseMessage> _response;
        private readonly Exception? _exception;
        public int CallCount { get; private set; }

        public SingleResponseHandler(Func<HttpResponseMessage> response) => _response = response;
        public SingleResponseHandler(Exception exception)
        {
            _exception = exception;
            _response = () => throw exception;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            CallCount++;
            if (_exception is not null)
                throw _exception;
            return Task.FromResult(_response());
        }
    }

    private sealed class FakeHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;
        public FakeHttpClientFactory(HttpMessageHandler handler) => _handler = handler;
        public HttpClient CreateClient(string name) => new(_handler);
    }

    private static HttpResponseMessage Response(HttpStatusCode status) => new(status) { Content = new StringContent("{}") };

    private static GraphHopperHealthCheck BuildCheck(HttpMessageHandler handler, string apiKey = "test-key")
    {
        var options = new RoutingOptions { GraphHopper = new GraphHopperOptions { ApiKey = apiKey } };
        return new GraphHopperHealthCheck(new FakeHttpClientFactory(handler), options,
            NullLogger<GraphHopperHealthCheck>.Instance);
    }

    private static HealthCheckContext Context() => new();

    [Fact]
    public async Task Not_configured_is_healthy_and_makes_no_http_call()
    {
        var handler = new SingleResponseHandler(() => Response(HttpStatusCode.OK));
        var check = BuildCheck(handler, apiKey: "");

        var result = await check.CheckHealthAsync(Context());

        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Contain("nicht konfiguriert");
        handler.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Successful_response_is_healthy()
    {
        var handler = new SingleResponseHandler(() => Response(HttpStatusCode.OK));
        var check = BuildCheck(handler);

        var result = await check.CheckHealthAsync(Context());

        result.Status.Should().Be(HealthStatus.Healthy);
        handler.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task Unauthorized_response_is_degraded_with_invalid_key_message()
    {
        var handler = new SingleResponseHandler(() => Response(HttpStatusCode.Unauthorized));
        var check = BuildCheck(handler);

        var result = await check.CheckHealthAsync(Context());

        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Contain("API key");
        handler.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task Forbidden_response_is_degraded_with_invalid_key_message()
    {
        var handler = new SingleResponseHandler(() => Response(HttpStatusCode.Forbidden));
        var check = BuildCheck(handler);

        var result = await check.CheckHealthAsync(Context());

        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Contain("API key");
    }

    [Fact]
    public async Task Network_exception_is_degraded_with_unreachable_message()
    {
        var handler = new SingleResponseHandler(new HttpRequestException("network down"));
        var check = BuildCheck(handler);

        var result = await check.CheckHealthAsync(Context());

        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Contain("nicht erreichbar");
    }

    [Fact]
    public async Task Server_error_is_degraded_with_unreachable_message()
    {
        var handler = new SingleResponseHandler(() => Response(HttpStatusCode.ServiceUnavailable));
        var check = BuildCheck(handler);

        var result = await check.CheckHealthAsync(Context());

        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Contain("nicht erreichbar");
        handler.CallCount.Should().Be(1); // health check must not retry
    }

    /// <summary>
    /// Defense-in-depth: an unexpected exception type (e.g. what a malformed BaseUrl would raise)
    /// must still degrade rather than propagate — this component's whole contract is "never
    /// throws" since it feeds a non-fatal diagnostic surface, not app readiness.
    /// </summary>
    [Fact]
    public async Task Unexpected_exception_type_is_degraded_not_thrown()
    {
        var handler = new SingleResponseHandler(new InvalidOperationException("unexpected"));
        var check = BuildCheck(handler);

        var result = await check.CheckHealthAsync(Context());

        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Contain("nicht erreichbar");
    }
}
