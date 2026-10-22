using AbfuhrPilot.Application.Auth;
using AbfuhrPilot.Application.Common;
using AbfuhrPilot.Application.Dtos;
using AbfuhrPilot.Domain.Common;
using AbfuhrPilot.Domain.Entities;
using AbfuhrPilot.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AbfuhrPilot.Application.Services;

public class AuthService
{
    private const int RefreshTokenDays = 14;

    private readonly IAppDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtTokenService _jwt;
    private readonly ILoginThrottle _throttle;
    private readonly IEmailSender _email;
    private readonly AuthTokenOptions _tokenOptions;

    public AuthService(IAppDbContext db, IPasswordHasher hasher, IJwtTokenService jwt, ILoginThrottle throttle,
        IEmailSender email, AuthTokenOptions tokenOptions)
    {
        _db = db;
        _hasher = hasher;
        _jwt = jwt;
        _throttle = throttle;
        _email = email;
        _tokenOptions = tokenOptions;
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest req, CancellationToken ct)
    {
        var email = req.Email.Trim().ToLower();
        _throttle.EnsureNotLocked(email); // brute-force lockout

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user is null || !user.IsActive || !_hasher.Verify(req.Password, user.PasswordHash))
        {
            _throttle.RegisterFailure(email);
            throw new DomainException("Invalid email or password.");
        }

