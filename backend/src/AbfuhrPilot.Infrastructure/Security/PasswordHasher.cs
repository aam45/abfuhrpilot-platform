using AbfuhrPilot.Application.Common;

namespace AbfuhrPilot.Infrastructure.Security;

/// <summary>BCrypt-based password hashing. Passwords are never stored in plain text.</summary>
public class PasswordHasher : IPasswordHasher
{
    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, workFactor: 11);
    public bool Verify(string password, string hash) => BCrypt.Net.BCrypt.Verify(password, hash);
}
