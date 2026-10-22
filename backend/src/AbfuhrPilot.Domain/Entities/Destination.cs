using AbfuhrPilot.Domain.Common;
using AbfuhrPilot.Domain.Enums;

namespace AbfuhrPilot.Domain.Entities;

/// <summary>A depot, recycling center, landfill, transfer station or customer site.</summary>
public class Destination : BaseEntity, ICompanyOwned
{
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DestinationType Type { get; set; }
    public string? Address { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? OpeningHours { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Waste categories this destination accepts.</summary>
    public List<WasteCategory> AcceptedWasteCategories { get; set; } = new();

    public bool Accepts(WasteCategory category) => AcceptedWasteCategories.Contains(category);
}
