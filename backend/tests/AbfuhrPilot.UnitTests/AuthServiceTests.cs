using AbfuhrPilot.Application.Auth;
using AbfuhrPilot.Application.Common;
using AbfuhrPilot.Application.Dtos;
using AbfuhrPilot.Application.Services;
using AbfuhrPilot.Domain.Common;
using AbfuhrPilot.Domain.Entities;
using AbfuhrPilot.Infrastructure.Security;
using FluentAssertions;
using Xunit;

namespace AbfuhrPilot.UnitTests;

public class AuthServiceTests
{
    private sealed class StubJwt : IJwtTokenService
    {
        public AccessToken CreateToken(User user, Guid? driverId) =>
            new($"token-{user.Id}", DateTime.UtcNow.AddMinutes(60));
    }

    private sealed class FakeEmail : IEmailSender
    {
        public Task SendAsync(string to, string subject, string body, CancellationToken ct = default) => Task.CompletedTask;
    }

    private static AuthService NewService(out ILoginThrottle throttle)
    {
        var db = TestDb.Create();
        throttle = new InMemoryLoginThrottle();
        var opts = new AuthTokenOptions { ExposeTokensForDev = true };
        return new AuthService(db, new PasswordHasher(), new StubJwt(), throttle, new FakeEmail(), opts);
    }

    private static RegisterRequest Reg(string password = "Passw0rd!") =>
        new("Demo GmbH", "Anna Admin", "admin@x.de", password);

    [Fact]
    public async Task Register_rejects_password_that_violates_policy()
    {
        var svc = NewService(out _);
        var act = async () => await svc.RegisterAsync(Reg("short"), default);
        await act.Should().ThrowAsync<DomainException>().WithMessage("*at least*");
    }

    [Fact]
    public async Task Register_issues_access_and_refresh_tokens()
    {
        var svc = NewService(out _);
        var res = await svc.RegisterAsync(Reg(), default);
        res.Token.Should().NotBeNullOrEmpty();
        res.RefreshToken.Should().NotBeNullOrEmpty();
        res.RefreshExpiresAt.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task Refresh_rotates_token_and_invalidates_the_old_one()
    {
        var svc = NewService(out _);
        var first = await svc.RegisterAsync(Reg(), default);

        var rotated = await svc.RefreshAsync(new RefreshRequest(first.RefreshToken), default);
        rotated.RefreshToken.Should().NotBe(first.RefreshToken);

        // Old token is now revoked → reuse must fail (rotation/theft detection).
        var reuseOld = async () => await svc.RefreshAsync(new RefreshRequest(first.RefreshToken), default);
        await reuseOld.Should().ThrowAsync<DomainException>();

        // New token still works.
        var again = await svc.RefreshAsync(new RefreshRequest(rotated.RefreshToken), default);
        again.RefreshToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Logout_revokes_the_refresh_token()
    {
        var svc = NewService(out _);
        var res = await svc.RegisterAsync(Reg(), default);

        await svc.LogoutAsync(new LogoutRequest(res.RefreshToken), default);

        var act = async () => await svc.RefreshAsync(new RefreshRequest(res.RefreshToken), default);
        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task Login_locks_out_after_repeated_failures()
    {
        var svc = NewService(out _);
        await svc.RegisterAsync(Reg(), default);

        for (var i = 0; i < 5; i++)
        {
            var bad = async () => await svc.LoginAsync(new LoginRequest("admin@x.de", "wrong-pass-1"), default);
            await bad.Should().ThrowAsync<DomainException>();
        }

        var locked = async () => await svc.LoginAsync(new LoginRequest("admin@x.de", "Passw0rd!"), default);
        await locked.Should().ThrowAsync<TooManyRequestsException>();
    }

    [Fact]
    public async Task Password_reset_flow_sets_new_password_and_is_single_use()
    {
        var svc = NewService(out _);
        await svc.RegisterAsync(Reg(), default);

        var request = await svc.RequestPasswordResetAsync(new RequestPasswordResetRequest("admin@x.de"), default);
        request.DevToken.Should().NotBeNullOrEmpty();

        await svc.ResetPasswordAsync(new ResetPasswordWithTokenRequest(request.DevToken!, "NewPass123"), default);

        // Old password no longer works; new one does.
        var oldPw = async () => await svc.LoginAsync(new LoginRequest("admin@x.de", "Passw0rd!"), default);
        await oldPw.Should().ThrowAsync<DomainException>();
        (await svc.LoginAsync(new LoginRequest("admin@x.de", "NewPass123"), default)).Token.Should().NotBeNullOrEmpty();

        // Token cannot be reused.
        var reuse = async () => await svc.ResetPasswordAsync(new ResetPasswordWithTokenRequest(request.DevToken!, "Another123"), default);
        await reuse.Should().ThrowAsync<DomainException>().WithMessage("*invalid or has expired*");
    }

    [Fact]
    public async Task Password_reset_does_not_reveal_whether_the_account_exists()
    {
        var svc = NewService(out _);
        var res = await svc.RequestPasswordResetAsync(new RequestPasswordResetRequest("nobody@x.de"), default);
        res.Message.Should().NotBeNullOrEmpty();
        res.DevToken.Should().BeNull(); // no token issued for a non-existent account
    }

    [Fact]
    public async Task Self_registration_marks_email_unverified_and_verification_completes_it()
    {
        var svc = NewService(out _);
        var reg = await svc.RegisterAsync(Reg(), default);
        // A verification token was issued on register.
        var request = await svc.RequestEmailVerificationAsync(new RequestEmailVerificationRequest("admin@x.de"), default);
        request.DevToken.Should().NotBeNullOrEmpty();

        await svc.VerifyEmailAsync(new VerifyEmailRequest(request.DevToken!), default);

        // Already verified → no new token issued.
        var again = await svc.RequestEmailVerificationAsync(new RequestEmailVerificationRequest("admin@x.de"), default);
        again.DevToken.Should().BeNull();
    }
}