        _throttle.Reset(email);
        var driverId = await _db.Drivers.Where(d => d.UserId == user.Id).Select(d => (Guid?)d.Id).FirstOrDefaultAsync(ct);
        return await BuildResponseAsync(user, driverId, ct);
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest req, CancellationToken ct)
    {
        var email = req.Email.Trim().ToLower();
        PasswordPolicy.Validate(req.Password);
        if (await _db.Users.AnyAsync(u => u.Email == email, ct))
            throw new DomainException("A user with this email already exists.");

        var company = new Company { Name = req.CompanyName };
        _db.Companies.Add(company);

        // The first user in a brand-new company workspace is its Admin.
        var user = new User
        {
            CompanyId = company.Id,
            FullName = req.FullName,
            Email = email,
            PasswordHash = _hasher.Hash(req.Password),
            Role = UserRole.Admin,
            IsActive = true,
            EmailVerified = false // self-registered → must verify (not enforced at login yet)
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        // Issue an email-verification token (logged as a fake email in dev).
        await IssueTokenAsync(user, UserTokenPurpose.EmailVerification, _tokenOptions.EmailVerificationHours,
            "E-Mail bestätigen", "verify-email", ct);

        return await BuildResponseAsync(user, null, ct);
    }

    // ---- Password reset ----
    public async Task<AuthActionResult> RequestPasswordResetAsync(RequestPasswordResetRequest req, CancellationToken ct)
    {
        var email = req.Email.Trim().ToLower();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email && u.IsActive, ct);
        string? raw = null;
        if (user is not null)
            raw = await IssueTokenAsync(user, UserTokenPurpose.PasswordReset, _tokenOptions.PasswordResetHours,
                "Passwort zurücksetzen", "reset-password", ct);
        // Same response whether or not the account exists (no account enumeration).
        return new AuthActionResult("Falls ein Konto existiert, wurde eine E-Mail zum Zurücksetzen gesendet.",
            _tokenOptions.ExposeTokensForDev ? raw : null);
    }

    public async Task ResetPasswordAsync(ResetPasswordWithTokenRequest req, CancellationToken ct)
    {
        PasswordPolicy.Validate(req.NewPassword);
        var token = await ConsumeTokenAsync(req.Token, UserTokenPurpose.PasswordReset, ct);
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == token.UserId, ct)
            ?? throw new DomainException("Invalid reset token.");

        user.PasswordHash = _hasher.Hash(req.NewPassword);
        user.Touch();
        await RevokeAllRefreshTokensAsync(user.Id, ct); // force re-login everywhere
        await _db.SaveChangesAsync(ct);
    }

    // ---- Email verification ----
    public async Task<AuthActionResult> RequestEmailVerificationAsync(RequestEmailVerificationRequest req, CancellationToken ct)
    {
        var email = req.Email.Trim().ToLower();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email && u.IsActive, ct);
        string? raw = null;
        if (user is { EmailVerified: false })
            raw = await IssueTokenAsync(user, UserTokenPurpose.EmailVerification, _tokenOptions.EmailVerificationHours,
                "E-Mail bestätigen", "verify-email", ct);
        return new AuthActionResult("Falls erforderlich, wurde eine Bestätigungs-E-Mail gesendet.",
            _tokenOptions.ExposeTokensForDev ? raw : null);
    }

    public async Task VerifyEmailAsync(VerifyEmailRequest req, CancellationToken ct)
    {
        var token = await ConsumeTokenAsync(req.Token, UserTokenPurpose.EmailVerification, ct);
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == token.UserId, ct)
            ?? throw new DomainException("Invalid verification token.");
        user.EmailVerified = true;
        user.Touch();
        await _db.SaveChangesAsync(ct);
    }

    private async Task<string> IssueTokenAsync(User user, UserTokenPurpose purpose, int expiryHours,
        string subject, string linkPath, CancellationToken ct)
    {
        var raw = RefreshTokenUtils.Generate();
        _db.UserTokens.Add(new UserToken
        {
            CompanyId = user.CompanyId,
            UserId = user.Id,
            Purpose = purpose,
            TokenHash = RefreshTokenUtils.Hash(raw),
            ExpiresAt = DateTime.UtcNow.AddHours(expiryHours),
        });
        await _db.SaveChangesAsync(ct);
        await _email.SendAsync(user.Email, subject,
            $"Öffnen Sie den folgenden Link: /{linkPath}?token={raw}", ct);
        return raw;
    }

    private async Task<UserToken> ConsumeTokenAsync(string raw, UserTokenPurpose purpose, CancellationToken ct)
    {
        var hash = RefreshTokenUtils.Hash(raw ?? string.Empty);
        var token = await _db.UserTokens.FirstOrDefaultAsync(t => t.TokenHash == hash && t.Purpose == purpose, ct);
        if (token is null || !token.IsActive)
            throw new DomainException("This link is invalid or has expired.");
        token.ConsumedAt = DateTime.UtcNow; // single-use
        return token;
    }

    private async Task RevokeAllRefreshTokensAsync(Guid userId, CancellationToken ct)
    {
        var active = await _db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAt == null).ToListAsync(ct);
        foreach (var t in active) t.RevokedAt = DateTime.UtcNow;
    }

    /// <summary>Rotates a refresh token: validates it, revokes it, issues a new pair.</summary>
    public async Task<AuthResponse> RefreshAsync(RefreshRequest req, CancellationToken ct)
    {
        var hash = RefreshTokenUtils.Hash(req.RefreshToken);
        var token = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (token is null || !token.IsActive)
            throw new DomainException("Invalid or expired refresh token.");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == token.UserId, ct);
        if (user is null || !user.IsActive)
            throw new DomainException("Account is no longer active.");

        var (raw, newToken) = CreateRefreshToken(user);
        token.RevokedAt = DateTime.UtcNow;          // rotation
        token.ReplacedByTokenHash = newToken.TokenHash;
        _db.RefreshTokens.Add(newToken);

        var driverId = await _db.Drivers.Where(d => d.UserId == user.Id).Select(d => (Guid?)d.Id).FirstOrDefaultAsync(ct);
        await _db.SaveChangesAsync(ct);
        return BuildResponse(user, driverId, raw, newToken.ExpiresAt);
    }

    /// <summary>Revokes a refresh token (logout). Idempotent — unknown tokens are ignored.</summary>
    public async Task LogoutAsync(LogoutRequest req, CancellationToken ct)
    {
        var hash = RefreshTokenUtils.Hash(req.RefreshToken);
        var token = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (token is { RevokedAt: null })
        {
            token.RevokedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task<UserDto> GetMeAsync(ICurrentUser current, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == current.UserId, ct)
                   ?? throw new NotFoundException("User not found.");
        return user.ToDto(current.DriverId);
    }

    private async Task<AuthResponse> BuildResponseAsync(User user, Guid? driverId, CancellationToken ct)
    {
        var (raw, refresh) = CreateRefreshToken(user);
        _db.RefreshTokens.Add(refresh);
        await _db.SaveChangesAsync(ct);
        return BuildResponse(user, driverId, raw, refresh.ExpiresAt);
    }

    private AuthResponse BuildResponse(User user, Guid? driverId, string refreshRaw, DateTime refreshExpiry)
    {
        var access = _jwt.CreateToken(user, driverId);
        return new AuthResponse(access.Token, access.ExpiresAt, refreshRaw, refreshExpiry, user.ToDto(driverId));
    }

    private static (string raw, RefreshToken token) CreateRefreshToken(User user)
    {
        var raw = RefreshTokenUtils.Generate();
        var token = new RefreshToken
        {
            CompanyId = user.CompanyId,
            UserId = user.Id,
            TokenHash = RefreshTokenUtils.Hash(raw),
            ExpiresAt = DateTime.UtcNow.AddDays(RefreshTokenDays)
        };
        return (raw, token);
    }
}
