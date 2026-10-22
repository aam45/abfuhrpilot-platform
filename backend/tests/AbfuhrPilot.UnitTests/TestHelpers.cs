using AbfuhrPilot.Application.Common;
using AbfuhrPilot.Domain.Enums;
using AbfuhrPilot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AbfuhrPilot.UnitTests;

/// <summary>A simple in-memory <see cref="ICurrentUser"/> for service tests.</summary>
public class FakeCurrentUser : ICurrentUser
{
    public Guid UserId { get; set; } = Guid.NewGuid();
    public Guid CompanyId { get; set; } = Guid.NewGuid();
    public UserRole Role { get; set; } = UserRole.Admin;
    public Guid? DriverId { get; set; }
    public bool IsAuthenticated => true;
}

public static class TestDb
{
    /// <summary>Creates an isolated in-memory database per test.</summary>
    public static AppDbContext Create()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }
}
