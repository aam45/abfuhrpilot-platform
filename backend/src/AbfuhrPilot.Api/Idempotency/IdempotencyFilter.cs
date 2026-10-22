using System.Text.Json;
using AbfuhrPilot.Application.Common;
using AbfuhrPilot.Infrastructure.Idempotency;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace AbfuhrPilot.Api.Idempotency;

/// <summary>
/// Marks an endpoint as idempotency-aware: an <c>Idempotency-Key</c> header is
/// honored (replay / conflict semantics). With <see cref="Required"/> the header
/// is mandatory (400 without it) — used for the driver's operational actions so
/// native offline replay is always safe.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class IdempotentAttribute : Attribute
{
    public bool Required { get; init; }
}

/// <summary>
/// Global resource filter implementing the idempotency contract for endpoints
/// marked with <see cref="IdempotentAttribute"/>:
///  - runs after auth (company/user known), before model binding;
///  - hashes method + path + streamed body (multipart: content-aware section
///    hash via <see cref="MultipartRequestHasher"/>, excluding the boundary);
///  - replays the stored response for a repeated key+hash (with
///    <c>Idempotency-Replayed: true</c>), so handlers, domain events and outbox
///    writes are NOT executed twice;
///  - rejects a reused key with a different request as 409;
///  - stores only successful (2xx) responses — errors re-execute on retry.
/// GET/HEAD/OPTIONS and unmarked endpoints pass through untouched.
/// </summary>
public class IdempotencyFilter : IAsyncResourceFilter
{
    public const string HeaderName = "Idempotency-Key";

    private readonly IdempotencyService _service;
    private readonly ICurrentUser _me;
    private readonly JsonSerializerOptions _json;

    public IdempotencyFilter(IdempotencyService service, ICurrentUser me, IOptions<JsonOptions> jsonOptions)
    {
        _service = service;
        _me = me;
        // Match MVC's output formatter exactly (it applies UnsafeRelaxedJsonEscaping
        // when no encoder is set) so a replayed body is byte-identical to the original.
        _json = new JsonSerializerOptions(jsonOptions.Value.JsonSerializerOptions)
        {
            Encoder = jsonOptions.Value.JsonSerializerOptions.Encoder
                      ?? System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
    }

    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var request = context.HttpContext.Request;
        var marker = context.ActionDescriptor.EndpointMetadata.OfType<IdempotentAttribute>().FirstOrDefault();

        if (marker is null || HttpMethods.IsGet(request.Method) ||
            HttpMethods.IsHead(request.Method) || HttpMethods.IsOptions(request.Method))
        {
            await next();
            return;
        }

        var key = request.Headers[HeaderName].FirstOrDefault()?.Trim();
        if (string.IsNullOrEmpty(key))
        {
            if (marker.Required)
            {
                context.Result = Error(context, StatusCodes.Status400BadRequest, "Missing Idempotency-Key",
                    $"This endpoint requires an '{HeaderName}' header (client-generated UUID per action).");
            }
            else
            {
                await next(); // optional endpoints work without a key
            }
            return;
        }

        if (key.Length > IdempotencyService.MaxKeyLength)
        {
            context.Result = Error(context, StatusCodes.Status400BadRequest, "Invalid Idempotency-Key",
                $"The '{HeaderName}' header must be at most {IdempotencyService.MaxKeyLength} characters.");
            return;
        }

        // Hash the request before model binding consumes the body.
        request.EnableBuffering();
        var hash = await IdempotencyService.ComputeRequestHashAsync(
            request.Method, request.Path.Value ?? string.Empty, request.Body, request.ContentType,
            context.HttpContext.RequestAborted, MultipartRequestHasher.ComputeAsync);
        request.Body.Position = 0;

        var lookup = await _service.LookupAsync(_me.CompanyId, _me.UserId, key, hash, context.HttpContext.RequestAborted);
        switch (lookup.Outcome)
        {
            case IdempotencyOutcome.Replay:
                context.HttpContext.Response.Headers["Idempotency-Replayed"] = "true";
                context.Result = new ContentResult
                {
                    Content = lookup.ResponseBody,
                    ContentType = "application/json",
                    StatusCode = lookup.StatusCode,
                };
                return;

            case IdempotencyOutcome.Conflict:
                context.Result = Error(context, StatusCodes.Status409Conflict, "Idempotency key conflict",
                    "This Idempotency-Key was already used for a different request. Never reuse a key for another action.");
                return;
        }

        var executed = await next();

        // Store only clean 2xx results so failed attempts can be retried.
        if (executed.Exception is not null && !executed.ExceptionHandled) return;
        var (status, body) = ExtractResponse(executed.Result);
        if (status is >= 200 and < 300)
        {
            await _service.StoreAsync(_me.CompanyId, _me.UserId, key, request.Method,
                request.Path.Value ?? string.Empty, hash, status, body, CancellationToken.None);
        }
    }

    private (int status, string body) ExtractResponse(IActionResult? result) => result switch
    {
        ObjectResult o => (o.StatusCode ?? StatusCodes.Status200OK,
            JsonSerializer.Serialize(o.Value, o.Value?.GetType() ?? typeof(object), _json)),
        StatusCodeResult s => (s.StatusCode, string.Empty),
        EmptyResult => (StatusCodes.Status200OK, string.Empty),
        null => (StatusCodes.Status200OK, string.Empty),
        _ => (0, string.Empty), // file/stream results etc. — not stored
    };

    private static ObjectResult Error(ResourceExecutingContext context, int status, string title, string detail) =>
        new(new
        {
            title,
            status,
            detail,
            correlationId = context.HttpContext.Items.TryGetValue("CorrelationId", out var c) ? c as string : null,
        })
        { StatusCode = status };
}
