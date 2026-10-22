// German UI labels for enum values + status → css-class mapping.
import { OrderStatus, RouteStatus, StopStatus, WasteCategory } from './models';

export const routeStatusLabel: Record<RouteStatus, string> = {
  Draft: 'Entwurf', Planned: 'Geplant', InProgress: 'In Arbeit', Completed: 'Abgeschlossen', Cancelled: 'Storniert',
};
export const stopStatusLabel: Record<StopStatus, string> = {
  Open: 'Offen', InProgress: 'In Arbeit', Done: 'Erledigt', Skipped: 'Übersprungen', Problem: 'Problem', Cancelled: 'Storniert',
};
export const orderStatusLabel: Record<OrderStatus, string> = {
  New: 'Neu', Scheduled: 'Eingeplant', Assigned: 'Zugewiesen', InProgress: 'In Arbeit',
  Completed: 'Abgeschlossen', Rejected: 'Abgelehnt', Cancelled: 'Storniert', Problem: 'Problem',
};
export const wasteCategoryLabel: Record<WasteCategory, string> = {
  HouseholdWaste: 'Hausmüll', Plastic: 'Kunststoff', Wood: 'Holz', Paper: 'Papier', Organic: 'Biomüll',
  Metal: 'Metall', Glass: 'Glas', BulkyWaste: 'Sperrmüll', ContainerWaste: 'Container',
  ConstructionWaste: 'Bauschutt', SpecialWaste: 'Sonderabfall',
};
export const problemTypeLabel: Record<string, string> = {
  BlockedByCar: 'Durch Auto blockiert', WrongWasteType: 'Falscher Abfall', ContainerNotFound: 'Container nicht gefunden',
  BinNotAvailable: 'Tonne nicht bereitgestellt', AddressNotFound: 'Adresse nicht gefunden', RoadClosed: 'Straße gesperrt',
  CustomerNotAvailable: 'Kunde nicht erreichbar', VehicleIssue: 'Fahrzeugproblem', Other: 'Sonstiges',
};
export const vehicleTypeLabel: Record<string, string> = {
  HouseholdWasteTruck: 'Hausmüllwagen', PlasticWasteTruck: 'Kunststoffwagen', WoodWasteTruck: 'Holzwagen',
  ContainerTruck: 'Containerfahrzeug', BulkyWasteTruck: 'Sperrmüllwagen', MixedUtilityTruck: 'Mehrzweckfahrzeug',
};
export const destinationTypeLabel: Record<string, string> = {
  Depot: 'Betriebshof', RecyclingCenter: 'Recyclinghof', Landfill: 'Deponie', TransferStation: 'Umschlagplatz', CustomerSite: 'Kundenstandort',
};
export const orderTypeLabel: Record<string, string> = {
  RegularPickup: 'Regelabholung', ContainerPickup: 'Container-Abholung', ContainerExchange: 'Container-Tausch',
  SpecialPickup: 'Sonderabholung', BulkyWaste: 'Sperrmüll', WoodPickup: 'Holz', PlasticPickup: 'Kunststoff',
};
export const pickupTypeLabel: Record<string, string> = {
  RegularBinPickup: 'Reguläre Tonne', ContainerPickup: 'Container-Abholung', ContainerExchange: 'Container-Tausch',
  BulkyWastePickup: 'Sperrmüll', WoodPickup: 'Holz', PlasticPickup: 'Kunststoff', SpecialOrder: 'Sonderauftrag',
};
export const containerStatusLabel: Record<string, string> = {
  Available: 'Verfügbar', Delivered: 'Aufgestellt', Full: 'Voll', PickupScheduled: 'Abholung geplant',
  InTransit: 'Unterwegs', AtDepot: 'Am Hof', Maintenance: 'Wartung',
};
export const userRoleLabel: Record<string, string> = {
  Admin: 'Administrator', Dispatcher: 'Disponent', Driver: 'Fahrer', Viewer: 'Betrachter',
};
/** Routing warning codes → German UI text (backend messages stay technical/English). */
export const routingWarningLabel: Record<string, string> = {
  PROVIDER_NOT_CONFIGURED: 'Routing-Anbieter nicht konfiguriert',
  PROVIDER_USING_LOCAL_FALLBACK: 'Lokaler Fallback — keine LKW-Regeln',
  PROVIDER_USING_CAR_FALLBACK: 'Anbieter nutzt Auto-Profil',
  TRUCK_RESTRICTIONS_NOT_ENFORCED: 'LKW-Beschränkungen nicht aktiv',
  TRUCK_PROFILE_NOT_FULLY_SUPPORTED: 'LKW-Profil aktiv, Fahrzeugmaße werden noch nicht übertragen',
  NO_VEHICLE_ASSIGNED: 'Kein Fahrzeug zugewiesen — Route ohne Fahrzeugregeln berechnet',
  MISSING_VEHICLE_HEIGHT: 'Fahrzeughöhe fehlt',
  MISSING_VEHICLE_WEIGHT: 'Fahrzeuggewicht fehlt',
  MISSING_VEHICLE_WIDTH: 'Fahrzeugbreite fehlt',
  MISSING_VEHICLE_LENGTH: 'Fahrzeuglänge fehlt',
  ROUTE_HAS_STOPS_WITHOUT_COORDINATES: 'Stopps ohne Koordinaten ausgeschlossen',
  PROVIDER_CALL_FAILED: 'Anbieterfehler — lokale Schätzung verwendet',
};

/**
 * Waste-fraction accent for the signature "fraction rail" (encodes the tour's
 * real fraction). Accepts a German waste-type name or a WasteCategory value.
 */
export function wasteColorVar(nameOrCategory?: string | null): string {
  const s = (nameOrCategory ?? '').toLowerCase();
  if (s.includes('kunststoff') || s.includes('plastic')) return 'var(--w-kunststoff)';
  if (s.includes('holz') || s.includes('wood')) return 'var(--w-holz)';
  if (s.includes('papier') || s.includes('paper')) return 'var(--w-papier)';
  if (s.includes('bio') || s.includes('organic')) return 'var(--w-bio)';
  if (s.includes('metall') || s.includes('metal')) return 'var(--w-metall)';
  if (s.includes('glas')) return 'var(--w-glas)';
  if (s.includes('sperr') || s.includes('bulky')) return 'var(--w-sperr)';
  if (s.includes('container')) return 'var(--w-container)';
  if (s.includes('sonder') || s.includes('special') || s.includes('bauschutt') || s.includes('construction')) return 'var(--w-sonder)';
  return 'var(--w-hausmuell)';
}

/** Maps a status value to a status-tone key used by the badge component. */
export function statusTone(value: string): string {
  const map: Record<string, string> = {
    Open: 'open', New: 'open', Draft: 'open',
    InProgress: 'progress', Planned: 'progress', Scheduled: 'progress', Assigned: 'progress',
    Done: 'done', Completed: 'done',
    Skipped: 'skip',
    Problem: 'problem', Rejected: 'problem', Cancelled: 'problem',
  };
  return map[value] ?? 'open';
}
