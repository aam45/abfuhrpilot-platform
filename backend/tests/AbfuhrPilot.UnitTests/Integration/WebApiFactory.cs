using AbfuhrPilot.Application.Common;
using AbfuhrPilot.Domain.Entities;
using AbfuhrPilot.Domain.Enums;
using AbfuhrPilot.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AbfuhrPilot.UnitTests.Integration;

/// <summary>
/// Shared HTTP-level test fixture for the driver contract: boots the real
/// ASP.NET Core pipeline (auth, the idempotency resource filter, JSON options,
/// exception middleware) against an isolated in-memory database, so contract
/// regressions are caught end-to-end rather than only at the service layer.
/// </summary>
public class WebApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = Guid.NewGuid().ToString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Seed:Enabled"] = "false",
                ["Swagger:Enabled"] = "false",
            });
        });

        builder.ConfigureServices(services =>
        {
            // Replace the real (Npgsql) AppDbContext registration with an isolated
            // in-memory database unique to this factory instance.
            var dbContextDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (dbContextDescriptor is not null) services.Remove(dbContextDescriptor);

            var dbContextServiceDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(AppDbContext));
            if (dbContextServiceDescriptor is not null) services.Remove(dbContextServiceDescriptor);

            services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(_dbName));
        });
    }

    /// <summary>Opens a scoped <see cref="AppDbContext"/> for seeding/asserting test data.</summary>
    public AppDbContext CreateDbContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AppDbContext>();
    }

    /// <summary>Seeds a minimal company/driver/vehicle/route/stop set and returns the ids.</summary>
    public async Task<SeededScenario> SeedScenarioAsync()
    {
        await using var db = CreateDbContext();
        var companyId = Guid.NewGuid();
        db.Companies.Add(new Company { Id = companyId, Name = "Test Co." });

        var wasteType = new WasteType
        {
            CompanyId = companyId, Name = "Restmüll", Code = "REST", Category = WasteCategory.HouseholdWaste,
        };
        db.WasteTypes.Add(wasteType);

        var vehicle = new Vehicle
        {
            CompanyId = companyId, Name = "LKW 1", LicensePlate = "AB-CD 123",
            VehicleType = VehicleType.HouseholdWasteTruck,
            AllowedWasteCategories = new List<WasteCategory> { WasteCategory.HouseholdWaste },
        };
        db.Vehicles.Add(vehicle);

        var driverUser = new User
        {
            CompanyId = companyId, FullName = "Driver One", Email = "driver1@test.local",
            PasswordHash = "x", Role = UserRole.Driver,
        };
        db.Users.Add(driverUser);

        var driver = new Driver { CompanyId = companyId, UserId = driverUser.Id, FullName = "Driver One" };
        db.Drivers.Add(driver);

        var route = new Route
        {
            CompanyId = companyId, RouteNumber = "R-1", Name = "Tour 1", WasteTypeId = wasteType.Id,
            VehicleId = vehicle.Id, DriverId = driver.Id, Status = RouteStatus.Planned,
        };
        db.Routes.Add(route);

        var stop = new Stop
        {
            CompanyId = companyId, RouteId = route.Id, SequenceNumber = 1, Address = "Musterstraße 1",
            WasteTypeId = wasteType.Id, PickupType = PickupType.RegularBinPickup,
        };
        db.Stops.Add(stop);

        await db.SaveChangesAsync();

        return new SeededScenario(companyId, driverUser.Id, driver.Id, route.Id, stop.Id);
    }

    /// <summary>Same as <see cref="SeedScenarioAsync"/> but the route is already
    /// <see cref="RouteStatus.InProgress"/>, which is a precondition for working
    /// (starting/completing/skipping/reporting a problem on) a stop.</summary>
    public async Task<SeededScenario> SeedInProgressScenarioAsync()
    {
        var scenario = await SeedScenarioAsync();
        await using var db = CreateDbContext();
        var route = await db.Routes.SingleAsync(r => r.Id == scenario.RouteId);
        route.Start(scenario.DriverId);
        await db.SaveChangesAsync();
        return scenario;
    }

    /// <summary>Mints a real, signed JWT for the given user via the app's actual <see cref="IJwtTokenService"/>.</summary>
    public string MintToken(Guid companyId, Guid userId, UserRole role, Guid? driverId = null)
    {
        using var scope = Services.CreateScope();
        var jwt = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        var user = new User { Id = userId, CompanyId = companyId, FullName = "Test User", Email = "u@test.local", Role = role };
        return jwt.CreateToken(user, driverId).Token;
    }

    public HttpClient CreateAuthenticatedClient(string token)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}

public record SeededScenario(Guid CompanyId, Guid DriverUserId, Guid DriverId, Guid RouteId, Guid StopId);
