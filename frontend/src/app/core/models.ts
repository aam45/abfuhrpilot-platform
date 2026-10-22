// Types mirroring the AbfuhrPilot /api/v1 contract (enums serialized as strings).

export type UserRole = 'Admin' | 'Dispatcher' | 'Driver' | 'Viewer';

export type WasteCategory =
  | 'HouseholdWaste' | 'Plastic' | 'Wood' | 'Paper' | 'Organic' | 'Metal'
  | 'Glass' | 'BulkyWaste' | 'ContainerWaste' | 'ConstructionWaste' | 'SpecialWaste';

export type RouteStatus = 'Draft' | 'Planned' | 'InProgress' | 'Completed' | 'Cancelled';
export type StopStatus = 'Open' | 'InProgress' | 'Done' | 'Skipped' | 'Problem' | 'Cancelled';
export type OrderStatus = 'New' | 'Scheduled' | 'Assigned' | 'InProgress' | 'Completed' | 'Rejected' | 'Cancelled' | 'Problem';
export type ContainerStatus = 'Available' | 'Delivered' | 'Full' | 'PickupScheduled' | 'InTransit' | 'AtDepot' | 'Maintenance';
export type VehicleType = 'HouseholdWasteTruck' | 'PlasticWasteTruck' | 'WoodWasteTruck' | 'ContainerTruck' | 'BulkyWasteTruck' | 'MixedUtilityTruck';
export type DestinationType = 'Depot' | 'RecyclingCenter' | 'Landfill' | 'TransferStation' | 'CustomerSite';
export type PickupType = 'RegularBinPickup' | 'ContainerPickup' | 'ContainerExchange' | 'BulkyWastePickup' | 'WoodPickup' | 'PlasticPickup' | 'SpecialOrder';
export type OrderType = 'RegularPickup' | 'ContainerPickup' | 'ContainerExchange' | 'SpecialPickup' | 'BulkyWaste' | 'WoodPickup' | 'PlasticPickup';
export type ProblemType = 'BlockedByCar' | 'WrongWasteType' | 'ContainerNotFound' | 'BinNotAvailable' | 'AddressNotFound' | 'RoadClosed' | 'CustomerNotAvailable' | 'VehicleIssue' | 'Other';

export interface User { id: string; fullName: string; email: string; role: UserRole; companyId: string; driverId?: string | null; isActive: boolean; }
export interface AuthResponse { token: string; expiresAt: string; refreshToken: string; refreshExpiresAt: string; user: User; }

export interface Paged<T> { items: T[]; page: number; pageSize: number; totalCount: number; totalPages: number; }

export interface WasteType { id: string; name: string; code: string; category: WasteCategory; description?: string; requiresSeparateCollection: boolean; requiresSpecialVehicle: boolean; defaultDestinationId?: string | null; isActive: boolean; }
export interface Vehicle { id: string; name: string; licensePlate: string; vehicleType: VehicleType; capacity?: number | null; heightMeters?: number | null; widthMeters?: number | null; lengthMeters?: number | null; weightTons?: number | null; maxLoadTons?: number | null; allowedWasteCategories: WasteCategory[]; isActive: boolean; }
export interface Driver { id: string; userId?: string | null; fullName: string; phoneNumber?: string | null; licenseNumber?: string | null; isActive: boolean; }
export interface Destination { id: string; name: string; type: DestinationType; address?: string; latitude?: number | null; longitude?: number | null; openingHours?: string | null; acceptedWasteCategories: WasteCategory[]; isActive: boolean; }
export interface Container { id: string; containerNumber: string; currentAddress?: string | null; latitude?: number | null; longitude?: number | null; wasteTypeId: string; size?: string | null; status: ContainerStatus; lastServicedAt?: string | null; }

