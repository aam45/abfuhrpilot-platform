using AbfuhrPilot.Domain.Common;
using AbfuhrPilot.Domain.Enums;

namespace AbfuhrPilot.Domain.Entities;

/// <summary>An authenticatable person in a company workspace.</summary>
public class User : BaseEntity, ICompanyOwned
{
    public Guid CompanyId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Viewer;
    public bool IsActive { get; set; } = true;

    /// <summary>Defaults to true (seeded/admin-created users). Self-registration sets it false
    /// and issues a verification token. Not yet enforced at login (foundation).</summary>
    public bool EmailVerified { get; set; } = true;
}
