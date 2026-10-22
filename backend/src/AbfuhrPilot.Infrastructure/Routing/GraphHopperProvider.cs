using System.Globalization;
using System.Text.Json;
using AbfuhrPilot.Application.Routing;

namespace AbfuhrPilot.Infrastructure.Routing;

/// <summary>
/// GraphHopper implementation of the routing + optimization contracts. Uses the
/// Directions ("/route") endpoint; with <c>optimize=true</c> it solves the TSP
/// over the given points. Provider-specific parsing stays here; the application
/// layer only sees the neutral DTOs. No API key is ever logged.
///
/// Truck honesty: the routing profile comes from configuration
/// (<c>Routing:GraphHopper:Profile</c>). With the default "car" profile the
/// result explicitly warns that truck restrictions are NOT enforced. With a
/// truck-capable profile ("truck"/"small_truck"/"custom") GraphHopper applies
/// its generic truck rules, but our specific vehicle dimensions are not yet
/// transmitted (needs a custom-model-capable plan + live validation) — so the
/// result still carries TRUCK_PROFILE_NOT_FULLY_SUPPORTED.
///
/// NOTE: the exact <c>points_order</c> field for optimized ordering is parsed
/// defensively — if GraphHopper does not return it for the configured plan, the
/// input order is preserved and a warning is set.
/// </summary>
public class GraphHopperProvider : IRoutingProvider, IRouteOptimizationProvider, INavigationProvider
{
    private readonly HttpClient _http;
    private readonly GraphHopperOptions _options;
    private readonly TimeSpan _retryDelay;

    /// <param name="retryDelay">
    /// Test-only override for the bounded transient-retry delay (production default
    /// stays 400ms when omitted). Not read from configuration; DI resolves the
    /// default via this optional parameter.
    /// </param>
    public GraphHopperProvider(HttpClient http, RoutingOptions options, TimeSpan? retryDelay = null)
    {
        _http = http;
        _options = options.GraphHopper;
        _http.Timeout = TimeSpan.FromSeconds(Math.Clamp(_options.TimeoutSeconds, 3, 60));
        _retryDelay = retryDelay ?? TimeSpan.FromMilliseconds(400);
    }

