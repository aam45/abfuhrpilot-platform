import { Observable } from 'rxjs';
import { ApiService } from '../../core/api/api.service';
import { ListConfigKey } from './list-configs';
import {
  containerStatusLabel, destinationTypeLabel, orderTypeLabel, pickupTypeLabel,
  vehicleTypeLabel, wasteCategoryLabel,
} from '../../core/labels';

export type FieldType = 'text' | 'textarea' | 'number' | 'select' | 'waste-multi' | 'checkbox' | 'date';

export interface FormField {
  key: string;
  label: string;
  type: FieldType;
  required?: boolean;
  half?: boolean;                 // render two-per-row on wide screens
  placeholder?: string;
  help?: string;
  options?: { value: string; label: string }[];
  optionsSource?: 'wasteTypes';   // dynamically loaded reference options
}

export interface FormConfig {
  singular: string;               // "Abfallart"
  article: string;                // grammatical article for messages
  fields: FormField[];
  toForm: (row: any) => Record<string, any>;
  buildBody: (v: any) => any;
  create: (api: ApiService, body: any) => Observable<any>;
  update: (api: ApiService, id: string, body: any) => Observable<any>;
}

const opts = (map: Record<string, string>) =>
  Object.entries(map).map(([value, label]) => ({ value, label }));

export const wasteCategoryOptions = opts(wasteCategoryLabel);

// Coerce optional numeric string → number | null (so empty fields send null).
const num = (v: any) => (v === '' || v == null ? null : Number(v));

