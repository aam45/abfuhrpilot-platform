using AbfuhrPilot.Domain.Common;
using AbfuhrPilot.Domain.Enums;

namespace AbfuhrPilot.Domain.Entities;

/// <summary>A single pickup location within a route ("Stopp").</summary>
public class Stop : BaseEntity, ICompanyOwned
{
    public Guid CompanyId { get; set; }
    public Guid RouteId { get; set; }
    public int SequenceNumber { get; set; }

    public string Address { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public Guid WasteTypeId { get; set; }
    public PickupType PickupType { get; set; }
    public Guid? ContainerId { get; set; }
    public string? CustomerName { get; set; }
    public string? Notes { get; set; }

    public StopStatus Status { get; set; } = StopStatus.Open;

    public DateTime? PlannedArrivalTime { get; set; }
    public DateTime? ActualArrivalTime { get; set; }
    public DateTime? CompletedAt { get; set; }

    public ProblemType? ProblemReason { get; set; }

    public void Start(bool routeStarted)
    {
        // Rule #7: a stop cannot be worked before the route is started.
        if (!routeStarted)
            throw new DomainException("The route must be started before working a stop.");
        if (Status is StopStatus.Done or StopStatus.Cancelled)
            throw new DomainException($"A {Status} stop cannot be started.");
        Status = StopStatus.InProgress;
        ActualArrivalTime ??= DateTime.UtcNow;
        Touch();
        Raise(new Events.StopStartedDomainEvent(CompanyId, Id, RouteId));
    }

    /// <summary>Rule #7 &amp; #8: only after the route is started; stores completion time.</summary>
    public void Complete(bool routeStarted)
    {
        if (!routeStarted)
            throw new DomainException("The route must be started before completing a stop.");
        if (Status is StopStatus.Cancelled)
            throw new DomainException("A cancelled stop cannot be completed.");
        Status = StopStatus.Done;
        CompletedAt = DateTime.UtcNow;
        ProblemReason = null;
        Touch();
        Raise(new Events.StopCompletedDomainEvent(CompanyId, Id, RouteId, CompletedAt.Value));
    }

    public void Skip(bool routeStarted)
    {
        if (!routeStarted)
            throw new DomainException("The route must be started before skipping a stop.");
        Status = StopStatus.Skipped;
        Touch();
        Raise(new Events.StopSkippedDomainEvent(CompanyId, Id, RouteId));
    }

    /// <summary>Rule #9: a problem stop must carry a problem reason.</summary>
    public void ReportProblem(ProblemType reason)
    {
        Status = StopStatus.Problem;
        ProblemReason = reason;
        Touch();
        Raise(new Events.ProblemReportedDomainEvent(CompanyId, Id, RouteId, reason));
    }
}
