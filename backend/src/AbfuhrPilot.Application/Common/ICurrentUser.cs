using AbfuhrPilot.Domain.Enums;

namespace AbfuhrPilot.Application.Common;

/// <summary>Ambient information about the authenticated caller (from the JWT).</summary>
public interface ICurrentUser
{
    Guid UserId { get; }
    Guid CompanyId { get; }
    UserRole Role { get; }
    Guid? DriverId { get; }
    bool IsAuthenticated { get; }
}
