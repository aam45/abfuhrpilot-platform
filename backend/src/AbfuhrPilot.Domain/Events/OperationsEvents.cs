using AbfuhrPilot.Domain.Common;
using AbfuhrPilot.Domain.Enums;

namespace AbfuhrPilot.Domain.Events;

// OperationsContext domain events — raised by Route/Stop aggregates.

public sealed record RouteStartedDomainEvent(Guid CompanyId, Guid RouteId, Guid? DriverId, Guid? VehicleId)
    : DomainEvent(CompanyId);

public sealed record RouteCompletedDomainEvent(Guid CompanyId, Guid RouteId, Guid? DriverId)
    : DomainEvent(CompanyId);

public sealed record StopStartedDomainEvent(Guid CompanyId, Guid StopId, Guid RouteId)
    : DomainEvent(CompanyId);

public sealed record StopCompletedDomainEvent(Guid CompanyId, Guid StopId, Guid RouteId, DateTime CompletedAtUtc)
    : DomainEvent(CompanyId);

public sealed record StopSkippedDomainEvent(Guid CompanyId, Guid StopId, Guid RouteId)
    : DomainEvent(CompanyId);

public sealed record ProblemReportedDomainEvent(Guid CompanyId, Guid StopId, Guid RouteId, ProblemType ProblemType)
    : DomainEvent(CompanyId);

/// <summary>Raised when a confirmed (optimized or manual) stop order is applied to a route.</summary>
public sealed record OptimizedRouteAppliedDomainEvent(Guid CompanyId, Guid RouteId, int StopCount)
    : DomainEvent(CompanyId);
