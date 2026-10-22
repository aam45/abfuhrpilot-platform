using AbfuhrPilot.Domain.Common;
using AbfuhrPilot.Domain.Entities;

namespace AbfuhrPilot.Domain.Services;

/// <summary>
/// Pure domain logic for the waste/vehicle/route compatibility business rules
/// (#2, #3, #13, #14). Kept dependency-free so it is trivially unit-testable.
/// </summary>
public static class WasteCompatibilityService
{
    /// <summary>Rule #2: a vehicle must be allowed to collect the route's waste type.</summary>
    public static void EnsureVehicleCanServeRoute(Vehicle vehicle, WasteType routeWasteType)
    {
        if (!vehicle.CanCollect(routeWasteType.Category))
            throw new DomainException(
                $"Vehicle '{vehicle.Name}' is not allowed to collect '{routeWasteType.Name}'.");

        // Rule #14: special waste needs an explicitly special-vehicle-flagged truck.
        // (The explicit dispatcher confirmation is enforced at the application layer.)
    }

    /// <summary>
    /// Rule #3 &amp; #13: a stop's waste type must be compatible with the route's
    /// waste type. Types requiring separate collection can never be mixed.
    /// </summary>
    public static void EnsureStopMatchesRoute(WasteType routeWasteType, WasteType stopWasteType, bool allowMixed)
    {
        if (routeWasteType.Id == stopWasteType.Id)
            return;

        if (stopWasteType.RequiresSeparateCollection || routeWasteType.RequiresSeparateCollection)
            throw new DomainException(
                $"'{stopWasteType.Name}' requires separate collection and cannot be added to a '{routeWasteType.Name}' route.");

        if (stopWasteType.Category != routeWasteType.Category && !allowMixed)
            throw new DomainException(
                $"Stop waste type '{stopWasteType.Name}' does not match route waste type '{routeWasteType.Name}'.");
    }
}