    public RoutingProviderType Type => RoutingProviderType.GraphHopper;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.ApiKey);

    /// <summary>
    /// Honest truck-warning computation for the configured profile — pure and
    /// unit-testable without any HTTP call.
    /// </summary>
    public static IReadOnlyList<RoutingWarning> BuildProfileWarnings(GraphHopperOptions options, VehicleRoutingProfile? vehicle)
    {
        var warnings = new List<RoutingWarning>();
        var wantsTruck = vehicle?.RequiresTruckRouting ?? false;

        if (!options.IsTruckProfile)
        {
            if (wantsTruck)
            {
                warnings.Add(new RoutingWarning(RoutingWarningCodes.ProviderUsingCarFallback,
                    "GraphHopper is currently configured with car profile; truck restrictions are not enforced.",
                    RoutingWarning.SeverityWarning));
                warnings.Add(new RoutingWarning(RoutingWarningCodes.TruckRestrictionsNotEnforced,
                    "Route was calculated with the car profile — height/weight/access restrictions were not applied.",
                    RoutingWarning.SeverityWarning));
            }
        }
        else
        {
            // Truck profile active: generic truck rules apply, but our concrete
            // vehicle dimensions are not yet passed to the API.
            warnings.Add(new RoutingWarning(RoutingWarningCodes.TruckProfileNotFullySupported,
                $"GraphHopper '{options.Profile}' profile applies generic truck rules; " +
                "vehicle-specific dimensions (height/weight/width/length) are not yet transmitted.",
                RoutingWarning.SeverityInfo));
        }
        return warnings;
    }

    public async Task<RouteCalculationResult> CalculateRouteAsync(RouteCalculationRequest request, CancellationToken ct)
    {
        var (points, _) = OrderedPoints(request.Start, request.Stops, request.End);
        using var doc = await CallRouteAsync(points, optimize: false, ct);
        var path = doc.RootElement.GetProperty("paths")[0];

        return new RouteCalculationResult(
            RoutingProviderType.GraphHopper, ProviderConfigured: true, Warning: null,
            path.GetProperty("distance").GetDouble(),
            path.GetProperty("time").GetDouble() / 1000.0,
            Array.Empty<RouteLeg>(),
            ReadGeometry(path),
            Array.Empty<Guid>())
        {
            Warnings = BuildProfileWarnings(_options, request.Vehicle),
            RoutingProfileUsed = _options.Profile,
            TruckRestrictionsApplied = _options.IsTruckProfile,
        };
    }

    public async Task<RouteOptimizationResult> OptimizeRouteAsync(RouteOptimizationRequest request, CancellationToken ct)
    {
        var (points, meta) = OrderedPoints(request.Start, request.Stops, request.End);
        using var doc = await CallRouteAsync(points, optimize: true, ct);
        var path = doc.RootElement.GetProperty("paths")[0];

        // Try to read the optimized point order; fall back to the input order.
        var warnings = new List<RoutingWarning>(BuildProfileWarnings(_options, request.Vehicle));
        string? warning = null;
        List<Guid?> orderedMeta;
        if (path.TryGetProperty("points_order", out var po) && po.ValueKind == JsonValueKind.Array)
        {
            orderedMeta = po.EnumerateArray()
                .Select(i => i.GetInt32())
                .Where(i => i >= 0 && i < meta.Count)
                .Select(i => meta[i])
                .ToList();
        }
        else
        {
            orderedMeta = meta;
            warning = "GraphHopper hat keine explizite Reihenfolge geliefert – Eingabereihenfolge beibehalten.";
            warnings.Add(new RoutingWarning(RoutingWarningCodes.TruckProfileNotFullySupported,
                "GraphHopper returned no explicit point order — the input order was kept.",
                RoutingWarning.SeverityInfo));
        }

        var order = orderedMeta
            .Where(id => id is not null)
            .Select((id, idx) => new OptimizedStop(id!.Value, idx + 1))
            .ToList();

        return new RouteOptimizationResult(
            RoutingProviderType.GraphHopper, ProviderConfigured: true, warning,
            order,
            path.GetProperty("distance").GetDouble(),
            path.GetProperty("time").GetDouble() / 1000.0,
            Array.Empty<Guid>(),
            ReadGeometry(path))
        {
            Warnings = warnings,
            RoutingProfileUsed = _options.Profile,
            TruckRestrictionsApplied = _options.IsTruckProfile,
        };
    }

    /// <summary>
    /// In-app navigation: requests real turn instructions from GraphHopper and
    /// parses them conservatively. If the configured plan returns no
    /// instructions, the input geometry is kept and an explicit
    /// TURN_BY_TURN_STEPS_NOT_AVAILABLE warning is added — never faked steps.
    /// Live-key validation of the instruction shape is still pending.
    /// </summary>
    public async Task<ProviderNavigation> GetNavigationAsync(RouteCalculationRequest request, CancellationToken ct)
    {
        var (points, _) = OrderedPoints(request.Start, request.Stops, request.End);
        using var doc = await CallRouteAsync(points, optimize: false, ct, instructions: true);
        var path = doc.RootElement.GetProperty("paths")[0];

        var geometry = ReadGeometry(path)?.Points ?? Array.Empty<GeoPoint>();
        var distance = path.GetProperty("distance").GetDouble();
        var duration = path.GetProperty("time").GetDouble() / 1000.0;
        var steps = ParseInstructions(path, geometry);

        var warnings = new List<RoutingWarning>(BuildProfileWarnings(_options, request.Vehicle));
        List<NavigationLeg> legs;
        if (steps.Count > 0)
        {
            // Conservative first pass: one leg spanning the whole tour with all
            // real steps (per-stop leg splitting needs live-key validation).
            legs = new List<NavigationLeg>
            {
                new(null, null, "Start", "Ziel", distance, duration, steps),
            };
        }
        else
        {
            legs = new List<NavigationLeg>();
            warnings.Add(new RoutingWarning(RoutingWarningCodes.TurnByTurnStepsNotAvailable,
                "GraphHopper lieferte keine Abbiegehinweise für dieses Profil.",
                RoutingWarning.SeverityWarning));
        }

        return new ProviderNavigation(RoutingProviderType.GraphHopper, ProviderConfigured: true,
            geometry, distance, duration, legs, warnings, _options.Profile, _options.IsTruckProfile);
    }

    /// <summary>Maps GraphHopper `instructions` (text/sign/street_name/interval) to
    /// neutral steps. Static + defensive so it is unit-testable with canned JSON.</summary>
    public static IReadOnlyList<NavigationStep> ParseInstructions(JsonElement path, IReadOnlyList<GeoPoint> geometry)
    {
        if (!path.TryGetProperty("instructions", out var instr) || instr.ValueKind != JsonValueKind.Array)
            return Array.Empty<NavigationStep>();

        var steps = new List<NavigationStep>();
        var seq = 1;
        foreach (var i in instr.EnumerateArray())
        {
            var text = i.TryGetProperty("text", out var t) ? t.GetString() ?? "" : "";
            var sign = i.TryGetProperty("sign", out var sg) && sg.ValueKind == JsonValueKind.Number ? sg.GetInt32() : 0;
            var road = i.TryGetProperty("street_name", out var rn) ? rn.GetString() : null;
            var dist = i.TryGetProperty("distance", out var d) ? d.GetDouble() : 0;
            var time = i.TryGetProperty("time", out var tm) ? tm.GetDouble() / 1000.0 : 0;

            double lat = 0, lng = 0;
            if (i.TryGetProperty("interval", out var iv) && iv.ValueKind == JsonValueKind.Array && iv.GetArrayLength() > 0)
            {
                var idx = iv[0].GetInt32();
                if (idx >= 0 && idx < geometry.Count) { lat = geometry[idx].Latitude; lng = geometry[idx].Longitude; }
            }

            var (kind, modifier) = MapSign(sign);
            steps.Add(new NavigationStep(seq++, text, kind, modifier,
                string.IsNullOrWhiteSpace(road) ? null : road, dist, time, lat, lng));
        }
        return steps;
    }

    private static (string kind, string? modifier) MapSign(int sign) => sign switch
    {
        -3 => ("turn", "sharp_left"),
        -2 => ("turn", "left"),
        -1 => ("turn", "slight_left"),
        0 => ("continue", null),
        1 => ("turn", "slight_right"),
        2 => ("turn", "right"),
        3 => ("turn", "sharp_right"),
        4 or 5 => ("arrive", null),          // finish / reached via point
        6 => ("roundabout", null),
        _ => ("unknown", null),
    };

    private async Task<JsonDocument> CallRouteAsync(List<GeoPoint> points, bool optimize, CancellationToken ct,
        bool instructions = false)
    {
        var query = new List<string>();
        foreach (var p in points)
            query.Add($"point={p.Latitude.ToString(CultureInfo.InvariantCulture)},{p.Longitude.ToString(CultureInfo.InvariantCulture)}");
        var profile = string.IsNullOrWhiteSpace(_options.Profile) ? "car" : _options.Profile;
        query.Add($"profile={Uri.EscapeDataString(profile)}");
        query.Add("points_encoded=false");
        query.Add("calc_points=true");
        query.Add(instructions ? "instructions=true" : "instructions=false");
        if (optimize) query.Add("optimize=true");
        query.Add($"key={Uri.EscapeDataString(_options.ApiKey)}");

        var url = $"{_options.BaseUrl.TrimEnd('/')}/route?{string.Join("&", query)}";

        // Bounded reliability: retry exactly once for transient failures (network
        // errors, HttpClient-internal timeout, HTTP 429/5xx). Never retry on
        // caller-cancelled requests or non-retryable 4xx (bad request / bad key —
        // retrying would just waste paid API quota). The URL/key is never logged.
        const int maxAttempts = 2;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var isLastAttempt = attempt == maxAttempts;
            var (transientFailure, response) = await TryGetAsync(url, isLastAttempt, ct);
            if (transientFailure)
            {
                await Task.Delay(_retryDelay, ct);
                continue;
            }

            using var resp = response!;
            resp.EnsureSuccessStatusCode();
            var stream = await resp.Content.ReadAsStreamAsync(ct);
            return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        }

        // Unreachable: the last attempt either returns or lets its exception propagate.
        throw new InvalidOperationException("GraphHopper call did not complete.");
    }

    /// <summary>
    /// Performs one GET attempt. Returns (true, null) only for a genuinely
    /// transient failure that is safe to retry (network error, HttpClient's own
    /// timeout, or HTTP 429/5xx) and only when this is not the last attempt.
    /// Any other outcome (success or non-retryable failure) is returned as-is
    /// so the caller can let <see cref="HttpResponseMessage.EnsureSuccessStatusCode"/>
    /// throw its normal exception without it being mistaken for a transient one.
    /// </summary>
    private async Task<(bool transientFailure, HttpResponseMessage? response)> TryGetAsync(
        string url, bool isLastAttempt, CancellationToken ct)
    {
        HttpResponseMessage resp;
        try
        {
            resp = await _http.GetAsync(url, ct);
        }
        catch (HttpRequestException) when (!isLastAttempt)
        {
            return (true, null);
        }
        catch (OperationCanceledException) when (!isLastAttempt && !ct.IsCancellationRequested)
        {
            // HttpClient's own timeout expired (not the caller's token) — retry once.
            return (true, null);
        }

        if (!isLastAttempt && IsRetryableStatus(resp.StatusCode))
        {
            resp.Dispose();
            return (true, null);
        }
        return (false, resp);
    }

    private static bool IsRetryableStatus(System.Net.HttpStatusCode status) =>
        status == System.Net.HttpStatusCode.TooManyRequests || (int)status >= 500;

    /// <summary>Builds the ordered point list and a parallel meta list (stop id per point, null for depot/end).</summary>
    private static (List<GeoPoint> points, List<Guid?> meta) OrderedPoints(
        GeoPoint? start, IReadOnlyList<RoutingStopInput> stops, GeoPoint? end)
    {
        var points = new List<GeoPoint>();
        var meta = new List<Guid?>();
        if (start is not null) { points.Add(start); meta.Add(null); }
        foreach (var s in stops.OrderBy(s => s.SequenceNumber))
        {
            points.Add(new GeoPoint(s.Latitude, s.Longitude));
            meta.Add(s.StopId);
        }
        if (end is not null) { points.Add(end); meta.Add(null); }
        return (points, meta);
    }

    private static RouteGeometry? ReadGeometry(JsonElement path)
    {
        if (!path.TryGetProperty("points", out var pts) || !pts.TryGetProperty("coordinates", out var coords))
            return null;
        var list = new List<GeoPoint>();
        foreach (var c in coords.EnumerateArray())
            list.Add(new GeoPoint(c[1].GetDouble(), c[0].GetDouble())); // GraphHopper is [lng, lat]
        return new RouteGeometry(list);
    }
}
