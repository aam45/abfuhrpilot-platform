import { MapConfig } from './map/map-provider';

/** Runtime configuration. Point this at your running AbfuhrPilot API. */
export const API_BASE = 'http://localhost:5080/api/v1';

/**
 * Map provider. Default `leaflet` = interactive OpenStreetMap tiles, no API key.
 * Falls back to a dependency-free schematic when coordinates are missing or the
 * map fails to load. Switch to `google` / `mapbox` (add `apiKey`), `osm`
 * (static) or `schematic` — no other code changes required.
 */
export const MAP_CONFIG: MapConfig = {
  provider: 'leaflet',
  apiKey: '',
};

/** Navigation extras. `enableSimulation` shows „Navigation simulieren" (demo/dev
 * GPS along the route geometry, always labeled „Simulierter Standort aktiv").
 * Set to false for production deployments with real devices. */
export const NAV_CONFIG = { enableSimulation: true };
