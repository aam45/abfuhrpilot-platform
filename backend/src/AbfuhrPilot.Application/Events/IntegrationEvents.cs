namespace AbfuhrPilot.Application.Events;

/// <summary>
/// Base for integration events — the contract future microservices consume.
/// Distinct from domain events: domain events are internal to a context;
/// integration events are the published, versioned, cross-service shape.
/// They are written to the outbox in the same transaction as the state change
/// and never contain secrets (passwords, hashes, tokens).
/// </summary>
public abstract record IntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    /// <summary>Fully-qualified stable type name, e.g. "abfuhrpilot.operations.route-started".</summary>
    public string EventType { get; init; } = string.Empty;
    public DateTime OccurredAtUtc { get; init; } = DateTime.UtcNow;
    public Guid CompanyId { get; init; }
    public string? CorrelationId { get; init; }
    public int SchemaVersion { get; init; } = 1;
}

// ---- Operations ----
public sealed record RouteStartedIntegrationEvent : IntegrationEvent
{
    public Guid RouteId { get; init; }
    public Guid? DriverId { get; init; }
    public Guid? VehicleId { get; init; }
}

public sealed record RouteCompletedIntegrationEvent : IntegrationEvent
{
    public Guid RouteId { get; init; }
    public Guid? DriverId { get; init; }
}

public sealed record StopCompletedIntegrationEvent : IntegrationEvent
{
    public Guid StopId { get; init; }
    public Guid RouteId { get; init; }
    public DateTime CompletedAtUtc { get; init; }
}

public sealed record ProblemReportedIntegrationEvent : IntegrationEvent
{
    public Guid StopId { get; init; }
    public Guid RouteId { get; init; }
    public string ProblemType { get; init; } = string.Empty;
}

public sealed record OptimizedRouteAppliedIntegrationEvent : IntegrationEvent
{
    public Guid RouteId { get; init; }
    public int StopCount { get; init; }
}

// ---- Identity ----
public sealed record UserCreatedIntegrationEvent : IntegrationEvent
{
    public Guid UserId { get; init; }
    public string Role { get; init; } = string.Empty;
}

public sealed record UserDeactivatedIntegrationEvent : IntegrationEvent
{
    public Guid UserId { get; init; }
}
