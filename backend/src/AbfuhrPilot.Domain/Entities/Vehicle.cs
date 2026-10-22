using AbfuhrPilot.Domain.Common;
using AbfuhrPilot.Domain.Enums;

namespace AbfuhrPilot.Domain.Entities;

/// <summary>A collection vehicle (truck). Knows which waste categories it may carry.</summary>
public class Vehicle : BaseEntity, ICompanyOwned
{
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string LicensePlate { get; set; } = string.Empty;
    public VehicleType VehicleType { get; set; }
    public decimal? Capacity { get; set; }

    // Physical dimensions used for truck-aware routing (all optional; routing
    // falls back with explicit warnings when values are missing).
    public double? HeightMeters { get; set; }
    public double? WidthMeters { get; set; }
    public double? LengthMeters { get; set; }
    public double? WeightTons { get; set; }
    public double? MaxLoadTons { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Waste categories this vehicle is allowed to collect.</summary>
    public List<WasteCategory> AllowedWasteCategories { get; set; } = new();

    /// <summary>Business rule #2: a vehicle may only serve categories it is equipped for.</summary>
    public bool CanCollect(WasteCategory category) => AllowedWasteCategories.Contains(category);
}
