using AbfuhrPilot.Domain.Common;

namespace AbfuhrPilot.Domain.Entities;

/// <summary>
/// A persisted refresh token. Only a hash of the token is stored (never the raw
/// value). Rotation: refreshing revokes the old token and records the successor,
/// so a stolen-then-rotated token can be detected as reused.
/// </summary>
public class RefreshToken : BaseEntity, ICompanyOwned
{
    public Guid CompanyId { get; set; }
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? ReplacedByTokenHash { get; set; }

    public bool IsActive => RevokedAt is null && DateTime.UtcNow < ExpiresAt;
}
