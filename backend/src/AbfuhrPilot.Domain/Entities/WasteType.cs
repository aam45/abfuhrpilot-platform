using AbfuhrPilot.Domain.Common;
using AbfuhrPilot.Domain.Enums;

namespace AbfuhrPilot.Domain.Entities;

/// <summary>
/// A company-configurable waste type. Each maps onto a fixed
/// <see cref="WasteCategory"/> so vehicles and destinations can validate against it.
/// </summary>
public class WasteType : BaseEntity, ICompanyOwned
{
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public WasteCategory Category { get; set; }
    public string? Description { get; set; }

    /// <summary>Rule #13: must not be mixed into a route of a different waste type.</summary>
    public bool RequiresSeparateCollection { get; set; }

    /// <summary>Rule #14: needs an explicitly equipped vehicle + confirmation.</summary>
    public bool RequiresSpecialVehicle { get; set; }

    public Guid? DefaultDestinationId { get; set; }
    public bool IsActive { get; set; } = true;
}
