namespace AbfuhrPilot.Domain.Enums;

/// <summary>Application roles used for role-based access control.</summary>
public enum UserRole
{
    Admin = 0,
    Dispatcher = 1,
    Driver = 2,
    Viewer = 3
}

/// <summary>
/// Fixed catalogue of waste categories. A company-configurable <c>WasteType</c>
/// entity maps onto exactly one of these so vehicles/destinations can express
/// which categories they support without a many-to-many join table.
/// </summary>
public enum WasteCategory
{
    HouseholdWaste = 0,   // Hausmüll
    Plastic = 1,          // Kunststoff
    Wood = 2,             // Holz
    Paper = 3,            // Papier
    Organic = 4,          // Biomüll
    Metal = 5,            // Metall
    Glass = 6,            // Glas
    BulkyWaste = 7,       // Sperrmüll
    ContainerWaste = 8,   // Container
    ConstructionWaste = 9,// Bauschutt
    SpecialWaste = 10     // Sonderabfall
}

public enum VehicleType
{
    HouseholdWasteTruck = 0,
    PlasticWasteTruck = 1,
    WoodWasteTruck = 2,
    ContainerTruck = 3,
    BulkyWasteTruck = 4,
    MixedUtilityTruck = 5
}

public enum DestinationType
{
    Depot = 0,
    RecyclingCenter = 1,
    Landfill = 2,
    TransferStation = 3,
    CustomerSite = 4
}

public enum RouteStatus
{
    Draft = 0,
    Planned = 1,
    InProgress = 2,
    Completed = 3,
    Cancelled = 4
}

public enum StopStatus
{
    Open = 0,
    InProgress = 1,
    Done = 2,
    Skipped = 3,
    Problem = 4,
    Cancelled = 5
}

public enum PickupType
{
    RegularBinPickup = 0,
    ContainerPickup = 1,
    ContainerExchange = 2,
    BulkyWastePickup = 3,
    WoodPickup = 4,
    PlasticPickup = 5,
    SpecialOrder = 6
}

public enum ContainerStatus
{
    Available = 0,
    Delivered = 1,
    Full = 2,
    PickupScheduled = 3,
    InTransit = 4,
    AtDepot = 5,
    Maintenance = 6
}

public enum OrderStatus
{
    New = 0,
    Scheduled = 1,
    Assigned = 2,
    InProgress = 3,
    Completed = 4,
    Rejected = 5,
    Cancelled = 6,
    Problem = 7
}

public enum OrderType
{
    RegularPickup = 0,
    ContainerPickup = 1,
    ContainerExchange = 2,
    SpecialPickup = 3,
    BulkyWaste = 4,
    WoodPickup = 5,
    PlasticPickup = 6
}

public enum UserTokenPurpose
{
    PasswordReset = 0,
    EmailVerification = 1
}

public enum ProblemType
{
    BlockedByCar = 0,
    WrongWasteType = 1,
    ContainerNotFound = 2,
    BinNotAvailable = 3,
    AddressNotFound = 4,
    RoadClosed = 5,
    CustomerNotAvailable = 6,
    VehicleIssue = 7,
    Other = 8
}
