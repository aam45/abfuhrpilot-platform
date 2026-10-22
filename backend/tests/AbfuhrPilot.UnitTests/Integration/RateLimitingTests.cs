using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using AbfuhrPilot.Application.Dtos;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AbfuhrPilot.UnitTests.Integration;

/// <summary>
/// HTTP-level pins for the 4 IP-partitioned rate limiter policies (wired in
/// Program.cs) applied to anonymous AuthController endpoints — split from a
/// single shared "auth-write" policy, so that heavy traffic on one endpoint (e.g. drivers'
/// refresh calls on a shared-NAT depot network) cannot exhaust a bucket
/// shared with unrelated auth traffic (e.g. login, password reset) from the
/// same IP. Each test creates its own <see cref="LowLimitFactory"/> instance
/// (not shared via IClassFixture) so the in-memory fixed-window partition
/// state — which is keyed on the constant "unknown" partition key under
/// ASP.NET Core's in-memory TestServer, since HttpContext.Connection.RemoteIpAddress
/// is always null there — can't leak between test methods. A shared fixture
/// would let one test's exhausted bucket silently pass or fail a later test
/// instead of independently proving the limiter's behavior.
/// </summary>
public class RateLimitingTests
{
    [Fact]
    public async Task Requests_beyond_the_configured_limit_return_429_with_frozen_error_shape()
    {
        await using var factory = new LowLimitFactory();
        var client = factory.CreateClient();
        var req = new RequestPasswordResetRequest("nobody@test.local");

        // request-password-reset uses the "auth-account-write" policy.
        // LowLimitFactory configures its PermitLimit=2, so the 3rd call in
        // the window must be rejected.
        var first = await client.PostAsJsonAsync("api/v1/auth/request-password-reset", req);
        var second = await client.PostAsJsonAsync("api/v1/auth/request-password-reset", req);
        var third = await client.PostAsJsonAsync("api/v1/auth/request-password-reset", req);

        first.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
        second.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
        third.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

        third.Headers.TryGetValues("Retry-After", out var retryAfter).Should().BeTrue();
        retryAfter!.Should().ContainSingle();

        var json = await third.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        root.GetProperty("title").GetString().Should().Be("Too many requests");
        root.GetProperty("status").GetInt32().Should().Be(429);
        root.GetProperty("detail").GetString().Should().NotBeNullOrEmpty();
        root.GetProperty("correlationId").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Login_is_isolated_from_the_auth_account_write_rate_limit_policy()
    {
        await using var factory = new LowLimitFactory();
        var client = factory.CreateClient();
        var resetReq = new RequestPasswordResetRequest("nobody@test.local");

        // Exhaust the "auth-account-write" bucket (PermitLimit=2 in
        // LowLimitFactory) via request-password-reset.
        var first = await client.PostAsJsonAsync("api/v1/auth/request-password-reset", resetReq);
        var second = await client.PostAsJsonAsync("api/v1/auth/request-password-reset", resetReq);
        var third = await client.PostAsJsonAsync("api/v1/auth/request-password-reset", resetReq);

        first.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
        second.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
        third.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

        // A subsequent login call, in the same window and from the same
        // (test-server) IP partition key, draws from the separate
        // "auth-login" policy and must NOT be rejected — proving the two
        // policies no longer share a bucket, which is the fix for the
        // flagged shared-NAT accidental-lockout risk.
        var loginReq = new LoginRequest("nobody@test.local", "wrong-password");
        var loginResponse = await client.PostAsJsonAsync("api/v1/auth/login", loginReq);

        loginResponse.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Retry_After_reflects_the_actual_rejected_policys_own_window_not_a_hardcoded_default()
    {
        // "auth-account-write" (request-password-reset) and "auth-login" (login) are
        // given deliberately different windows here so a Retry-After bug that always
        // reports "auth-login"'s window would make this test fail for the first assertion.
        await using var factory = new DivergentWindowFactory();
        var client = factory.CreateClient();
        var resetReq = new RequestPasswordResetRequest("nobody@test.local");

        await client.PostAsJsonAsync("api/v1/auth/request-password-reset", resetReq);
        await client.PostAsJsonAsync("api/v1/auth/request-password-reset", resetReq);
        var accountWriteRejected = await client.PostAsJsonAsync("api/v1/auth/request-password-reset", resetReq);

        accountWriteRejected.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        accountWriteRejected.Headers.TryGetValues("Retry-After", out var accountWriteRetryAfter).Should().BeTrue();
        accountWriteRetryAfter!.Should().ContainSingle().Which.Should().Be("120");

        // Both permitted login calls fail with 409 (invalid credentials, not a
        // rate-limit rejection) before the 3rd call is rejected by the limiter itself.
        var loginReq = new LoginRequest("nobody@test.local", "wrong-password");
        await client.PostAsJsonAsync("api/v1/auth/login", loginReq);
        await client.PostAsJsonAsync("api/v1/auth/login", loginReq);
        var loginRejected = await client.PostAsJsonAsync("api/v1/auth/login", loginReq);

        loginRejected.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        loginRejected.Headers.TryGetValues("Retry-After", out var loginRetryAfter).Should().BeTrue();
        loginRetryAfter!.Should().ContainSingle().Which.Should().Be("45");
    }

    /// <summary>Same pipeline as <see cref="WebApiFactory"/> but with a low, fast,
    /// test-only "auth-account-write" permit limit instead of the production default,
    /// leaving the other 3 policies at their production defaults.</summary>
    public class LowLimitFactory : WebApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RateLimiting:AuthAccountWrite:PermitLimit"] = "2",
                    ["RateLimiting:AuthAccountWrite:WindowSeconds"] = "60",
                });
            });
        }
    }

    /// <summary>Same pipeline as <see cref="WebApiFactory"/>, but with low permit limits
    /// and deliberately divergent windows for "auth-account-write" and "auth-login" so a
    /// test can prove Retry-After is resolved per-policy rather than always reporting
    /// "auth-login"'s window.</summary>
    public class DivergentWindowFactory : WebApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RateLimiting:AuthAccountWrite:PermitLimit"] = "2",
                    ["RateLimiting:AuthAccountWrite:WindowSeconds"] = "120",
                    ["RateLimiting:AuthLogin:PermitLimit"] = "2",
                    ["RateLimiting:AuthLogin:WindowSeconds"] = "45",
                });
            });
        }
    }
}
