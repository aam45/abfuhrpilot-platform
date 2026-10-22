namespace AbfuhrPilot.Application.Common;

/// <summary>
/// Exposes the current request's correlation id so it can flow into outbox
/// messages / integration events. Implemented in the API layer from the
/// middleware-generated id; null outside an HTTP request (tests, workers).
/// </summary>
public interface ICorrelationIdProvider
{
    string? CorrelationId { get; }
}
