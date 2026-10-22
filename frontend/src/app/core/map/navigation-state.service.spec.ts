import { NavigationStateService, haversineMeters } from './navigation-state.service';

describe('NavigationStateService (in-app navigation state)', () => {
  let svc: NavigationStateService;
  let cleared: number[];

  beforeEach(() => {
    svc = new NavigationStateService();
    cleared = [];
    svc.geoProvider = {
      watchPosition: () => 42,
      clearWatch: (id: number) => cleared.push(id),
    };
  });

  it('haversine distance is roughly correct (~111 km per degree of latitude)', () => {
    const d = haversineMeters({ latitude: 52, longitude: 13 }, { latitude: 53, longitude: 13 });
    expect(d).toBeGreaterThan(110_000);
    expect(d).toBeLessThan(112_500);
  });

  it('starts searching on navigation start and clears the watch on end', () => {
    svc.startNavigation();
    expect(svc.navMode()).toBeTrue();
    expect(svc.geoState()).toBe('searching');
    svc.endNavigation();
    expect(svc.navMode()).toBeFalse();
    expect(svc.geoState()).toBe('idle');
    expect(cleared).toEqual([42]);
  });

  it('maps geolocation errors to German states', () => {
    svc.handleError(1); // permission denied
    expect(svc.geoState()).toBe('denied');
    expect(svc.geoLabel()).toBe('Standort nicht erlaubt');
    svc.handleError(2);
    expect(svc.geoState()).toBe('unavailable');
    expect(svc.geoLabel()).toBe('Standort nicht verfügbar');
  });

  it('is unavailable when the browser has no geolocation', () => {
    svc.geoProvider = null;
    svc.startNavigation();
    expect(svc.geoState()).toBe('unavailable');
  });

  it('computes distance to the next stop from the GPS fix', () => {
    svc.target.set({ latitude: 52.52, longitude: 13.405 });
    expect(svc.distanceToTarget()).toBeNull(); // no position yet
    svc.handlePosition(52.52, 13.405);
    expect(svc.distanceToTarget()).toBe(0);
    svc.handlePosition(52.53, 13.405); // ~1.1 km north
    expect(svc.distanceToTarget()!).toBeGreaterThan(1000);
  });

  it('simulation steps along the route geometry and is always labeled as simulated', () => {
    jasmine.clock().install();
    try {
      svc.routeGeometry.set([
        { latitude: 52.52, longitude: 13.40 },
        { latitude: 52.53, longitude: 13.41 },
        { latitude: 52.54, longitude: 13.42 },
      ]);
      svc.startSimulation(1000);
      expect(svc.simulated()).toBeTrue();
      expect(svc.geoLabel()).toBe('Simulierter Standort aktiv'); // honesty label
      expect(svc.position()!.latitude).toBe(52.52);
      jasmine.clock().tick(1000);
      expect(svc.position()!.latitude).toBe(52.53);
      jasmine.clock().tick(1000);
      expect(svc.position()!.latitude).toBe(52.54); // reached the end
      svc.stopSimulation();
      expect(svc.simulated()).toBeFalse();
    } finally { jasmine.clock().uninstall(); }
  });

  it('simulation can be paused and resumed', () => {
    jasmine.clock().install();
    try {
      svc.routeGeometry.set([
        { latitude: 52.52, longitude: 13.40 },
        { latitude: 52.53, longitude: 13.41 },
      ]);
      svc.startSimulation(1000);
      svc.pauseSimulation();
      jasmine.clock().tick(2000);
      expect(svc.position()!.latitude).toBe(52.52); // frozen while paused
      expect(svc.simPaused()).toBeTrue();
      svc.resumeSimulation();
      jasmine.clock().tick(1000);
      expect(svc.position()!.latitude).toBeGreaterThan(52.52); // moving again
      svc.stopSimulation();
    } finally { jasmine.clock().uninstall(); }
  });

  it('simulation refuses to start without geometry (no fake movement)', () => {
    svc.startSimulation();
    expect(svc.simulated()).toBeFalse();
    expect(svc.position()).toBeNull();
  });

  it('follow mode: on with navigation, paused by user pan, resumed by recenter', () => {
    svc.startNavigation();
    expect(svc.follow()).toBeTrue();
    svc.pauseFollow();          // user dragged the map
    expect(svc.follow()).toBeFalse();
    svc.resumeFollow();         // „Standort zentrieren"
    expect(svc.follow()).toBeTrue();
    svc.endNavigation();
    expect(svc.follow()).toBeFalse();
  });

  it('off-route only fires beyond the threshold AND with geometry present', () => {
    // No geometry → never off-route (no false warnings).
    svc.handlePosition(50, 10);
    expect(svc.offRoute()).toBeFalse();

    svc.routeGeometry.set([
      { latitude: 52.52, longitude: 13.40 },
      { latitude: 52.53, longitude: 13.41 },
    ]);
    svc.handlePosition(52.5205, 13.4005); // ~70 m from the line start → on route
    expect(svc.offRoute()).toBeFalse();
    svc.handlePosition(52.54, 13.30);     // several km away → off route
    expect(svc.offRoute()).toBeTrue();
  });
});
