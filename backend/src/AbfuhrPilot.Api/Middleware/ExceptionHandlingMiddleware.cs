using System.Text.Json;
using AbfuhrPilot.Application.Common;
using AbfuhrPilot.Domain.Common;

namespace AbfuhrPilot.Api.Middleware;

/// <summary>
/// Converts domain/application exceptions into a consistent JSON error contract
/// and attaches a correlation id to every response for traceability.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers["X-Correlation-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString();
        context.Response.Headers["X-Correlation-Id"] = correlationId;
        context.Items["CorrelationId"] = correlationId; // flows into outbox/integration events

        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            var (status, title) = ex switch
            {
                NotFoundException => (StatusCodes.Status404NotFound, "Not found"),
                ForbiddenException => (StatusCodes.Status403Forbidden, "Forbidden"),
                Application.Auth.TooManyRequestsException => (StatusCodes.Status429TooManyRequests, "Too many requests"),
                DomainException => (StatusCodes.Status409Conflict, "Business rule violation"),
                _ => (StatusCodes.Status500InternalServerError, "Unexpected error")
            };

            if (status == StatusCodes.Status500InternalServerError)
                _logger.LogError(ex, "Unhandled exception (correlation {CorrelationId})", correlationId);
            else
                _logger.LogWarning("{Title}: {Message} (correlation {CorrelationId})", title, ex.Message, correlationId);

            context.Response.StatusCode = status;
            context.Response.ContentType = "application/json";
            var payload = JsonSerializer.Serialize(new
            {
                title,
                status,
                detail = status == StatusCodes.Status500InternalServerError ? "An unexpected error occurred." : ex.Message,
                correlationId
            });
            await context.Response.WriteAsync(payload);
        }
    }
}
