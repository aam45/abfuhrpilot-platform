using AbfuhrPilot.Application.Common;
using AbfuhrPilot.Application.Dtos;
using AbfuhrPilot.Application.Services;
using AbfuhrPilot.Domain.Common;
using AbfuhrPilot.Domain.Entities;
using AbfuhrPilot.Domain.Enums;
using AbfuhrPilot.Infrastructure.Persistence;
using AbfuhrPilot.Infrastructure.Security;
using FluentAssertions;
using Xunit;

namespace AbfuhrPilot.UnitTests;

public class UserServiceTests
{
    private readonly FakeCurrentUser _me = new();

    private (UserService svc, AppDbContext db) NewService()
    {
        var db = TestDb.Create();
        return (new UserService(db, _me, new PasswordHasher()), db);
    }

    private async Task<User> SeedAdmin(AppDbContext db)
    {
        var admin = new User { CompanyId = _me.CompanyId, FullName = "Anna Admin", Email = "admin@x.de",
            PasswordHash = new PasswordHasher().Hash("Passw0rd!"), Role = UserRole.Admin, IsActive = true };
        db.Users.Add(admin);
        await db.SaveChangesAsync();
        return admin;
    }

    [Fact]
    public async Task Create_hashes_password_and_applies_policy()
    {
        var (svc, _) = NewService();

        var weak = async () => await svc.CreateAsync(new CreateUserRequest("Bob", "bob@x.de", "abc", UserRole.Driver), default);
        await weak.Should().ThrowAsync<DomainException>().WithMessage("*at least*");

        var ok = await svc.CreateAsync(new CreateUserRequest("Bob", "bob@x.de", "Passw0rd!", UserRole.Dispatcher), default);
        ok.Email.Should().Be("bob@x.de");
        ok.Role.Should().Be(UserRole.Dispatcher);
    }

    [Fact]
    public async Task Create_rejects_duplicate_email()
    {
        var (svc, _) = NewService();
        await svc.CreateAsync(new CreateUserRequest("A", "dup@x.de", "Passw0rd!", UserRole.Viewer), default);
        var again = async () => await svc.CreateAsync(new CreateUserRequest("B", "DUP@x.de", "Passw0rd!", UserRole.Viewer), default);
        await again.Should().ThrowAsync<DomainException>().WithMessage("*already exists*");
    }

    [Fact]
    public async Task Cannot_demote_or_deactivate_the_last_active_admin()
    {
        var (svc, db) = NewService();
        var admin = await SeedAdmin(db);

        var demote = async () => await svc.UpdateAsync(admin.Id, new UpdateUserRequest("Anna", UserRole.Viewer, true), default);
        await demote.Should().ThrowAsync<DomainException>().WithMessage("*last active administrator*");

        var deactivate = async () => await svc.UpdateAsync(admin.Id, new UpdateUserRequest("Anna", UserRole.Admin, false), default);
        await deactivate.Should().ThrowAsync<DomainException>().WithMessage("*last active administrator*");
    }

    [Fact]
    public async Task Can_demote_admin_when_another_active_admin_exists()
    {
        var (svc, db) = NewService();
        var admin1 = await SeedAdmin(db);
        await svc.CreateAsync(new CreateUserRequest("Second Admin", "admin2@x.de", "Passw0rd!", UserRole.Admin), default);

        var result = await svc.UpdateAsync(admin1.Id, new UpdateUserRequest("Anna", UserRole.Dispatcher, true), default);
        result.Role.Should().Be(UserRole.Dispatcher);
    }

    [Fact]
    public async Task Deactivating_a_user_prevents_login_and_raises_the_event()
    {
        var (svc, db) = NewService();
        await SeedAdmin(db); // keeps the last-admin guard satisfied
        var created = await svc.CreateAsync(new CreateUserRequest("Vera", "vera@x.de", "Passw0rd!", UserRole.Viewer), default);

        await svc.UpdateAsync(created.Id, new UpdateUserRequest("Vera", UserRole.Viewer, IsActive: false), default);

        // Deactivation event was published to the outbox.
        db.OutboxMessages.Select(m => m.Type).Should().Contain("abfuhrpilot.identity.user-deactivated");

        // And login is now rejected.
        var auth = new AuthService(db, new PasswordHasher(), new StubJwt(),
            new InMemoryLoginThrottle(), new FakeEmail(), new AbfuhrPilot.Application.Auth.AuthTokenOptions());
        var act = async () => await auth.LoginAsync(new LoginRequest("vera@x.de", "Passw0rd!"), default);
        await act.Should().ThrowAsync<DomainException>();
    }

    private sealed class StubJwt : IJwtTokenService
    {
        public AccessToken CreateToken(User user, Guid? driverId) => new("t", DateTime.UtcNow.AddMinutes(60));
    }
    private sealed class FakeEmail : AbfuhrPilot.Application.Auth.IEmailSender
    {
        public Task SendAsync(string to, string subject, string body, CancellationToken ct = default) => Task.CompletedTask;
    }

    [Fact]
    public async Task Update_is_scoped_to_the_callers_company()
    {
        var (svc, db) = NewService();
        var otherCompanyUser = new User { CompanyId = Guid.NewGuid(), FullName = "X", Email = "x@y.de",
            PasswordHash = "h", Role = UserRole.Viewer, IsActive = true };
        db.Users.Add(otherCompanyUser);
        await db.SaveChangesAsync();

        var act = async () => await svc.UpdateAsync(otherCompanyUser.Id, new UpdateUserRequest("X", UserRole.Admin, true), default);
        await act.Should().ThrowAsync<NotFoundException>();
    }
}
