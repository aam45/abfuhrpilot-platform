using AbfuhrPilot.Application.Dtos;
using AbfuhrPilot.Domain.Entities;

namespace AbfuhrPilot.Application.Common;

/// <summary>Hand-written entity → DTO mappers (no reflection, easy to reason about).</summary>
public static class Mapping
{
    public static UserDto ToDto(this User u, Guid? driverId = null) =>
        new(u.Id, u.FullName, u.Email, u.Role, u.CompanyId, driverId, u.IsActive);

    public static WasteTypeDto ToDto(this WasteType w) =>
        new(w.Id, w.Name, w.Code, w.Category, w.Description, w.RequiresSeparateCollection,
            w.RequiresSpecialVehicle, w.DefaultDestinationId, w.IsActive);

    public static VehicleDto ToDto(this Vehicle v) =>
        new(v.Id, v.Name, v.LicensePlate, v.VehicleType, v.Capacity, v.HeightMeters, v.WidthMeters,
            v.LengthMeters, v.WeightTons, v.MaxLoadTons, v.AllowedWasteCategories, v.IsActive);

    public static DriverDto ToDto(this Driver d) =>
        new(d.Id, d.UserId, d.FullName, d.PhoneNumber, d.LicenseNumber, d.IsActive);

    public static DestinationDto ToDto(this Destination d) =>
        new(d.Id, d.Name, d.Type, d.Address, d.Latitude, d.Longitude, d.OpeningHours,
            d.AcceptedWasteCategories, d.IsActive);

    public static ContainerDto ToDto(this Container c) =>
        new(c.Id, c.ContainerNumber, c.CurrentAddress, c.Latitude, c.Longitude, c.WasteTypeId,
            c.Size, c.Status, c.LastServicedAt);

    public static StopDto ToDto(this Stop s) =>
        new(s.Id, s.RouteId, s.SequenceNumber, s.Address, s.Latitude, s.Longitude, s.WasteTypeId,
            s.PickupType, s.ContainerId, s.CustomerName, s.Notes, s.Status, s.PlannedArrivalTime,
            s.ActualArrivalTime, s.CompletedAt, s.ProblemReason);

    public static OrderDto ToDto(this Order o) =>
        new(o.Id, o.OrderNumber, o.OrderType, o.WasteTypeId, o.PickupType, o.ContainerId, o.CustomerName,
            o.CustomerPhone, o.Address, o.Latitude, o.Longitude, o.RequestedDate, o.ScheduledDate,
            o.AssignedRouteId, o.Status, o.Notes);

    public static RouteDto ToDto(this Route r, string? wasteTypeName = null, string? vehicleName = null, string? driverName = null)
    {
        var stops = r.Stops ?? new List<Stop>();
        return new RouteDto(r.Id, r.RouteNumber, r.Name, r.Date, r.WasteTypeId, wasteTypeName, r.VehicleId, vehicleName,
            r.DriverId, driverName, r.StartDepotId, r.EndDestinationId, r.Status, r.PlannedStartTime, r.PlannedEndTime,
            r.ActualStartTime, r.ActualEndTime, stops.Count,
            stops.Count(s => s.Status == Domain.Enums.StopStatus.Done),
            stops.Count(s => s.Status == Domain.Enums.StopStatus.Problem));
    }
}
