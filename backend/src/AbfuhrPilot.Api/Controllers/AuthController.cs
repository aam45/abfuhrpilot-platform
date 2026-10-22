using AbfuhrPilot.Application.Common;
using AbfuhrPilot.Application.Dtos;
using AbfuhrPilot.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AbfuhrPilot.Api.Controllers;

// Anonymous endpoints below are split across 4 IP-partitioned rate-limit
// policies (see Program.cs) as defense-in-depth against request-volume abuse:
// auth-login (login), auth-account-write (register, request-password-reset,
// request-email-verification), auth-token-confirm (reset-password,
// verify-email), auth-session (refresh, logout). Splitting avoids one
// endpoint's traffic (e.g. drivers' refresh calls on a shared-NAT depot
// network) exhausting a bucket shared with unrelated auth traffic. Login
// additionally keeps its own account-lockout ILoginThrottle logic.
[ApiController]
[Route("api/v1/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AuthService _auth;
    public AuthController(AuthService auth) => _auth = auth;

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-login")]
    public Task<AuthResponse> Login(LoginRequest req, CancellationToken ct) => _auth.LoginAsync(req, ct);

    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-account-write")]
    public Task<AuthResponse> Register(RegisterRequest req, CancellationToken ct) => _auth.RegisterAsync(req, ct);

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-session")]
    public Task<AuthResponse> Refresh(RefreshRequest req, CancellationToken ct) => _auth.RefreshAsync(req, ct);

    [HttpPost("logout")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-session")]
    public async Task<IActionResult> Logout(LogoutRequest req, CancellationToken ct)
    {
        await _auth.LogoutAsync(req, ct);
        return NoContent();
    }

    [HttpPost("request-password-reset")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-account-write")]
    public Task<AuthActionResult> RequestPasswordReset(RequestPasswordResetRequest req, CancellationToken ct) =>
        _auth.RequestPasswordResetAsync(req, ct);

    [HttpPost("reset-password")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-token-confirm")]
    public async Task<IActionResult> ResetPassword(ResetPasswordWithTokenRequest req, CancellationToken ct)
    {
        await _auth.ResetPasswordAsync(req, ct);
        return NoContent();
    }

    [HttpPost("request-email-verification")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-account-write")]
    public Task<AuthActionResult> RequestEmailVerification(RequestEmailVerificationRequest req, CancellationToken ct) =>
        _auth.RequestEmailVerificationAsync(req, ct);

    [HttpPost("verify-email")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-token-confirm")]
    public async Task<IActionResult> VerifyEmail(VerifyEmailRequest req, CancellationToken ct)
    {
        await _auth.VerifyEmailAsync(req, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/v1")]
[Authorize]
public class MeController : ControllerBase
{
    private readonly AuthService _auth;
    private readonly ICurrentUser _me;
    public MeController(AuthService auth, ICurrentUser me) { _auth = auth; _me = me; }

    [HttpGet("me")]
    public Task<UserDto> Me(CancellationToken ct) => _auth.GetMeAsync(_me, ct);
}
