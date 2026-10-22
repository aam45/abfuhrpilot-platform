using AbfuhrPilot.Application.Routing;

namespace AbfuhrPilot.Application.Contracts;

// ============================================================================
// Module contracts — the ONLY sanctioned way one bounded context reads another.
// Today they are in-process implementations over the shared database (modular
// monolith); when a context is extracted into a microservice, the same
// interface is re-implemented as an HTTP/gRPC client without touching callers.
// ============================================================================

/// <summary>FleetContext read API (vehicles) consumed by RoutingContext.</summary>
public interface IFleetReadService
{
    /// <summary>Routing profile for a vehicle (company-scoped), or null if not found.</summary>
    Task<VehicleRoutingProfile?> GetVehicleRoutingProfileAsync(Guid vehicleId, CancellationToken ct);
}

/// <summary>WasteContext + FleetContext compatibility query consumed by RoutingContext.</summary>
public interface IWasteCompatibilityService
{
    /// <summary>True when the vehicle's allowed categories include the waste type's category.</summary>
    Task<bool> VehicleCanCollectWasteAsync(Guid vehicleId, Guid wasteTypeId, CancellationToken ct);
}
