import { Observable } from 'rxjs';
import { ApiService } from '../../core/api/api.service';
import { Paged } from '../../core/models';
import {
  containerStatusLabel, destinationTypeLabel, orderStatusLabel, orderTypeLabel,
  vehicleTypeLabel, wasteCategoryLabel,
} from '../../core/labels';

export type ListConfigKey = 'orders' | 'containers' | 'vehicles' | 'drivers' | 'wasteTypes' | 'destinations';

export interface ListColumn {
  key: string;
  label: string;
  kind?: 'text' | 'status' | 'waste' | 'list';
  strong?: boolean;
  map?: Record<string, string>;
}

interface Cfg { title: string; subtitle: string; icon: string; columns: ListColumn[]; load: (api: ApiService, q: any) => Observable<Paged<any>>; }

export const listConfigs: Record<ListConfigKey, Cfg> = {
  orders: {
    title: 'Aufträge', subtitle: 'Container- und Sonderaufträge', icon: '📋',
    load: (api, q) => api.orders(q),
    columns: [
      { key: 'orderNumber', label: 'Nr.', strong: true },
      { key: 'customerName', label: 'Kunde' },
      { key: 'address', label: 'Adresse' },
      { key: 'orderType', label: 'Auftragsart', map: orderTypeLabel },
      { key: 'status', label: 'Status', kind: 'status', map: orderStatusLabel },
    ],
  },
  containers: {
    title: 'Container', subtitle: 'Bestand und aktueller Status', icon: '📦',
    load: (api, q) => api.containers(q),
    columns: [
      { key: 'containerNumber', label: 'Container-Nr.', strong: true },
      { key: 'currentAddress', label: 'Standort' },
      { key: 'size', label: 'Größe' },
      { key: 'status', label: 'Status', kind: 'status', map: containerStatusLabel },
    ],
  },
  vehicles: {
    title: 'Fahrzeuge', subtitle: 'Flotte und freigegebene Abfallarten', icon: '🚚',
    load: (api, q) => api.vehicles(q),
    columns: [
      { key: 'name', label: 'Fahrzeug', strong: true },
      { key: 'licensePlate', label: 'Kennzeichen' },
      { key: 'vehicleType', label: 'Typ', map: vehicleTypeLabel },
      { key: 'allowedWasteCategories', label: 'Erlaubte Abfallarten', kind: 'list', map: wasteCategoryLabel },
    ],
  },
  drivers: {
    title: 'Fahrer', subtitle: 'Fahrpersonal', icon: '👷',
    load: (api, q) => api.drivers(q),
    columns: [
      { key: 'fullName', label: 'Name', strong: true },
      { key: 'phoneNumber', label: 'Telefon' },
      { key: 'licenseNumber', label: 'Führerschein' },
    ],
  },
  wasteTypes: {
    title: 'Abfallarten', subtitle: 'Fraktionen des Unternehmens', icon: '🗑',
    load: (api, q) => api.wasteTypes(q),
    columns: [
      { key: 'name', label: 'Bezeichnung', strong: true },
      { key: 'code', label: 'Code' },
      { key: 'category', label: 'Kategorie', kind: 'waste', map: wasteCategoryLabel },
    ],
  },
  destinations: {
    title: 'Ziele / Höfe', subtitle: 'Depots, Recyclinghöfe, Deponien', icon: '🏭',
    load: (api, q) => api.destinations(q),
    columns: [
      { key: 'name', label: 'Name', strong: true },
      { key: 'type', label: 'Typ', map: destinationTypeLabel },
      { key: 'address', label: 'Adresse' },
      { key: 'acceptedWasteCategories', label: 'Angenommene Abfallarten', kind: 'list', map: wasteCategoryLabel },
    ],
  },
};
