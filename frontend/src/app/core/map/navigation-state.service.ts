import { Injectable, computed, signal } from '@angular/core';
import { GeoPoint } from '../models';

export type GeoState = 'idle' | 'searching' | 'active' | 'denied' | 'unavailable';

export const geoStateLabel: Record<GeoState, string> = {
  idle: '',
  searching: 'Standort wird gesucht …',
  active: 'Standort aktiv',
  denied: 'Standort nicht erlaubt',
  unavailable: 'Standort nicht verfügbar',
};

/** Straight-line meters between two points (good enough for stop distance/off-route). */
export function haversineMeters(a: GeoPoint, b: GeoPoint): number {
  const R = 6_371_000;
  const dLat = ((b.latitude - a.latitude) * Math.PI) / 180;
  const dLng = ((b.longitude - a.longitude) * Math.PI) / 180;
  const la1 = (a.latitude * Math.PI) / 180;
  const la2 = (b.latitude * Math.PI) / 180;
  const h = Math.sin(dLat / 2) ** 2 + Math.cos(la1) * Math.cos(la2) * Math.sin(dLng / 2) ** 2;
  return 2 * R * Math.asin(Math.min(1, Math.sqrt(h)));
}

/**
 * In-app navigation state for the driver screen (component-provided, Signals):
 * navigation mode, browser geolocation (never blocks stop actions when denied),
 * distance to the next stop, and conservative off-route detection — disabled
 * without route geometry, so it can never raise a false warning.
 */
@Injectable()
export class NavigationStateService {
  static readonly OFF_ROUTE_THRESHOLD_M = 150;

  readonly navMode = signal(false);
  readonly geoState = signal<GeoState>('idle');
  readonly position = signal<GeoPoint | null>(null);
  /** Demo mode: position moves along the route geometry — always labeled as simulated. */
  readonly simulated = signal(false);
  /** Camera follows the driver; paused when the user pans the map manually. */
  readonly follow = signal(false);
  /** Next-stop coordinates (set by the screen). */
  readonly target = signal<GeoPoint | null>(null);
  /** Route geometry used for off-route checks (set when navigation data loads). */
  readonly routeGeometry = signal<GeoPoint[]>([]);

  /** Test seam: replaced in specs; defaults to the browser Geolocation API. */
  geoProvider: Pick<Geolocation, 'watchPosition' | 'clearWatch'> | null =
    typeof navigator !== 'undefined' && navigator.geolocation ? navigator.geolocation : null;

  private watchId: number | null = null;
  private simTimer: ReturnType<typeof setInterval> | null = null;
  private simIndex = 0;
  private simFraction = 0;
  readonly simPaused = signal(false);

  readonly geoLabel = computed(() =>
    this.simulated() ? 'Simulierter Standort aktiv' : geoStateLabel[this.geoState()]);

  /** Meters from the current position to the next stop, or null without both. */
  readonly distanceToTarget = computed(() => {
    const p = this.position(); const t = this.target();
    return p && t ? Math.round(haversineMeters(p, t)) : null;
  });

  /** True only when we have BOTH a position and geometry, and the position is
   * farther than the threshold from every geometry point. */
  readonly offRoute = computed(() => {
    const p = this.position(); const geo = this.routeGeometry();
    if (!p || geo.length < 2) return false; // no geometry → no false warnings
    let min = Number.POSITIVE_INFINITY;
    for (const g of geo) {
      const d = haversineMeters(p, g);
      if (d < min) min = d;
      if (min <= NavigationStateService.OFF_ROUTE_THRESHOLD_M) return false;
    }
    return min > NavigationStateService.OFF_ROUTE_THRESHOLD_M;
  });

  startNavigation() {
    this.navMode.set(true);
    this.follow.set(true);
    this.startWatch();
  }

  endNavigation() {
    this.navMode.set(false);
    this.follow.set(false);
    this.stopWatch();
    this.stopSimulation();
    this.geoState.set('idle');
    this.position.set(null);
  }

  /** „Standort erneut anfragen" — re-requests browser geolocation. */
  retry() {
    this.stopWatch();
    this.startWatch();
  }

  /** User panned the map → stop following until they recenter. */
  pauseFollow() { this.follow.set(false); }
  resumeFollow() { this.follow.set(true); }

  /**
   * Demo/dev only („Navigation simulieren", gated by NAV_CONFIG.enableSimulation):
   * moves a position SMOOTHLY along the route geometry (interpolated between
   * points). Never hidden — geoLabel says „Simulierter Standort aktiv".
   * @param intervalMs tick length; 4 ticks traverse one geometry segment.
   */
  startSimulation(intervalMs = 1000) {
    const geo = this.routeGeometry();
    if (geo.length < 2 || this.simTimer) return;
    this.stopWatch();
    this.simulated.set(true);
    this.simPaused.set(false);
    this.geoState.set('active');
    this.simIndex = 0;
    this.simFraction = 0;
    this.position.set(geo[0]);
    this.simTimer = setInterval(() => {
      if (this.simPaused()) return;
      this.simFraction += 0.25;
      if (this.simFraction >= 1) { this.simFraction = 0; this.simIndex++; }
      if (this.simIndex >= geo.length - 1) {
        this.position.set(geo[geo.length - 1]);
        this.stopSimulation(/* keepPosition */ true);
        return;
      }
      const a = geo[this.simIndex]; const b = geo[this.simIndex + 1]; const t = this.simFraction;
      this.position.set({
        latitude: a.latitude + (b.latitude - a.latitude) * t,
        longitude: a.longitude + (b.longitude - a.longitude) * t,
      });
    }, intervalMs / 4);
  }

  pauseSimulation() { if (this.simulated()) this.simPaused.set(true); }
  resumeSimulation() { this.simPaused.set(false); }

  stopSimulation(keepPosition = false) {
    if (this.simTimer) { clearInterval(this.simTimer); this.simTimer = null; }
    this.simPaused.set(false);
    if (!keepPosition && this.simulated()) { this.position.set(null); this.geoState.set('idle'); }
    if (!keepPosition) this.simulated.set(false);
  }

  private startWatch() {
    if (!this.geoProvider) { this.geoState.set('unavailable'); return; }
    this.geoState.set('searching');
    this.watchId = this.geoProvider.watchPosition(
      pos => this.handlePosition(pos.coords.latitude, pos.coords.longitude),
      err => this.handleError(err.code),
      { enableHighAccuracy: true, maximumAge: 5000, timeout: 20000 },
    );
  }

  private stopWatch() {
    if (this.watchId !== null && this.geoProvider) this.geoProvider.clearWatch(this.watchId);
    this.watchId = null;
  }

  /** Public for tests: a GPS fix arrived. */
  handlePosition(latitude: number, longitude: number) {
    this.position.set({ latitude, longitude });
    this.geoState.set('active');
  }

  /** Public for tests: geolocation failed. code 1 = permission denied. */
  handleError(code: number) {
    this.geoState.set(code === 1 ? 'denied' : 'unavailable');
    this.position.set(null);
  }
}
