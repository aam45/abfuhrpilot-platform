using AbfuhrPilot.Domain.Enums;

namespace AbfuhrPilot.Application.Dtos;

// UserDto itself lives in AuthDtos.cs (shared with the auth responses).

public record CreateUserRequest(string FullName, string Email, string Password, UserRole Role);

public record UpdateUserRequest(string FullName, UserRole Role, bool IsActive);

public record ResetPasswordRequest(string NewPassword);
