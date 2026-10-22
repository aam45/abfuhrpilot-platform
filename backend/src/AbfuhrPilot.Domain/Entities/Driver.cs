using AbfuhrPilot.Domain.Common;

namespace AbfuhrPilot.Domain.Entities;

/// <summary>A truck driver. May optionally be linked to a login <see cref="User"/>.</summary>
public class Driver : BaseEntity, ICompanyOwned
{
    public Guid CompanyId { get; set; }
    public Guid? UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? LicenseNumber { get; set; }
    public bool IsActive { get; set; } = true;
}
