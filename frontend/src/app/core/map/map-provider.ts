/**
 * Map-provider abstraction. The app is never hard-wired to one map vendor:
 * a provider is chosen from configuration and provides EITHER
 *  - an interactive tile layer (Leaflet/OSM — pan/zoom, no key), or
 *  - a static-map image URL (Google / Mapbox / OSM-static), or
 *  - nothing (`null`) → the UI falls back to a dependency-free schematic plot.
 * New vendors implement the same contract without touching the components.
 */
export interface MapMarker {
  lat: number;
  lng: number;
  label: string;   // usually the stop sequence number
  tone: string;    // open | progress | done | skip | problem
}

export interface TileLayerSpec {
  urlTemplate: string;
  attribution: string;
  maxZoom: number;
}

export interface MapProvider {
  readonly name: string;
  /** True if this provider renders real map tiles (vs. the schematic fallback). */
  readonly hasTiles: boolean;
  /** True if this provider renders an interactive (pan/zoom) tile layer. */
  readonly interactive: boolean;
  /** Raster tile layer for interactive providers, else null. */
  tileLayer(): TileLayerSpec | null;
  /** A static-map image URL for the markers, or null → caller renders the schematic. */
  staticImageUrl(markers: MapMarker[], width: number, height: number): string | null;
}

export interface MapConfig {
  provider: 'schematic' | 'leaflet' | 'google' | 'mapbox' | 'osm';
  apiKey?: string;
}

const toneHex: Record<string, string> = {
  open: '64748b', progress: '2563eb', done: '16a34a', skip: 'a16207', problem: 'dc2626',
};

/** Default: no external key, no dependency — the component draws a schematic. */
class SchematicProvider implements MapProvider {
  readonly name = 'schematic';
  readonly hasTiles = false;
  readonly interactive = false;
  tileLayer() { return null; }
  staticImageUrl() { return null; }
}

/** Interactive Leaflet map on OpenStreetMap raster tiles — free, no API key. */
class LeafletOsmProvider implements MapProvider {
  readonly name = 'leaflet-osm';
  readonly hasTiles = true;
  readonly interactive = true;
  tileLayer(): TileLayerSpec {
    return {
      urlTemplate: 'https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png',
      attribution: '© OpenStreetMap-Mitwirkende',
      maxZoom: 19,
    };
  }
  staticImageUrl() { return null; }
}

class GoogleStaticProvider implements MapProvider {
  readonly name = 'google';
  readonly hasTiles = true;
  readonly interactive = false;
  constructor(private key: string) {}
  tileLayer() { return null; }
  staticImageUrl(markers: MapMarker[], width: number, height: number): string | null {
    if (!this.key || !markers.length) return null;
    const pins = markers
      .map(m => `markers=color:0x${toneHex[m.tone] ?? '64748b'}%7Clabel:${encodeURIComponent(m.label)}%7C${m.lat},${m.lng}`)
      .join('&');
    return `https://maps.googleapis.com/maps/api/staticmap?size=${width}x${height}&scale=2&${pins}&key=${this.key}`;
  }
}

class MapboxStaticProvider implements MapProvider {
  readonly name = 'mapbox';
  readonly hasTiles = true;
  readonly interactive = false;
  constructor(private token: string) {}
  tileLayer() { return null; }
  staticImageUrl(markers: MapMarker[], width: number, height: number): string | null {
    if (!this.token || !markers.length) return null;
    const overlay = markers
      .map(m => `pin-s-${(m.label || '1').slice(0, 1)}+${toneHex[m.tone] ?? '64748b'}(${m.lng},${m.lat})`)
      .join(',');
    return `https://api.mapbox.com/styles/v1/mapbox/streets-v12/static/${overlay}/auto/${width}x${height}@2x?access_token=${this.token}`;
  }
}

class OsmStaticProvider implements MapProvider {
  readonly name = 'osm';
  readonly hasTiles = true;
  readonly interactive = false;
  tileLayer() { return null; }
  staticImageUrl(markers: MapMarker[], width: number, height: number): string | null {
    if (!markers.length) return null;
    // Public, key-less static renderer. Reliability varies → still safe: on error
    // the component's <img> handler falls back to the schematic view.
    const pins = markers.map(m => `markers=${m.lat},${m.lng},lightblue${m.label}`).join('&');
    return `https://staticmap.openstreetmap.de/staticmap.php?size=${width}x${height}&${pins}`;
  }
}

export function createMapProvider(config: MapConfig): MapProvider {
  switch (config.provider) {
    case 'leaflet': return new LeafletOsmProvider();
    case 'google': return new GoogleStaticProvider(config.apiKey ?? '');
    case 'mapbox': return new MapboxStaticProvider(config.apiKey ?? '');
    case 'osm': return new OsmStaticProvider();
    default: return new SchematicProvider();
  }
}

export const toneColor = (tone: string) => `#${toneHex[tone] ?? '64748b'}`;

/** Builds an external turn-by-turn navigation link (Google Maps). */
export function navigationUrl(lat?: number | null, lng?: number | null, address?: string): string {
  if (lat != null && lng != null) return `https://www.google.com/maps/dir/?api=1&destination=${lat},${lng}`;
  return `https://www.google.com/maps/search/?api=1&query=${encodeURIComponent(address ?? '')}`;
}