export interface RouteDto {
  id: string; routeNumber: string; name: string; date: string; wasteTypeId: string; wasteTypeName?: string;
  vehicleId?: string | null; vehicleName?: string; driverId?: string | null; driverName?: string;
  startDepotId?: string | null; endDestinationId?: string | null; status: RouteStatus;
  plannedStartTime?: string | null; plannedEndTime?: string | null; actualStartTime?: string | null; actualEndTime?: string | null;
  stopCount: number; doneCount: number; problemCount: number;
}
export interface Stop {
  id: string; routeId: string; sequenceNumber: number; address: string; latitude?: number | null; longitude?: number | null;
  wasteTypeId: string; pickupType: PickupType; containerId?: string | null; customerName?: string | null; notes?: string | null;
  status: StopStatus; plannedArrivalTime?: string | null; actualArrivalTime?: string | null; completedAt?: string | null; problemReason?: ProblemType | null;
}
export interface Order {
  id: string; orderNumber: string; orderType: OrderType; wasteTypeId: string; pickupType: PickupType; containerId?: string | null;
  customerName: string; customerPhone?: string | null; address: string; latitude?: number | null; longitude?: number | null;
  requestedDate?: string | null; scheduledDate?: string | null; assignedRouteId?: string | null; status: OrderStatus; notes?: string | null;
}

export interface ProblemPhotoDto { id: string; url: string; fileName: string; contentType: string; sizeBytes: number; }

// ---- Routing / navigation ----
export type RoutingProviderType = 'None' | 'LocalFallback' | 'GraphHopper' | 'Google' | 'Mapbox' | 'Here' | 'Osrm';
export interface GeoPoint { latitude: number; longitude: number; }
export interface RouteGeometry { points: GeoPoint[]; }
export interface RouteLeg { fromStopId?: string | null; toStopId?: string | null; distanceMeters: number; durationSeconds: number; }
export interface RoutingWarning { code: string; message: string; severity: 'Info' | 'Warning'; }
export interface RouteCalculationResult {
  provider: RoutingProviderType; providerConfigured: boolean; warning?: string | null;
  totalDistanceMeters: number; totalDurationSeconds: number; legs: RouteLeg[];
  geometry?: RouteGeometry | null; excludedStopIds: string[];
  warnings?: RoutingWarning[]; routingProfileUsed?: string | null; truckRestrictionsApplied?: boolean;
}
export interface OptimizedStop { stopId: string; newSequenceNumber: number; }
export interface RouteOptimizationResult {
  provider: RoutingProviderType; providerConfigured: boolean; warning?: string | null;
  order: OptimizedStop[]; totalDistanceMeters: number; totalDurationSeconds: number;
  excludedStopIds: string[]; geometry?: RouteGeometry | null;
  warnings?: RoutingWarning[]; routingProfileUsed?: string | null; truckRestrictionsApplied?: boolean;
}
export interface NavigationLinks { googleMaps: string; appleMaps: string; openStreetMap: string; }

// ---- In-app navigation (GET /routes/{id}/in-app-navigation) ----
export interface NavigationStep {
  sequenceNumber: number; instruction: string; maneuverType: string; modifier?: string | null;
  roadName?: string | null; distanceMeters: number; durationSeconds: number; latitude: number; longitude: number;
}
export interface NavigationLeg {
  fromStopId?: string | null; toStopId?: string | null; fromLabel: string; toLabel: string;
  distanceMeters: number; durationSeconds: number; steps: NavigationStep[];
}
export interface InAppNavigation {
  routeId: string; provider: RoutingProviderType; providerConfigured: boolean;
  totalDistanceMeters: number; totalDurationSeconds: number;
  geometry: GeoPoint[]; legs: NavigationLeg[]; warnings: RoutingWarning[];
  routingProfileUsed?: string | null; truckRestrictionsApplied: boolean;
}

export interface OperationsDashboard {
  routesToday: number; activeRoutes: number; completedStops: number; openStops: number;
  problemStops: number; totalStops: number; openOrders: number; vehiclesInUse: number; todaysRoutes: RouteDto[];
}
export interface DriverDashboard { todaysRoute?: RouteDto | null; nextStop?: Stop | null; stops: Stop[]; doneCount: number; totalCount: number; }
