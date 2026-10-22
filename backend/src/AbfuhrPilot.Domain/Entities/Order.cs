using AbfuhrPilot.Domain.Common;
using AbfuhrPilot.Domain.Enums;

namespace AbfuhrPilot.Domain.Entities;

/// <summary>A customer request for a pickup / container / special order ("Auftrag").</summary>
public class Order : BaseEntity, ICompanyOwned
{
    public Guid CompanyId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public OrderType OrderType { get; set; }
    public Guid WasteTypeId { get; set; }
    public PickupType PickupType { get; set; }
    public Guid? ContainerId { get; set; }

    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerPhone { get; set; }
    public string Address { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public DateOnly? RequestedDate { get; set; }
    public DateOnly? ScheduledDate { get; set; }
    public Guid? AssignedRouteId { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.New;
    public string? Notes { get; set; }

    public void Schedule(DateOnly date)
    {
        if (Status is OrderStatus.Completed or OrderStatus.Cancelled or OrderStatus.Rejected)
            throw new DomainException($"A {Status} order cannot be scheduled.");
        ScheduledDate = date;
        Status = OrderStatus.Scheduled;
        Touch();
    }

    public void AssignToRoute(Guid routeId)
    {
        AssignedRouteId = routeId;
        Status = OrderStatus.Assigned;
        Touch();
    }

    public void Complete()
    {
        Status = OrderStatus.Completed;
        Touch();
    }

    public void Reject(string? reason)
    {
        Status = OrderStatus.Rejected;
        if (!string.IsNullOrWhiteSpace(reason))
            Notes = reason;
        Touch();
    }
}
