using AbfuhrPilot.Domain.Common;
using AbfuhrPilot.Domain.Events;

namespace AbfuhrPilot.Application.Events;

/// <summary>
/// Maps internal domain events to published integration events. Events with no
/// mapping stay internal (e.g. StopStarted/StopSkipped are domain-only today —
/// a deliberate choice to keep the published contract small).
/// </summary>
public static class IntegrationEventMapper
{
    public static IntegrationEvent? Map(IDomainEvent e, string? correlationId) => e switch
    {
        RouteStartedDomainEvent x => new RouteStartedIntegrationEvent
        {
            EventType = "abfuhrpilot.operations.route-started",
            CompanyId = x.CompanyId, CorrelationId = correlationId, OccurredAtUtc = x.OccurredAtUtc,
            RouteId = x.RouteId, DriverId = x.DriverId, VehicleId = x.VehicleId,
        },
        RouteCompletedDomainEvent x => new RouteCompletedIntegrationEvent
        {
            EventType = "abfuhrpilot.operations.route-completed",
            CompanyId = x.CompanyId, CorrelationId = correlationId, OccurredAtUtc = x.OccurredAtUtc,
            RouteId = x.RouteId, DriverId = x.DriverId,
        },
        StopCompletedDomainEvent x => new StopCompletedIntegrationEvent
        {
            EventType = "abfuhrpilot.operations.stop-completed",
            CompanyId = x.CompanyId, CorrelationId = correlationId, OccurredAtUtc = x.OccurredAtUtc,
            StopId = x.StopId, RouteId = x.RouteId, CompletedAtUtc = x.CompletedAtUtc,
        },
        ProblemReportedDomainEvent x => new ProblemReportedIntegrationEvent
        {
            EventType = "abfuhrpilot.operations.problem-reported",
            CompanyId = x.CompanyId, CorrelationId = correlationId, OccurredAtUtc = x.OccurredAtUtc,
            StopId = x.StopId, RouteId = x.RouteId, ProblemType = x.ProblemType.ToString(),
        },
        OptimizedRouteAppliedDomainEvent x => new OptimizedRouteAppliedIntegrationEvent
        {
            EventType = "abfuhrpilot.operations.optimized-route-applied",
            CompanyId = x.CompanyId, CorrelationId = correlationId, OccurredAtUtc = x.OccurredAtUtc,
            RouteId = x.RouteId, StopCount = x.StopCount,
        },
        UserCreatedDomainEvent x => new UserCreatedIntegrationEvent
        {
            EventType = "abfuhrpilot.identity.user-created",
            CompanyId = x.CompanyId, CorrelationId = correlationId, OccurredAtUtc = x.OccurredAtUtc,
            UserId = x.UserId, Role = x.Role.ToString(),
        },
        UserDeactivatedDomainEvent x => new UserDeactivatedIntegrationEvent
        {
            EventType = "abfuhrpilot.identity.user-deactivated",
            CompanyId = x.CompanyId, CorrelationId = correlationId, OccurredAtUtc = x.OccurredAtUtc,
            UserId = x.UserId,
        },
        _ => null, // domain-only event — not published
    };
}
