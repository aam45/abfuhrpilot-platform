import { createMapProvider, navigationUrl, MapMarker } from './map-provider';

describe('map-provider', () => {
  const markers: MapMarker[] = [
    { lat: 52.52, lng: 13.40, label: '1', tone: 'open' },
    { lat: 52.53, lng: 13.41, label: '2', tone: 'done' },
  ];

  it('defaults to the schematic provider (no key, no tiles)', () => {
    const p = createMapProvider({ provider: 'schematic' });
    expect(p.name).toBe('schematic');
    expect(p.hasTiles).toBeFalse();
    expect(p.staticImageUrl(markers, 640, 360)).toBeNull();
  });

  it('google provider returns null without a key', () => {
    const p = createMapProvider({ provider: 'google', apiKey: '' });
    expect(p.staticImageUrl(markers, 640, 360)).toBeNull();
  });

  it('google provider builds a static-map URL with a key', () => {
    const p = createMapProvider({ provider: 'google', apiKey: 'KEY123' });
    const url = p.staticImageUrl(markers, 640, 360)!;
    expect(url).toContain('maps.googleapis.com/maps/api/staticmap');
    expect(url).toContain('key=KEY123');
    expect(url).toContain('52.52,13.4');
  });

  it('mapbox provider builds an overlay URL with a token', () => {
    const p = createMapProvider({ provider: 'mapbox', apiKey: 'TOK' });
    const url = p.staticImageUrl(markers, 640, 360)!;
    expect(url).toContain('api.mapbox.com');
    expect(url).toContain('access_token=TOK');
  });

  it('leaflet provider is interactive and exposes a keyless OSM tile layer', () => {
    const p = createMapProvider({ provider: 'leaflet' });
    expect(p.interactive).toBeTrue();
    expect(p.hasTiles).toBeTrue();
    const tiles = p.tileLayer();
    expect(tiles).not.toBeNull();
    expect(tiles!.urlTemplate).toContain('tile.openstreetmap.org');
    expect(p.staticImageUrl(markers, 640, 360)).toBeNull(); // interactive → no static image
  });

  it('non-interactive providers expose no tile layer', () => {
    expect(createMapProvider({ provider: 'schematic' }).tileLayer()).toBeNull();
    expect(createMapProvider({ provider: 'google', apiKey: 'K' }).tileLayer()).toBeNull();
    expect(createMapProvider({ provider: 'schematic' }).interactive).toBeFalse();
  });

  it('navigationUrl uses coordinates when present, else a text search', () => {
    expect(navigationUrl(52.5, 13.4)).toContain('destination=52.5,13.4');
    expect(navigationUrl(null, null, 'Hauptstraße 1')).toContain('query=Hauptstra');
  });
});
