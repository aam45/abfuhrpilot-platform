using System.Security.Cryptography;
using AbfuhrPilot.Domain.Common;

namespace AbfuhrPilot.Application.Auth;

/// <summary>
/// Throttles login attempts per identifier (email) to slow brute-force attacks.
/// Implemented in Infrastructure (in-memory for the MVP; swap for a distributed
/// store when scaling out).
/// </summary>
public interface ILoginThrottle
{
    /// <summary>Throws <see cref="TooManyRequestsException"/> if currently locked out.</summary>
    void EnsureNotLocked(string key);
    void RegisterFailure(string key);
    void Reset(string key);
}

/// <summary>Maps to HTTP 429 in the API layer.</summary>
public class TooManyRequestsException : Exception
{
    public TooManyRequestsException(string message) : base(message) { }
}

/// <summary>Minimum password policy applied whenever a password is set.</summary>
public static class PasswordPolicy
{
    public const int MinLength = 8;

    public static void Validate(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < MinLength)
            throw new DomainException($"Password must be at least {MinLength} characters long.");
        if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit))
            throw new DomainException("Password must contain at least one letter and one digit.");
    }
}

/// <summary>Sends transactional emails. The MVP implementation logs to the app log
/// ("dev-mode fake email"); swap for SMTP/Postmark/SES later.</summary>
public interface IEmailSender
{
    Task SendAsync(string toEmail, string subject, string body, CancellationToken ct = default);
}

/// <summary>Tunables for password-reset / email-verification tokens.</summary>
public class AuthTokenOptions
{
    public int PasswordResetHours { get; set; } = 2;
    public int EmailVerificationHours { get; set; } = 48;
    /// <summary>Dev/testing only: return the raw token in the API response. Never enable in prod.</summary>
    public bool ExposeTokensForDev { get; set; } = false;
}

/// <summary>Opaque refresh-token generation + hashing (SHA-256; only the hash is stored).</summary>
public static class RefreshTokenUtils
{
    public static string Generate() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        .Replace("+", "-").Replace("/", "_").TrimEnd('=');

    public static string Hash(string rawToken)
    {
        var bytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes);
    }
}
