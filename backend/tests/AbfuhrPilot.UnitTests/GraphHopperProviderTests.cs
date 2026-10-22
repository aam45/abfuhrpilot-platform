using System.Net;
using AbfuhrPilot.Application.Routing;
using AbfuhrPilot.Infrastructure.Routing;
using FluentAssertions;
using Xunit;

namespace AbfuhrPilot.UnitTests;

/// <summary>
/// Covers the bounded single-retry reliability behavior added to
/// <see cref="GraphHopperProvider"/>'s internal route call: transient failures
/// (429/5xx) are retried exactly once; non-retryable 4xx failures are not
/// retried at all, so a single request never burns extra paid API quota.
/// </summary>
public class GraphHopperProviderTests
{
    private const string CannedRouteJson = """
        {"paths":[{"distance":1234.5,"time":60000,"points":{"coordinates":[[13.4,52.5],[13.41,52.51]]}}]}
        """;

    /// <summary>Fake handler that returns a canned sequence of responses, one per call, and
    /// records how many times it was invoked (never the request URL/key — tests only assert counts).</summary>
    private sealed class SequenceHandler : HttpMessageHandler
    {
        private readonly List<Func<HttpResponseMessage>> _responses;
        public int CallCount { get; private set; }

        public SequenceHandler(params Func<HttpResponseMessage>[] responses)
        {
            _responses = new List<Func<HttpResponseMessage>>(responses);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var index = Math.Min(CallCount, _responses.Count - 1);
            CallCount++;
            // Responses can throw (e.g. HttpRequestException for a network failure, or
            // OperationCanceledException for cancellation) instead of returning a value —
            // the throw happens synchronously here, before the result is wrapped in a Task,
            // which mirrors how HttpClientHandler surfaces these failures.
            return Task.FromResult(_responses[index]());
        }
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body) };

    private static RouteCalculationRequest BuildRequest() =>
        new(new GeoPoint(52.5, 13.4),
            new List<RoutingStopInput> { new(Guid.NewGuid(), 1, 52.51, 13.41) },
            End: null, Vehicle: null);

    /// <summary>
    /// Builds a provider for tests. <paramref name="retryDelay"/> defaults to a near-zero delay
    /// so retry-triggering tests don't take a real wall-clock hit; the 400ms production default
    /// is only exercised when the provider is constructed via DI (see DependencyInjection.cs).
    /// </summary>
    private static GraphHopperProvider BuildProvider(SequenceHandler handler, TimeSpan? retryDelay = null)
    {
        var http = new HttpClient(handler);
        var options = new RoutingOptions
        {
            GraphHopper = new GraphHopperOptions { ApiKey = "test-key", TimeoutSeconds = 5 },
        };
        return new GraphHopperProvider(http, options, retryDelay ?? TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task Transient_503_then_success_retries_once_and_returns_the_result()
    {
        var handler = new SequenceHandler(
            () => Json(HttpStatusCode.ServiceUnavailable, ""),
            () => Json(HttpStatusCode.OK, CannedRouteJson));
        var provider = BuildProvider(handler);

        var result = await provider.CalculateRouteAsync(BuildRequest(), default);

        handler.CallCount.Should().Be(2);
        result.TotalDistanceMeters.Should().Be(1234.5);
        result.TotalDurationSeconds.Should().Be(60.0);
    }

    [Fact]
    public async Task Non_retryable_400_does_not_retry_and_propagates()
    {
        var handler = new SequenceHandler(() => Json(HttpStatusCode.BadRequest, ""));
        var provider = BuildProvider(handler);

        var act = async () => await provider.CalculateRouteAsync(BuildRequest(), default);

        await act.Should().ThrowAsync<HttpRequestException>();
        handler.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task Immediate_success_does_not_retry()
    {
        var handler = new SequenceHandler(() => Json(HttpStatusCode.OK, CannedRouteJson));
        var provider = BuildProvider(handler);

        var result = await provider.CalculateRouteAsync(BuildRequest(), default);

        handler.CallCount.Should().Be(1);
        result.TotalDistanceMeters.Should().Be(1234.5);
    }

    [Fact]
    public async Task Two_consecutive_transient_failures_still_propagates_after_single_retry()
    {
        var handler = new SequenceHandler(
            () => Json(HttpStatusCode.ServiceUnavailable, ""),
            () => Json(HttpStatusCode.ServiceUnavailable, ""));
        var provider = BuildProvider(handler);

        var act = async () => await provider.CalculateRouteAsync(BuildRequest(), default);

        await act.Should().ThrowAsync<HttpRequestException>();
        handler.CallCount.Should().Be(2); // exactly one retry, then propagate
    }

    [Fact]
    public async Task Network_failure_HttpRequestException_then_success_retries_once_and_returns_the_result()
    {
        // Mirrors the 503-then-success case, but for a genuine network-level failure
        // (HttpRequestException thrown directly from the handler, not a 5xx status code) —
        // covers the `catch (HttpRequestException) when (!isLastAttempt)` branch in TryGetAsync.
        var handler = new SequenceHandler(
            () => throw new HttpRequestException("network down"),
            () => Json(HttpStatusCode.OK, CannedRouteJson));
        var provider = BuildProvider(handler);

        var result = await provider.CalculateRouteAsync(BuildRequest(), default);

        handler.CallCount.Should().Be(2);
        result.TotalDistanceMeters.Should().Be(1234.5);
        result.TotalDurationSeconds.Should().Be(60.0);
    }

    [Fact]
    public async Task HttpClient_internal_timeout_not_caller_cancellation_retries_once_and_returns_the_result()
    {
        // The caller's own token (`default`, i.e. CancellationToken.None) is never cancelled here,
        // so `ct.IsCancellationRequested` is false when the OperationCanceledException is caught —
        // this is indistinguishable from HttpClient's own internal timeout and must be retried.
        var handler = new SequenceHandler(
            () => throw new OperationCanceledException("HttpClient internal timeout"),
            () => Json(HttpStatusCode.OK, CannedRouteJson));
        var provider = BuildProvider(handler);

        var result = await provider.CalculateRouteAsync(BuildRequest(), default);

        handler.CallCount.Should().Be(2);
        result.TotalDistanceMeters.Should().Be(1234.5);
        result.TotalDurationSeconds.Should().Be(60.0);
    }

    [Fact]
    public async Task Caller_cancellation_propagates_and_is_not_retried()
    {
        // The caller's own CancellationTokenSource is cancelled synchronously inside the fake
        // handler right before it throws, so by the time TryGetAsync's catch clause runs,
        // `ct.IsCancellationRequested` is true for the *same* token the caller passed in — this
        // must NOT be swallowed/retried; it must propagate as a cancellation.
        using var cts = new CancellationTokenSource();
        var handler = new SequenceHandler(
            () =>
            {
                cts.Cancel();
                throw new OperationCanceledException(cts.Token);
            },
            () => Json(HttpStatusCode.OK, CannedRouteJson));
        var provider = BuildProvider(handler);

        var act = async () => await provider.CalculateRouteAsync(BuildRequest(), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        handler.CallCount.Should().Be(1); // not retried — caller cancellation propagates immediately
    }
}
