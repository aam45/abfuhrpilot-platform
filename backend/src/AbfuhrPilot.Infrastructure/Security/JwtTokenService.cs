using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AbfuhrPilot.Application.Common;
using AbfuhrPilot.Domain.Entities;
using Microsoft.IdentityModel.Tokens;

namespace AbfuhrPilot.Infrastructure.Security;

public class JwtOptions
{
    public string Issuer { get; set; } = "AbfuhrPilot";
    public string Audience { get; set; } = "AbfuhrPilot";
    public string SecretKey { get; set; } = string.Empty;
    /// <summary>Access-token lifetime in minutes. Short-lived; sessions live on via refresh tokens.</summary>
    public int AccessTokenMinutes { get; set; } = 60;
}

/// <summary>Custom JWT claim names used across the app.</summary>
public static class AppClaims
{
    public const string CompanyId = "company_id";
    public const string DriverId = "driver_id";
}

public class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _options;
    public JwtTokenService(JwtOptions options) => _options = options;

    public AccessToken CreateToken(User user, Guid? driverId)
    {
        var expiresAt = DateTime.UtcNow.AddMinutes(_options.AccessTokenMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Role, user.Role.ToString()),
            new(AppClaims.CompanyId, user.CompanyId.ToString())
        };
        if (driverId is not null)
            claims.Add(new Claim(AppClaims.DriverId, driverId.Value.ToString()));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SecretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: creds);
        return new AccessToken(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
