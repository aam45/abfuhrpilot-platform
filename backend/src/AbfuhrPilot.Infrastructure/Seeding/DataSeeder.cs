using AbfuhrPilot.Application.Common;
using AbfuhrPilot.Domain.Entities;
using AbfuhrPilot.Domain.Enums;
using AbfuhrPilot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AbfuhrPilot.Infrastructure.Seeding;

/// <summary>
/// Seeds a realistic demo workspace (company, users, catalogue, routes) so the
/// app can be tested end-to-end. Idempotent: only runs on an empty database.
/// </summary>
public class DataSeeder
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher _hasher;
    public DataSeeder(AppDbContext db, IPasswordHasher hasher) { _db = db; _hasher = hasher; }

    public const string DemoPassword = "Passw0rd!";

    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (await _db.Companies.AnyAsync(ct)) return;

        var company = new Company { Name = "Stadtreinigung Musterstadt GmbH", Address = "Betriebshof 1, 12345 Musterstadt" };
        _db.Companies.Add(company);

        // ---- Users ----
        User Mk(string name, string email, UserRole role) => new()
        {
            CompanyId = company.Id, FullName = name, Email = email,
            PasswordHash = _hasher.Hash(DemoPassword), Role = role, IsActive = true
        };
        var admin = Mk("Anna Admin", "admin@demo.de", UserRole.Admin);
        var dispo = Mk("Dieter Dispo", "dispo@demo.de", UserRole.Dispatcher);
        var driverUser = Mk("Frank Fahrer", "fahrer@demo.de", UserRole.Driver);
        var viewer = Mk("Vera View", "viewer@demo.de", UserRole.Viewer);
        _db.Users.AddRange(admin, dispo, driverUser, viewer);

        // ---- Waste types ----
        WasteType Wt(string name, string code, WasteCategory cat, bool sep = false, bool special = false) => new()
        {
            CompanyId = company.Id, Name = name, Code = code, Category = cat,
            RequiresSeparateCollection = sep, RequiresSpecialVehicle = special
        };
        var hausmuell = Wt("Hausmüll", "HM", WasteCategory.HouseholdWaste);
        var kunststoff = Wt("Kunststoff", "KS", WasteCategory.Plastic, sep: true);
        var holz = Wt("Holz", "HZ", WasteCategory.Wood, sep: true);
        var papier = Wt("Papier", "PA", WasteCategory.Paper, sep: true);
        var bio = Wt("Biomüll", "BIO", WasteCategory.Organic, sep: true);
        var sperr = Wt("Sperrmüll", "SP", WasteCategory.BulkyWaste);
        var container = Wt("Container", "CT", WasteCategory.ContainerWaste);
        var sonder = Wt("Sonderabfall", "SO", WasteCategory.SpecialWaste, sep: true, special: true);
        _db.WasteTypes.AddRange(hausmuell, kunststoff, holz, papier, bio, sperr, container, sonder);

        // ---- Destinations ----
        var depot = new Destination { CompanyId = company.Id, Name = "Betriebshof Musterstadt", Type = DestinationType.Depot,
            Address = "Betriebshof 1, 12345 Musterstadt", Latitude = 52.5200, Longitude = 13.4050,
            AcceptedWasteCategories = Enum.GetValues<WasteCategory>().ToList() };
        var recycling = new Destination { CompanyId = company.Id, Name = "Recyclinghof Nord", Type = DestinationType.RecyclingCenter,
            Address = "Recyclingweg 5, 12347 Musterstadt", Latitude = 52.5350, Longitude = 13.4200,
            AcceptedWasteCategories = new() { WasteCategory.Plastic, WasteCategory.Paper, WasteCategory.Wood, WasteCategory.Metal, WasteCategory.Glass } };
        _db.Destinations.AddRange(depot, recycling);

        // ---- Vehicles (realistic Müllwagen dimensions for truck-aware routing) ----
        var truck1 = new Vehicle { CompanyId = company.Id, Name = "Presswagen 1", LicensePlate = "MU-AB 101",
            VehicleType = VehicleType.HouseholdWasteTruck, Capacity = 22,
            HeightMeters = 3.6, WidthMeters = 2.55, LengthMeters = 10.5, WeightTons = 18, MaxLoadTons = 11,
            AllowedWasteCategories = new() { WasteCategory.HouseholdWaste, WasteCategory.Organic } };
        var truck2 = new Vehicle { CompanyId = company.Id, Name = "Kunststoffwagen", LicensePlate = "MU-KS 202",
            VehicleType = VehicleType.PlasticWasteTruck, Capacity = 18,
            HeightMeters = 3.5, WidthMeters = 2.55, LengthMeters = 9.8, WeightTons = 16, MaxLoadTons = 9,
            AllowedWasteCategories = new() { WasteCategory.Plastic, WasteCategory.Paper } };
        var truck3 = new Vehicle { CompanyId = company.Id, Name = "Containerfahrzeug", LicensePlate = "MU-CT 303",
            VehicleType = VehicleType.ContainerTruck, Capacity = 30,
            HeightMeters = 3.8, WidthMeters = 2.55, LengthMeters = 11.5, WeightTons = 26, MaxLoadTons = 15,
            AllowedWasteCategories = new() { WasteCategory.ContainerWaste, WasteCategory.Wood, WasteCategory.BulkyWaste, WasteCategory.ConstructionWaste } };
        _db.Vehicles.AddRange(truck1, truck2, truck3);

        // ---- Drivers ----
        var frank = new Driver { CompanyId = company.Id, UserId = driverUser.Id, FullName = "Frank Fahrer",
            PhoneNumber = "+49 170 1234567", LicenseNumber = "C+E 998877" };
        var gerd = new Driver { CompanyId = company.Id, FullName = "Gerd Gaspedal", PhoneNumber = "+49 171 7654321", LicenseNumber = "C 112233" };
        _db.Drivers.AddRange(frank, gerd);

        // ---- Containers ----
        var c1 = new Container { CompanyId = company.Id, ContainerNumber = "C-0001", WasteTypeId = container.Id,
            CurrentAddress = "Baustelle Ringstraße 12", Size = "7 m³", Status = ContainerStatus.Delivered, Latitude = 52.515, Longitude = 13.41 };
        var c2 = new Container { CompanyId = company.Id, ContainerNumber = "C-0002", WasteTypeId = holz.Id,
            CurrentAddress = "Tischlerei Holzweg 3", Size = "10 m³", Status = ContainerStatus.Full, Latitude = 52.51, Longitude = 13.42 };
        _db.Containers.AddRange(c1, c2);

        // ---- Route with stops (Hausmüll tour today) ----
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var route = new Route
        {
            CompanyId = company.Id, RouteNumber = "R-001", Name = "Hausmüll Innenstadt", Date = today,
            WasteTypeId = hausmuell.Id, VehicleId = truck1.Id, DriverId = frank.Id,
            StartDepotId = depot.Id, EndDestinationId = depot.Id, Status = RouteStatus.Planned,
            PlannedStartTime = DateTime.UtcNow.Date.AddHours(6), PlannedEndTime = DateTime.UtcNow.Date.AddHours(12)
        };
        string[] streets = { "Hauptstraße 1", "Hauptstraße 15", "Marktplatz 3", "Lindenweg 8", "Schulstraße 22" };
        for (var i = 0; i < streets.Length; i++)
        {
            route.Stops.Add(new Stop
            {
                CompanyId = company.Id, SequenceNumber = i + 1, Address = streets[i],
                WasteTypeId = hausmuell.Id, PickupType = PickupType.RegularBinPickup,
                Latitude = 52.52 + i * 0.001, Longitude = 13.405 + i * 0.001, Status = StopStatus.Open
            });
        }
        _db.Routes.Add(route);

        // ---- Container/special orders ----
        _db.Orders.AddRange(
            new Order { CompanyId = company.Id, OrderNumber = "A-1001", OrderType = OrderType.ContainerExchange,
                WasteTypeId = container.Id, PickupType = PickupType.ContainerExchange, ContainerId = c1.Id,
                CustomerName = "Bau GmbH", CustomerPhone = "+49 30 111222", Address = "Ringstraße 12",
                RequestedDate = today, Status = OrderStatus.New, Latitude = 52.515, Longitude = 13.41 },
            new Order { CompanyId = company.Id, OrderNumber = "A-1002", OrderType = OrderType.BulkyWaste,
                WasteTypeId = sperr.Id, PickupType = PickupType.BulkyWastePickup,
                CustomerName = "Familie Müller", Address = "Gartenstraße 7", RequestedDate = today, Status = OrderStatus.New });

        await _db.SaveChangesAsync(ct);
    }
}