export const formConfigs: Record<ListConfigKey, FormConfig> = {
  wasteTypes: {
    singular: 'Abfallart', article: 'die',
    fields: [
      { key: 'name', label: 'Bezeichnung', type: 'text', required: true, placeholder: 'z. B. Hausmüll' },
      { key: 'code', label: 'Code', type: 'text', required: true, half: true, placeholder: 'HM' },
      { key: 'category', label: 'Kategorie', type: 'select', required: true, half: true, options: wasteCategoryOptions },
      { key: 'description', label: 'Beschreibung', type: 'textarea' },
      { key: 'requiresSeparateCollection', label: 'Getrennte Sammlung erforderlich', type: 'checkbox', help: 'Darf nicht mit anderen Abfallarten gemischt werden.' },
      { key: 'requiresSpecialVehicle', label: 'Spezialfahrzeug erforderlich', type: 'checkbox', help: 'Zuweisung erfordert ausdrückliche Bestätigung.' },
      { key: 'isActive', label: 'Aktiv', type: 'checkbox' },
    ],
    toForm: r => ({
      name: r.name, code: r.code, category: r.category, description: r.description ?? '',
      requiresSeparateCollection: r.requiresSeparateCollection, requiresSpecialVehicle: r.requiresSpecialVehicle,
      isActive: r.isActive,
    }),
    buildBody: v => ({ ...v, defaultDestinationId: null }),
    create: (api, b) => api.createWasteType(b),
    update: (api, id, b) => api.updateWasteType(id, b),
  },

  vehicles: {
    singular: 'Fahrzeug', article: 'das',
    fields: [
      { key: 'name', label: 'Bezeichnung', type: 'text', required: true, placeholder: 'Presswagen 1' },
      { key: 'licensePlate', label: 'Kennzeichen', type: 'text', required: true, half: true, placeholder: 'MU-AB 101' },
      { key: 'vehicleType', label: 'Fahrzeugtyp', type: 'select', required: true, half: true, options: opts(vehicleTypeLabel) },
      { key: 'capacity', label: 'Kapazität (m³)', type: 'number', half: true },
      { key: 'heightMeters', label: 'Höhe (m)', type: 'number', half: true, help: 'Für LKW-Routing (Brücken/Durchfahrten).' },
      { key: 'widthMeters', label: 'Breite (m)', type: 'number', half: true },
      { key: 'lengthMeters', label: 'Länge (m)', type: 'number', half: true },
      { key: 'weightTons', label: 'Gewicht (t)', type: 'number', half: true, help: 'Für gewichtsbeschränkte Straßen.' },
      { key: 'maxLoadTons', label: 'Max. Zuladung (t)', type: 'number', half: true },
      { key: 'allowedWasteCategories', label: 'Freigegebene Abfallarten', type: 'waste-multi', required: true, help: 'Das Fahrzeug darf nur diese Abfallarten sammeln.' },
      { key: 'isActive', label: 'Aktiv', type: 'checkbox' },
    ],
    toForm: r => ({
      name: r.name, licensePlate: r.licensePlate, vehicleType: r.vehicleType, capacity: r.capacity ?? '',
      heightMeters: r.heightMeters ?? '', widthMeters: r.widthMeters ?? '', lengthMeters: r.lengthMeters ?? '',
      weightTons: r.weightTons ?? '', maxLoadTons: r.maxLoadTons ?? '',
      allowedWasteCategories: r.allowedWasteCategories ?? [], isActive: r.isActive,
    }),
    buildBody: v => ({
      ...v, capacity: num(v.capacity), heightMeters: num(v.heightMeters), widthMeters: num(v.widthMeters),
      lengthMeters: num(v.lengthMeters), weightTons: num(v.weightTons), maxLoadTons: num(v.maxLoadTons),
      allowedWasteCategories: v.allowedWasteCategories ?? [],
    }),
    create: (api, b) => api.createVehicle(b),
    update: (api, id, b) => api.updateVehicle(id, b),
  },

  drivers: {
    singular: 'Fahrer', article: 'den',
    fields: [
      { key: 'fullName', label: 'Name', type: 'text', required: true, placeholder: 'Frank Fahrer' },
      { key: 'phoneNumber', label: 'Telefon', type: 'text', half: true, placeholder: '+49 170 …' },
      { key: 'licenseNumber', label: 'Führerscheinnummer', type: 'text', half: true },
      { key: 'isActive', label: 'Aktiv', type: 'checkbox' },
    ],
    toForm: r => ({ fullName: r.fullName, phoneNumber: r.phoneNumber ?? '', licenseNumber: r.licenseNumber ?? '', isActive: r.isActive }),
    buildBody: v => ({ ...v, userId: null }),
    create: (api, b) => api.createDriver(b),
    update: (api, id, b) => api.updateDriver(id, b),
  },

  destinations: {
    singular: 'Ziel', article: 'das',
    fields: [
      { key: 'name', label: 'Name', type: 'text', required: true, placeholder: 'Recyclinghof Nord' },
      { key: 'type', label: 'Typ', type: 'select', required: true, half: true, options: opts(destinationTypeLabel) },
      { key: 'openingHours', label: 'Öffnungszeiten', type: 'text', half: true, placeholder: 'Mo–Fr 07–16 Uhr' },
      { key: 'address', label: 'Adresse', type: 'text' },
      { key: 'latitude', label: 'Breitengrad', type: 'number', half: true },
      { key: 'longitude', label: 'Längengrad', type: 'number', half: true },
      { key: 'acceptedWasteCategories', label: 'Angenommene Abfallarten', type: 'waste-multi' },
      { key: 'isActive', label: 'Aktiv', type: 'checkbox' },
    ],
    toForm: r => ({
      name: r.name, type: r.type, openingHours: r.openingHours ?? '', address: r.address ?? '',
      latitude: r.latitude ?? '', longitude: r.longitude ?? '',
      acceptedWasteCategories: r.acceptedWasteCategories ?? [], isActive: r.isActive,
    }),
    buildBody: v => ({ ...v, latitude: num(v.latitude), longitude: num(v.longitude), acceptedWasteCategories: v.acceptedWasteCategories ?? [] }),
    create: (api, b) => api.createDestination(b),
    update: (api, id, b) => api.updateDestination(id, b),
  },

  containers: {
    singular: 'Container', article: 'den',
    fields: [
      { key: 'containerNumber', label: 'Container-Nr.', type: 'text', required: true, half: true, placeholder: 'C-0001' },
      { key: 'wasteTypeId', label: 'Abfallart', type: 'select', required: true, half: true, optionsSource: 'wasteTypes' },
      { key: 'size', label: 'Größe', type: 'text', half: true, placeholder: '7 m³' },
      { key: 'status', label: 'Status', type: 'select', half: true, options: opts(containerStatusLabel) },
      { key: 'currentAddress', label: 'Aktueller Standort', type: 'text' },
      { key: 'latitude', label: 'Breitengrad', type: 'number', half: true },
      { key: 'longitude', label: 'Längengrad', type: 'number', half: true },
    ],
    toForm: r => ({
      containerNumber: r.containerNumber, wasteTypeId: r.wasteTypeId, size: r.size ?? '',
      status: r.status, currentAddress: r.currentAddress ?? '', latitude: r.latitude ?? '', longitude: r.longitude ?? '',
    }),
    buildBody: v => ({ ...v, status: v.status || 'Available', latitude: num(v.latitude), longitude: num(v.longitude), lastServicedAt: null }),
    create: (api, b) => api.createContainer(b),
    update: (api, id, b) => api.updateContainer(id, b),
  },

  orders: {
    singular: 'Auftrag', article: 'den',
    fields: [
      { key: 'orderNumber', label: 'Auftragsnummer', type: 'text', required: true, half: true, placeholder: 'A-1003' },
      { key: 'orderType', label: 'Auftragsart', type: 'select', required: true, half: true, options: opts(orderTypeLabel) },
      { key: 'wasteTypeId', label: 'Abfallart', type: 'select', required: true, half: true, optionsSource: 'wasteTypes' },
      { key: 'pickupType', label: 'Abholart', type: 'select', required: true, half: true, options: opts(pickupTypeLabel) },
      { key: 'customerName', label: 'Kunde', type: 'text', required: true, half: true },
      { key: 'customerPhone', label: 'Telefon', type: 'text', half: true },
      { key: 'address', label: 'Adresse', type: 'text', required: true },
      { key: 'requestedDate', label: 'Wunschtermin', type: 'date', half: true },
      { key: 'notes', label: 'Anmerkungen', type: 'textarea' },
    ],
    toForm: r => ({
      orderNumber: r.orderNumber, orderType: r.orderType, wasteTypeId: r.wasteTypeId, pickupType: r.pickupType,
      customerName: r.customerName, customerPhone: r.customerPhone ?? '', address: r.address,
      requestedDate: r.requestedDate ?? '', notes: r.notes ?? '',
    }),
    buildBody: v => ({ ...v, containerId: null, latitude: null, longitude: null, requestedDate: v.requestedDate || null }),
    create: (api, b) => api.createOrder(b),
    update: (api, id, b) => api.updateOrder(id, b),
  },
};
