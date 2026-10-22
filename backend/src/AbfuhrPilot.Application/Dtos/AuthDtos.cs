using AbfuhrPilot.Domain.Enums;

namespace AbfuhrPilot.Application.Dtos;

public record LoginRequest(string Email, string Password);

public record RegisterRequest(string CompanyName, string FullName, string Email, string Password);

public record RefreshRequest(string RefreshToken);

public record LogoutRequest(string RefreshToken);

public record RequestPasswordResetRequest(string Email);
public record ResetPasswordWithTokenRequest(string Token, string NewPassword);
public record RequestEmailVerificationRequest(string Email);
public record VerifyEmailRequest(string Token);

/// <summary>Generic result for the reset/verify request endpoints. <c>DevToken</c>
/// is populated only when <c>Auth:ExposeTokensForDev</c> is on (never in prod).</summary>
public record AuthActionResult(string Message, string? DevToken = null);

public record AuthResponse(string Token, DateTime ExpiresAt, string RefreshToken, DateTime RefreshExpiresAt, UserDto User);

public record UserDto(Guid Id, string FullName, string Email, UserRole Role, Guid CompanyId, Guid? DriverId, bool IsActive);
