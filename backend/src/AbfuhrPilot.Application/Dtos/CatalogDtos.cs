using AbfuhrPilot.Domain.Enums;

namespace AbfuhrPilot.Application.Dtos;

// ---- WasteType ----
public record WasteTypeDto(Guid Id, string Name, string Code, WasteCategory Category, string? Description,
    bool RequiresSeparateCollection, bool RequiresSpecialVehicle, Guid? DefaultDestinationId, bool IsActive);

public record WasteTypeUpsert(string Name, string Code, WasteCategory Category, string? Description,
    bool RequiresSeparateCollection, bool RequiresSpecialVehicle, Guid? DefaultDestinationId, bool IsActive = true);

// ---- Vehicle ----
public record VehicleDto(Guid Id, string Name, string LicensePlate, VehicleType VehicleType, decimal? Capacity,
    double? HeightMeters, double? WidthMeters, double? LengthMeters, double? WeightTons, double? MaxLoadTons,
    List<WasteCategory> AllowedWasteCategories, bool IsActive);

public record VehicleUpsert(string Name, string LicensePlate, VehicleType VehicleType, decimal? Capacity,
    double? HeightMeters, double? WidthMeters, double? LengthMeters, double? WeightTons, double? MaxLoadTons,
    List<WasteCategory> AllowedWasteCategories, bool IsActive = true);

// ---- Driver ----
public record DriverDto(Guid Id, Guid? UserId, string FullName, string? PhoneNumber, string? LicenseNumber, bool IsActive);

public record DriverUpsert(string FullName, string? PhoneNumber, string? LicenseNumber, Guid? UserId, bool IsActive = true);

// ---- Destination ----
public record DestinationDto(Guid Id, string Name, DestinationType Type, string? Address, double? Latitude,
    double? Longitude, string? OpeningHours, List<WasteCategory> AcceptedWasteCategories, bool IsActive);

public record DestinationUpsert(string Name, DestinationType Type, string? Address, double? Latitude,
    double? Longitude, string? OpeningHours, List<WasteCategory> AcceptedWasteCategories, bool IsActive = true);

// ---- Container ----
public record ContainerDto(Guid Id, string ContainerNumber, string? CurrentAddress, double? Latitude, double? Longitude,
    Guid WasteTypeId, string? Size, ContainerStatus Status, DateTime? LastServicedAt);

public record ContainerUpsert(string ContainerNumber, string? CurrentAddress, double? Latitude, double? Longitude,
    Guid WasteTypeId, string? Size, ContainerStatus Status = ContainerStatus.Available, DateTime? LastServicedAt = null);
