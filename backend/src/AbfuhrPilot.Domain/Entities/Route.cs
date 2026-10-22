using AbfuhrPilot.Domain.Common;
using AbfuhrPilot.Domain.Enums;

namespace AbfuhrPilot.Domain.Entities;

/// <summary>
/// A planned collection tour ("Tour"). Has exactly one main waste type,
/// one assigned vehicle and one assigned driver.
/// </summary>
public class Route : BaseEntity, ICompanyOwned
{
    public Guid CompanyId { get; set; }
    public string RouteNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateOnly Date { get; set; }

    public Guid WasteTypeId { get; set; }
    public Guid? VehicleId { get; set; }
    public Guid? DriverId { get; set; }
    public Guid? StartDepotId { get; set; }
    public Guid? EndDestinationId { get; set; }

    public RouteStatus Status { get; set; } = RouteStatus.Draft;

    public DateTime? PlannedStartTime { get; set; }
    public DateTime? PlannedEndTime { get; set; }
    public DateTime? ActualStartTime { get; set; }
    public DateTime? ActualEndTime { get; set; }

    public List<Stop> Stops { get; set; } = new();

    public bool IsStarted => Status == RouteStatus.InProgress || Status == RouteStatus.Completed;

    /// <summary>Rule #6: a driver may only start a route assigned to them.</summary>
    public void Start(Guid driverId)
    {
        if (DriverId != driverId)
            throw new DomainException("Only the assigned driver can start this route.");
        if (Status == RouteStatus.Completed || Status == RouteStatus.Cancelled)
            throw new DomainException($"A {Status} route cannot be started.");
        if (VehicleId is null)
            throw new DomainException("A route must have a vehicle assigned before it can start.");

        Status = RouteStatus.InProgress;
        ActualStartTime ??= DateTime.UtcNow;
        Touch();
        Raise(new Events.RouteStartedDomainEvent(CompanyId, Id, DriverId, VehicleId));
    }

    /// <summary>Rule #6: a driver may only complete a route assigned to them.</summary>
    public void Complete(Guid driverId)
    {
        if (DriverId != driverId)
            throw new DomainException("Only the assigned driver can complete this route.");
        if (Status != RouteStatus.InProgress)
            throw new DomainException("Only an in-progress route can be completed.");
        Status = RouteStatus.Completed;
        ActualEndTime = DateTime.UtcNow;
        Touch();
        Raise(new Events.RouteCompletedDomainEvent(CompanyId, Id, DriverId));
    }
}
