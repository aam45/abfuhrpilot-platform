using AbfuhrPilot.Domain.Common;
using AbfuhrPilot.Domain.Enums;

namespace AbfuhrPilot.Domain.Entities;

/// <summary>A physical waste container that is delivered, exchanged and picked up.</summary>
public class Container : BaseEntity, ICompanyOwned
{
    public Guid CompanyId { get; set; }

    /// <summary>Rule #5: must be unique within a company (enforced by a DB index).</summary>
    public string ContainerNumber { get; set; } = string.Empty;

    public string? CurrentAddress { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public Guid WasteTypeId { get; set; }
    public string? Size { get; set; }
    public ContainerStatus Status { get; set; } = ContainerStatus.Available;
    public DateTime? LastServicedAt { get; set; }
}
