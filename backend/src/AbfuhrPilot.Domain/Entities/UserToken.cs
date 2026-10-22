using AbfuhrPilot.Domain.Common;
using AbfuhrPilot.Domain.Enums;

namespace AbfuhrPilot.Domain.Entities;

/// <summary>
/// A single-use, expiring token for password reset or email verification.
/// Only a hash of the token is stored. Consumed tokens cannot be reused.
/// </summary>
public class UserToken : BaseEntity, ICompanyOwned
{   
    public Guid CompanyId { get; set; }
    public Guid UserId { get; set; }
    public UserTokenPurpose Purpose { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? ConsumedAt { get; set; }

    public bool IsActive => ConsumedAt is null && DateTime.UtcNow < ExpiresAt;
}
