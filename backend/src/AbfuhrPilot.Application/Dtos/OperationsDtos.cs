using AbfuhrPilot.Domain.Enums;

namespace AbfuhrPilot.Application.Dtos;

// ---- Route ----
public record RouteDto(Guid Id, string RouteNumber, string Name, DateOnly Date, Guid WasteTypeId, string? WasteTypeName,
    Guid? VehicleId, string? VehicleName, Guid? DriverId, string? DriverName, Guid? StartDepotId, Guid? EndDestinationId,
    RouteStatus Status, DateTime? PlannedStartTime, DateTime? PlannedEndTime, DateTime? ActualStartTime,
    DateTime? ActualEndTime, int StopCount, int DoneCount, int ProblemCount);

public record RouteUpsert(string RouteNumber, string Name, DateOnly Date, Guid WasteTypeId, Guid? VehicleId,
    Guid? DriverId, Guid? StartDepotId, Guid? EndDestinationId, DateTime? PlannedStartTime, DateTime? PlannedEndTime);

public record AssignRouteRequest(Guid? DriverId, Guid? VehicleId, bool ConfirmSpecialWaste = false);

// ---- Stop ----
public record StopDto(Guid Id, Guid RouteId, int SequenceNumber, string Address, double? Latitude, double? Longitude,
    Guid WasteTypeId, PickupType PickupType, Guid? ContainerId, string? CustomerName, string? Notes, StopStatus Status,
    DateTime? PlannedArrivalTime, DateTime? ActualArrivalTime, DateTime? CompletedAt, ProblemType? ProblemReason);

public record StopUpsert(int SequenceNumber, string Address, double? Latitude, double? Longitude, Guid WasteTypeId,
    PickupType PickupType, Guid? ContainerId, string? CustomerName, string? Notes, DateTime? PlannedArrivalTime,
    bool AllowMixedWaste = false);

public record ReportProblemRequest(ProblemType ProblemType, string? Description, List<Guid>? PhotoIds = null);

public record ProblemPhotoDto(Guid Id, string Url, string FileName, string ContentType, long SizeBytes);

/// <summary>New stop order for a route: the full list of stop ids in the desired sequence.</summary>
public record ReorderStopsRequest(List<Guid> StopIds);

// ---- Order ----
public record OrderDto(Guid Id, string OrderNumber, OrderType OrderType, Guid WasteTypeId, PickupType PickupType,
    Guid? ContainerId, string CustomerName, string? CustomerPhone, string Address, double? Latitude, double? Longitude,
    DateOnly? RequestedDate, DateOnly? ScheduledDate, Guid? AssignedRouteId, OrderStatus Status, string? Notes);

public record OrderUpsert(string OrderNumber, OrderType OrderType, Guid WasteTypeId, PickupType PickupType,
    Guid? ContainerId, string CustomerName, string? CustomerPhone, string Address, double? Latitude, double? Longitude,
    DateOnly? RequestedDate, string? Notes);

public record ScheduleOrderRequest(DateOnly ScheduledDate);
public record AssignOrderToRouteRequest(Guid RouteId);
public record RejectOrderRequest(string? Reason);

// ---- Dashboard ----
public record OperationsDashboardDto(int RoutesToday, int ActiveRoutes, int CompletedStops, int OpenStops,
    int ProblemStops, int TotalStops, int OpenOrders, int VehiclesInUse, List<RouteDto> TodaysRoutes);

public record DriverDashboardDto(RouteDto? TodaysRoute, StopDto? NextStop, List<StopDto> Stops, int DoneCount, int TotalCount);
