using AbfuhrPilot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AbfuhrPilot.Application.Common;

/// <summary>
/// Persistence abstraction used by the application layer. The concrete
/// implementation lives in the Infrastructure layer (EF Core + PostgreSQL),
/// keeping the application services free of a hard EF dependency at the edges.
/// </summary>
public interface IAppDbContext
{
    DbSet<Company> Companies { get; }
    DbSet<User> Users { get; }
    DbSet<Driver> Drivers { get; }
    DbSet<Vehicle> Vehicles { get; }
    DbSet<WasteType> WasteTypes { get; }
    DbSet<Destination> Destinations { get; }
    DbSet<Route> Routes { get; }
    DbSet<Stop> Stops { get; }
    DbSet<Container> Containers { get; }
    DbSet<Order> Orders { get; }
    DbSet<ProblemReport> ProblemReports { get; }
    DbSet<ProblemPhoto> ProblemPhotos { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<UserToken> UserTokens { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
