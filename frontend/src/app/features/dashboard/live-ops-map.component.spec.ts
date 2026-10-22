import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { LiveOpsMapComponent } from './live-ops-map.component';
import { RouteDto, Stop } from '../../core/models';
import { API_BASE } from '../../core/config';

function route(overrides: Partial<RouteDto> = {}): RouteDto {
  return {
    id: 'r1', routeNumber: 'R-001', name: 'Hausmüll Innenstadt', date: '2026-07-06',
    wasteTypeId: 'w1', wasteTypeName: 'Hausmüll', status: 'InProgress',
    stopCount: 4, doneCount: 2, problemCount: 0,
    ...overrides,
  };
}

function stop(overrides: Partial<Stop> = {}): Stop {
  return {
    id: 's1', routeId: 'r1', sequenceNumber: 1, address: 'Hauptstraße 1',
    latitude: 52.52, longitude: 13.4, wasteTypeId: 'w1', pickupType: 'RegularBinPickup', status: 'Open',
    ...overrides,
  };
}

describe('LiveOpsMapComponent', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [LiveOpsMapComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  const text = (fixture: any) => (fixture.nativeElement as HTMLElement).textContent ?? '';

  /** `routes` is set directly on the instance (not via a template binding), so
   * `ngOnChanges` — which Angular would otherwise fire automatically — must be
   * invoked explicitly, matching the existing `ngOnChanges`-spec pattern. */
  function setRoutes(fixture: any, routes: RouteDto[]) {
    fixture.componentInstance.routes = routes;
    fixture.componentInstance.ngOnChanges({
      routes: { currentValue: routes, previousValue: null, firstChange: true, isFirstChange: () => true },
    });
  }

  it('shows the honest empty state when there are no active routes', () => {
    const fixture = TestBed.createComponent(LiveOpsMapComponent);
    setRoutes(fixture, []);
    fixture.detectChanges();
    expect(text(fixture)).toContain('Keine aktive Tour');
  });

  it('shows the honest empty state when active routes have no stop coordinates', () => {
    const fixture = TestBed.createComponent(LiveOpsMapComponent);
    setRoutes(fixture, [route()]);
    fixture.detectChanges();
    http.expectOne(`${API_BASE}/routes/r1/stops`).flush([stop({ latitude: null, longitude: null })]);
    fixture.detectChanges();
    expect(text(fixture)).toContain('Keine Koordinaten für aktive Touren hinterlegt');
  });

  it('builds fraction-colored marker groups from routes with coordinates', () => {
    const fixture = TestBed.createComponent(LiveOpsMapComponent);
    const r = route();
    setRoutes(fixture, [r]);
    fixture.detectChanges();
    http.expectOne(`${API_BASE}/routes/r1/stops`).flush([stop()]);
    fixture.detectChanges();

    const groups = fixture.componentInstance.markerGroups();
    expect(groups.length).toBe(1);
    expect(groups[0].route.routeNumber).toBe('R-001');
    expect(groups[0].color).toBe('var(--w-hausmuell)');
    expect(groups[0].stops.length).toBe(1);
  });

  it('computes the quiet stats line from active-route totals only', () => {
    const fixture = TestBed.createComponent(LiveOpsMapComponent);
    const routes = [route({ id: 'r1', doneCount: 2, stopCount: 4 }), route({ id: 'r2', routeNumber: 'R-002', doneCount: 1, stopCount: 3 })];
    setRoutes(fixture, routes);
    fixture.detectChanges();
    http.expectOne(`${API_BASE}/routes/r1/stops`).flush([stop({ routeId: 'r1' })]);
    http.expectOne(`${API_BASE}/routes/r2/stops`).flush([stop({ id: 's2', routeId: 'r2', latitude: 52.53, longitude: 13.41 })]);
    fixture.detectChanges();

    expect(fixture.componentInstance.statsLine()).toBe('3/7 Stopps · 2 Touren aktiv');
  });

  it('re-initializes the map after the container is torn down and re-created (routes go empty then active again)', () => {
    const fixture = TestBed.createComponent(LiveOpsMapComponent);
    setRoutes(fixture, [route()]);
    fixture.detectChanges();
    http.expectOne(`${API_BASE}/routes/r1/stops`).flush([stop()]);
    fixture.detectChanges();
    expect(fixture.componentInstance.markerGroups().length).toBe(1);

    // Routes go empty — the *ngIf host (and #mapEl) is destroyed.
    setRoutes(fixture, []);
    fixture.detectChanges();
    expect(text(fixture)).toContain('Keine aktive Tour');

    // Routes become active again — #mapEl re-appears; must not stay stuck blank.
    setRoutes(fixture, [route()]);
    fixture.detectChanges();
    http.expectOne(`${API_BASE}/routes/r1/stops`).flush([stop()]);
    fixture.detectChanges();

    expect(fixture.componentInstance.markerGroups().length).toBe(1);
    expect((fixture.componentInstance as any).map).toBeTruthy();
  });

  it('a failed stop fetch for one route degrades gracefully without blocking the others', () => {
    const fixture = TestBed.createComponent(LiveOpsMapComponent);
    const routes = [route({ id: 'r1' }), route({ id: 'r2', routeNumber: 'R-002' })];
    setRoutes(fixture, routes);
    fixture.detectChanges();
    http.expectOne(`${API_BASE}/routes/r1/stops`).flush({ error: 'boom' }, { status: 500, statusText: 'Server Error' });
    http.expectOne(`${API_BASE}/routes/r2/stops`).flush([stop({ id: 's2', routeId: 'r2' })]);
    fixture.detectChanges();

    const groups = fixture.componentInstance.markerGroups();
    expect(groups.length).toBe(1);
    expect(groups[0].route.routeNumber).toBe('R-002');
  });
});
