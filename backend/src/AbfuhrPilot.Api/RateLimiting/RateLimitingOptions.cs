namespace AbfuhrPilot.Api.RateLimiting;

/// <summary>
/// Configuration for the IP-partitioned fixed-window rate limiters applied to
/// anonymous <c>AuthController</c> endpoints. Split into four named policies
/// (login, account-write, token-confirm, session) instead of one shared
/// bucket so that, on shared-NAT depot/carrier networks, heavy traffic on one
/// endpoint (e.g. drivers' <c>refresh</c> calls) cannot exhaust the same
/// bucket and accidentally 429 unrelated legitimate auth traffic (e.g.
/// <c>login</c>, password reset) from the same IP. Login additionally keeps
/// its own account-lockout <see cref="AbfuhrPilot.Application.Auth.ILoginThrottle"/>
/// logic; these fixed-window limiters remain a coarser, per-IP
/// defense-in-depth layer against request-volume abuse.
/// </summary>
public class RateLimitingOptions
{
    /// <summary>Policy for <c>POST /login</c>.</summary>
    public RateLimitPolicyOptions AuthLogin { get; set; } = new() { PermitLimit = 10, WindowSeconds = 60 };

    /// <summary>Policy for <c>register</c>, <c>request-password-reset</c>, <c>request-email-verification</c>.</summary>
    public RateLimitPolicyOptions AuthAccountWrite { get; set; } = new() { PermitLimit = 5, WindowSeconds = 60 };

    /// <summary>Policy for <c>reset-password</c>, <c>verify-email</c>.</summary>
    public RateLimitPolicyOptions AuthTokenConfirm { get; set; } = new() { PermitLimit = 10, WindowSeconds = 60 };

    /// <summary>Policy for <c>refresh</c>, <c>logout</c>.</summary>
    public RateLimitPolicyOptions AuthSession { get; set; } = new() { PermitLimit = 30, WindowSeconds = 60 };
}

public class RateLimitPolicyOptions
{
    /// <summary>Requests allowed per <see cref="WindowSeconds"/> window, per client IP.</summary>
    public int PermitLimit { get; set; } = 10;

    /// <summary>Fixed-window length in seconds.</summary>
    public int WindowSeconds { get; set; } = 60;
}
