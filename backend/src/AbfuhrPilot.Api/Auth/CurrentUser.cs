using System.Security.Claims;
using AbfuhrPilot.Application.Common;
using AbfuhrPilot.Domain.Enums;
using AbfuhrPilot.Infrastructure.Security;

namespace AbfuhrPilot.Api.Auth;

/// <summary>Reads the authenticated caller's identity from the JWT claims.</summary>
public class CurrentUser : ICurrentUser
{
    private readonly ClaimsPrincipal? _principal;

    public CurrentUser(IHttpContextAccessor accessor) => _principal = accessor.HttpContext?.User;

    public bool IsAuthenticated => _principal?.Identity?.IsAuthenticated ?? false;

    public Guid UserId => GetGuid(ClaimTypes.NameIdentifier)
                          ?? GetGuid("sub")
                          ?? Guid.Empty;

    public Guid CompanyId => GetGuid(AppClaims.CompanyId) ?? Guid.Empty;

    public Guid? DriverId => GetGuid(AppClaims.DriverId);

    public UserRole Role =>
        Enum.TryParse<UserRole>(_principal?.FindFirstValue(ClaimTypes.Role), out var r) ? r : UserRole.Viewer;

    private Guid? GetGuid(string type) =>
        Guid.TryParse(_principal?.FindFirstValue(type), out var g) ? g : null;
}
