using AbfuhrPilot.Application.Common;
using AbfuhrPilot.Application.Routing;
using AbfuhrPilot.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AbfuhrPilot.Application.Contracts;

/// <summary>In-process FleetContext read implementation (modular monolith).</summary>
public class FleetReadService : IFleetReadService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _me;
    public FleetReadService(IAppDbContext db, ICurrentUser me) { _db = db; _me = me; }

    public async Task<VehicleRoutingProfile?> GetVehicleRoutingProfileAsync(Guid vehicleId, CancellationToken ct)
    {
        var v = await _db.Vehicles.FirstOrDefaultAsync(x => x.Id == vehicleId && x.CompanyId == _me.CompanyId, ct);
        if (v is null) return null;

        return new VehicleRoutingProfile
        {
            VehicleType = v.VehicleType.ToString(),
            HeightMeters = v.HeightMeters,
            WidthMeters = v.WidthMeters,
            LengthMeters = v.LengthMeters,
            WeightTons = v.WeightTons,
            MaxLoadTons = v.MaxLoadTons,
            IsContainerTruck = v.VehicleType == VehicleType.ContainerTruck,
            RequiresTruckRouting = true, // every Müllwagen route is a truck route
            AllowedWasteCategories = v.AllowedWasteCategories.Select(c => c.ToString()).ToList(),
        };
    }
}

/// <summary>In-process Waste/Fleet compatibility implementation (modular monolith).</summary>
public class WasteCompatibilityQueryService : IWasteCompatibilityService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _me;
    public WasteCompatibilityQueryService(IAppDbContext db, ICurrentUser me) { _db = db; _me = me; }

    public async Task<bool> VehicleCanCollectWasteAsync(Guid vehicleId, Guid wasteTypeId, CancellationToken ct)
    {
        var vehicle = await _db.Vehicles.FirstOrDefaultAsync(v => v.Id == vehicleId && v.CompanyId == _me.CompanyId, ct);
        var waste = await _db.WasteTypes.FirstOrDefaultAsync(w => w.Id == wasteTypeId && w.CompanyId == _me.CompanyId, ct);
        if (vehicle is null || waste is null) return false;
        return vehicle.CanCollect(waste.Category);
    }
}
