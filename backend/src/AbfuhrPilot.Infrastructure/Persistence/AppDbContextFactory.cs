using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AbfuhrPilot.Infrastructure.Persistence;

/// <summary>
/// Used by the EF Core CLI (`dotnet ef migrations …`) so tooling can build the
/// model against PostgreSQL without booting the whole web application.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Host=localhost;Port=5432;Database=abfuhrpilot;Username=abfuhrpilot;Password=abfuhrpilot";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new AppDbContext(options);
    }
}
