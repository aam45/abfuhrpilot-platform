import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { DriverComponent, derivePrimaryAction } from './driver.component';
import { NavigationStateService } from '../../core/map/navigation-state.service';
import { API_BASE } from '../../core/config';

const ROUTE_ID = 'r1';

// Stops WITHOUT coordinates → map renders its empty state (no Leaflet in tests).
function dashboard(status = 'InProgress') {
  const stop = {
    id: 's1', routeId: ROUTE_ID, sequenceNumber: 1, address: 'Hauptstraße 1',
    latitude: null, longitude: null, wasteTypeId: 'w1', pickupType: 'RegularBinPickup', status: 'Open',
  };
  return {
    todaysRoute: {
      id: ROUTE_ID, routeNumber: 'R-001', name: 'Hausmüll Innenstadt', date: '2026-07-04',
      wasteTypeId: 'w1', wasteTypeName: 'Hausmüll', vehicleId: 'v1', vehicleName: 'Presswagen 1',
      driverId: 'd1', status, stopCount: 1, doneCount: 0, problemCount: 0,
    },
    nextStop: stop, stops: [stop], doneCount: 0, totalCount: 1,
  };
}

const navLinks = { googleMaps: 'https://www.google.com/maps/dir/x', appleMaps: 'https://maps.apple.com/x', openStreetMap: 'https://osm.org/x' };

const fallbackNavigation = {
  routeId: ROUTE_ID, provider: 'LocalFallback', providerConfigured: false,
  totalDistanceMeters: 1200, totalDurationSeconds: 140,
  geometry: [{ latitude: 52.52, longitude: 13.40 }, { latitude: 52.53, longitude: 13.41 }],
  legs: [{ fromStopId: 's1', toStopId: 's2', fromLabel: 'A', toLabel: 'B', distanceMeters: 1200, durationSeconds: 140, steps: [] }],
  warnings: [{ code: 'TURN_BY_TURN_STEPS_NOT_AVAILABLE', message: 'Lokaler Fallback: Keine echten Abbiegehinweise verfügbar.', severity: 'Warning' }],
  routingProfileUsed: 'straight-line', truckRestrictionsApplied: false,
};

describe('DriverComponent — in-app navigation', () => {
  let http: HttpTestingController;

  function mount() {
    const fixture = TestBed.createComponent(DriverComponent);
    // Deterministic geolocation: no real browser prompt in tests.
    const nav = fixture.debugElement.injector.get(NavigationStateService);
    nav.geoProvider = { watchPosition: () => 7, clearWatch: () => {} };
    fixture.detectChanges(); // ngOnInit → dashboard load
    http.expectOne(`${API_BASE}/dashboard/driver`).flush(dashboard());
    fixture.detectChanges();
    http.expectOne(`${API_BASE}/routes/${ROUTE_ID}/navigation-links`).flush(navLinks);
    fixture.detectChanges();
    return { fixture, nav };
  }

  const text = (fixture: any) => (fixture.nativeElement as HTMLElement).textContent ?? '';

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [DriverComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  it('shows "Navigation starten" for an in-progress route', () => {
    const { fixture } = mount();
    expect(text(fixture)).toContain('Navigation starten');
  });

  it('external maps are a fallback link, never the primary CTA', () => {
    const { fixture } = mount();
    const primary: HTMLElement = fixture.nativeElement.querySelector('.prim');
    expect(primary.textContent).not.toContain('externer Karte');
    expect(primary.textContent).toContain('Stopp starten'); // in-app action is primary
    expect(text(fixture)).toContain('In externer Karte öffnen'); // fallback exists, in the sheet
  });

  it('navigation mode fetches in-app navigation and shows the HONEST fallback guidance', () => {
    const { fixture } = mount();
    fixture.componentInstance.toggleNavigation(ROUTE_ID);
    http.expectOne(`${API_BASE}/routes/${ROUTE_ID}/in-app-navigation`).flush(fallbackNavigation);
    fixture.detectChanges();
    expect(text(fixture)).toContain('Navigation beenden');
    expect(text(fixture)).toContain('Weiter zu Hauptstraße 1'); // guidance names the stop
    expect(text(fixture)).toContain('Abbiegehinweise vom Routing-Anbieter nicht verfügbar'); // no fake turns
    expect(text(fixture)).toContain('Entfernung verfügbar nach Standortfreigabe'); // no GPS fix yet
  });

  it('shows the navigation HUD (dark strip), not a dashboard card', () => {
    const { fixture } = mount();
    const hud: HTMLElement = fixture.nativeElement.querySelector('.hud');
    expect(hud).toBeTruthy();
    expect(hud.textContent).toContain('R-001');
    expect(hud.textContent).toContain('0 / 1');
  });

  it('GPS permission denied shows the actionable panel and never blocks actions', () => {
    const { fixture, nav } = mount();
    fixture.componentInstance.toggleNavigation(ROUTE_ID);
    http.expectOne(`${API_BASE}/routes/${ROUTE_ID}/in-app-navigation`).flush(fallbackNavigation);
    nav.handleError(1); // permission denied
    fixture.detectChanges();
    expect(text(fixture)).toContain('Standortfreigabe erforderlich');
    expect(text(fixture)).toContain('Standort erneut anfragen');
    expect(text(fixture)).toContain('Ohne Standort fortfahren');
    expect(text(fixture)).toContain('Navigation simulieren'); // simulate offered in the panel
    expect(fixture.nativeElement.querySelector('.prim')).toBeTruthy(); // primary action still there
    fixture.componentInstance.gpsDismissed.set(true); // „Ohne Standort fortfahren"
    fixture.detectChanges();
    expect(text(fixture)).not.toContain('Standortfreigabe erforderlich');
  });

  it('offers „Navigation simulieren" (config-gated) and labels the simulated position', () => {
    const { fixture, nav } = mount();
    fixture.componentInstance.toggleNavigation(ROUTE_ID);
    http.expectOne(`${API_BASE}/routes/${ROUTE_ID}/in-app-navigation`).flush(fallbackNavigation);
    fixture.detectChanges();
    expect(text(fixture)).toContain('Navigation simulieren'); // geometry present, no fix yet
    nav.startSimulation();
    fixture.detectChanges();
    expect(text(fixture)).toContain('Simulierter Standort aktiv'); // honesty label
    expect(text(fixture)).not.toContain('Navigation simulieren');
    nav.stopSimulation();
  });

  it('shows the recenter control once a GPS fix exists', () => {
    const { fixture, nav } = mount();
    fixture.componentInstance.toggleNavigation(ROUTE_ID);
    http.expectOne(`${API_BASE}/routes/${ROUTE_ID}/in-app-navigation`).flush(fallbackNavigation);
    nav.handlePosition(52.52, 13.40);
    fixture.detectChanges();
    const ctl: HTMLElement = fixture.nativeElement.querySelector('.map-ctl');
    expect(ctl).toBeTruthy();
    expect(ctl.getAttribute('aria-label')).toBe('Standort zentrieren');
  });

  it('distance to the next stop appears once GPS and stop coordinates exist', () => {
    const { fixture, nav } = mount();
    fixture.componentInstance.toggleNavigation(ROUTE_ID);
    http.expectOne(`${API_BASE}/routes/${ROUTE_ID}/in-app-navigation`).flush(fallbackNavigation);
    nav.target.set({ latitude: 52.53, longitude: 13.41 }); // as if the stop had coordinates
    nav.handlePosition(52.52, 13.40);
    fixture.detectChanges();
    expect(text(fixture)).toContain('bis zum Stopp');
  });

  it('daylight contrast: .ap-subtle on the driver surface uses the darkened AA override, not the global token', () => {
    const { fixture } = mount();
    const subtle: HTMLElement = fixture.nativeElement.querySelector('.ap-subtle');
    expect(subtle).toBeTruthy();
    // :host { --c-text-subtle:#697670 } → 4.74:1 on white (AA); global #93a09a is only ~2.7:1.
    expect(getComputedStyle(subtle).color).toBe('rgb(105, 118, 112)');
  });

  it('expanded-sheet timeline rail carries the tour fraction color via --rail', () => {
    const { fixture } = mount();
    fixture.componentInstance.ui.setSheetState('expanded');
    fixture.detectChanges();
    const timeline: HTMLElement = fixture.nativeElement.querySelector('ul.timeline');
    expect(timeline).toBeTruthy();
    // Mock tour is "Hausmüll" → wasteColorVar → var(--w-hausmuell). The connector's
    // color-mix tint reads this custom property; removing the binding kills the rail.
    expect(timeline.style.getPropertyValue('--rail')).toBe('var(--w-hausmuell)');
  });

  it('primary action still derives correctly (regression)', () => {
    expect(derivePrimaryAction({ status: 'InProgress' } as any, { status: 'Open' } as any, 0, 3)!.label)
      .toContain('Stopp starten');
  });

  // ---- end-of-tour destination CTA (completed state) ----

  /** Completed tour: no next stop, everything done. */
  function completedDashboard(routeOverrides: any = {}) {
    const stop = {
      id: 's1', routeId: ROUTE_ID, sequenceNumber: 1, address: 'Hauptstraße 1',
      latitude: null, longitude: null, wasteTypeId: 'w1', pickupType: 'RegularBinPickup', status: 'Done',
    };
    return {
      todaysRoute: {
        id: ROUTE_ID, routeNumber: 'R-001', name: 'Hausmüll Innenstadt', date: '2026-07-04',
        wasteTypeId: 'w1', wasteTypeName: 'Hausmüll', vehicleId: 'v1', vehicleName: 'Presswagen 1',
        driverId: 'd1', status: 'Completed', stopCount: 1, doneCount: 1, problemCount: 0,
        ...routeOverrides,
      },
      nextStop: null, stops: [stop], doneCount: 1, totalCount: 1,
    };
  }

  function mountCompleted(routeOverrides: any = {}) {
    const fixture = TestBed.createComponent(DriverComponent);
    const nav = fixture.debugElement.injector.get(NavigationStateService);
    nav.geoProvider = { watchPosition: () => 7, clearWatch: () => {} };
    fixture.detectChanges();
    http.expectOne(`${API_BASE}/dashboard/driver`).flush(completedDashboard(routeOverrides));
    fixture.detectChanges();
    http.expectOne(`${API_BASE}/routes/${ROUTE_ID}/navigation-links`).flush(navLinks);
    fixture.detectChanges();
    return fixture;
  }

  const destinationsPage = (items: any[]) =>
    ({ items, page: 1, pageSize: 200, totalCount: items.length, totalPages: 1 });

  it('completed tour with endDestinationId renders ONE dominant destination CTA with product label, name and coords', () => {
    const fixture = mountCompleted({ endDestinationId: 'dest-1' });
    http.expectOne(r => r.url === `${API_BASE}/destinations`).flush(destinationsPage([
      { id: 'dest-1', name: 'Recyclinghof Nord', type: 'RecyclingCenter', latitude: 52.61, longitude: 13.51, acceptedWasteCategories: [], isActive: true },
    ]));
    fixture.detectChanges();
    const cta: HTMLAnchorElement = fixture.nativeElement.querySelector('a.ap-btn--primary.prim');
    expect(cta).toBeTruthy();
    expect(cta.textContent).toContain('Zum Recyclinghof');
    expect(cta.href).toContain('52.61,13.51');
    expect(text(fixture)).toContain('Recyclinghof Nord'); // real name as the quiet adjacent line
    expect(text(fixture)).not.toContain('Gute Fahrt zurück zum Betriebshof');
  });

  it('completed tour falls back to the startDepotId depot when no end destination is set', () => {
    const fixture = mountCompleted({ startDepotId: 'depot-1' });
    http.expectOne(r => r.url === `${API_BASE}/destinations`).flush(destinationsPage([
      { id: 'depot-1', name: 'Betriebshof Mitte', type: 'Depot', latitude: 52.5, longitude: 13.4, acceptedWasteCategories: [], isActive: true },
    ]));
    fixture.detectChanges();
    const cta: HTMLAnchorElement = fixture.nativeElement.querySelector('a.ap-btn--primary.prim');
    expect(cta.textContent).toContain('Zum Betriebshof');
    expect(cta.href).toContain('52.5,13.4');
    expect(text(fixture)).toContain('Betriebshof Mitte');
  });

  it('completed tour without destination ids keeps the current quiet text — no CTA, no fetch', () => {
    const fixture = mountCompleted(); // neither endDestinationId nor startDepotId
    http.expectNone(r => r.url === `${API_BASE}/destinations`);
    expect(text(fixture)).toContain('Tour abgeschlossen');
    expect(text(fixture)).toContain('Gute Fahrt zurück zum Betriebshof');
    expect(fixture.nativeElement.querySelector('a.ap-btn--primary.prim')).toBeNull();
  });

  it('destination lookup error falls back to the quiet text — never a broken CTA', () => {
    const fixture = mountCompleted({ endDestinationId: 'dest-1' });
    http.expectOne(r => r.url === `${API_BASE}/destinations`)
      .flush({ error: 'boom' }, { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();
    expect(text(fixture)).toContain('Gute Fahrt zurück zum Betriebshof');
    expect(fixture.nativeElement.querySelector('a.ap-btn--primary.prim')).toBeNull();
  });

  it('re-completing with the SAME destination id after a non-completed reload refetches — the CTA must not die in a long-lived tab', () => {
    const activeDest = destinationsPage([
      { id: 'dest-1', name: 'Recyclinghof Nord', type: 'RecyclingCenter', latitude: 52.61, longitude: 13.51, acceptedWasteCategories: [], isActive: true },
    ]);
    // Day 1: completed → CTA shows.
    const fixture = mountCompleted({ endDestinationId: 'dest-1' });
    http.expectOne(r => r.url === `${API_BASE}/destinations`).flush(activeDest);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('a.ap-btn--primary.prim')).toBeTruthy();
    // Dispatch reopens the route: reload with a non-completed status → CTA clears.
    fixture.componentInstance.load();
    http.expectOne(`${API_BASE}/dashboard/driver`).flush(completedDashboard({ endDestinationId: 'dest-1', status: 'InProgress' }));
    http.expectOne(`${API_BASE}/routes/${ROUTE_ID}/navigation-links`).flush(navLinks);
    fixture.detectChanges();
    expect(fixture.componentInstance.destinationCta()).toBeNull();
    // Completed again with the SAME id → must fetch and render the CTA again (latch reset).
    fixture.componentInstance.load();
    http.expectOne(`${API_BASE}/dashboard/driver`).flush(completedDashboard({ endDestinationId: 'dest-1' }));
    http.expectOne(`${API_BASE}/routes/${ROUTE_ID}/navigation-links`).flush(navLinks);
    fixture.detectChanges();
    http.expectOne(r => r.url === `${API_BASE}/destinations`).flush(activeDest);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('a.ap-btn--primary.prim')).toBeTruthy();
  });

  it('endDestinationId wins over startDepotId when both are set', () => {
    const fixture = mountCompleted({ endDestinationId: 'dest-1', startDepotId: 'depot-1' });
    http.expectOne(r => r.url === `${API_BASE}/destinations`).flush(destinationsPage([
      { id: 'depot-1', name: 'Betriebshof Mitte', type: 'Depot', latitude: 52.5, longitude: 13.4, acceptedWasteCategories: [], isActive: true },
      { id: 'dest-1', name: 'Recyclinghof Nord', type: 'RecyclingCenter', latitude: 52.61, longitude: 13.51, acceptedWasteCategories: [], isActive: true },
    ]));
    fixture.detectChanges();
    const cta: HTMLAnchorElement = fixture.nativeElement.querySelector('a.ap-btn--primary.prim');
    expect(cta.textContent).toContain('Zum Recyclinghof');
    expect(text(fixture)).toContain('Recyclinghof Nord');
  });

  it('a deactivated destination never becomes a driver CTA — quiet text instead', () => {
    const fixture = mountCompleted({ endDestinationId: 'dest-1' });
    http.expectOne(r => r.url === `${API_BASE}/destinations`).flush(destinationsPage([
      { id: 'dest-1', name: 'Recyclinghof Nord', type: 'RecyclingCenter', latitude: 52.61, longitude: 13.51, acceptedWasteCategories: [], isActive: false },
    ]));
    fixture.detectChanges();
    expect(text(fixture)).toContain('Gute Fahrt zurück zum Betriebshof');
    expect(fixture.nativeElement.querySelector('a.ap-btn--primary.prim')).toBeNull();
  });

  it('problem-report flow: upload a photo, remove one, submit — reports the problem and reloads', () => {
    const { fixture } = mount();
    const component = fixture.componentInstance;
    const stop = dashboard().nextStop as any;

    component.openProblem(stop);
    fixture.detectChanges();

    // Select a photo → uploaded via ApiService.uploadProblemPhoto (multipart POST).
    const file = new File(['x'], 'evidence.jpg', { type: 'image/jpeg' });
    const dataTransfer = new DataTransfer();
    dataTransfer.items.add(file);
    const fileInput = { files: dataTransfer.files, value: '' } as unknown as HTMLInputElement;
    component.onPhotoSelected({ target: fileInput } as unknown as Event);
    http.expectOne(`${API_BASE}/problem-photos`).flush({ id: 'photo-1' });
    fixture.detectChanges();
    expect(component.photos().length).toBe(1);
    expect(component.photos()[0].id).toBe('photo-1');

    // Add a second photo, then remove it again — should never hit the network.
    const file2 = new File(['y'], 'evidence2.jpg', { type: 'image/jpeg' });
    const dataTransfer2 = new DataTransfer();
    dataTransfer2.items.add(file2);
    const fileInput2 = { files: dataTransfer2.files, value: '' } as unknown as HTMLInputElement;
    component.onPhotoSelected({ target: fileInput2 } as unknown as Event);
    http.expectOne(`${API_BASE}/problem-photos`).flush({ id: 'photo-2' });
    fixture.detectChanges();
    expect(component.photos().length).toBe(2);

    component.removePhoto(1);
    fixture.detectChanges();
    expect(component.photos().length).toBe(1);
    expect(component.photos()[0].id).toBe('photo-1');

    // Submit — reportProblem fires with the remaining photo id, then modal closes and dashboard reloads.
    component.submitProblem({ type: 'BlockedByCar', note: 'Auto blockiert Zufahrt' });
    const reportReq = http.expectOne(`${API_BASE}/stops/${stop.id}/report-problem`);
    expect(reportReq.request.body).toEqual({ problemType: 'BlockedByCar', description: 'Auto blockiert Zufahrt', photoIds: ['photo-1'] });
    reportReq.flush(stop);
    fixture.detectChanges();

    expect(component.problemFor()).toBeNull();

    http.expectOne(`${API_BASE}/dashboard/driver`).flush(dashboard());
    fixture.detectChanges();
    http.expectOne(`${API_BASE}/routes/${ROUTE_ID}/navigation-links`).flush(navLinks);
    fixture.detectChanges();
  });

  it('onPhotoSelected HTTP-error branch: a failed upload is marked error and excluded from photoIds on submit', () => {
    const { fixture } = mount();
    const component = fixture.componentInstance;
    const stop = dashboard().nextStop as any;
    component.openProblem(stop);
    fixture.detectChanges();

    // First photo uploads fine.
    const goodFile = new File(['x'], 'good.jpg', { type: 'image/jpeg' });
    const goodDT = new DataTransfer(); goodDT.items.add(goodFile);
    component.onPhotoSelected({ target: { files: goodDT.files, value: '' } as unknown as HTMLInputElement } as unknown as Event);
    http.expectOne(`${API_BASE}/problem-photos`).flush({ id: 'photo-good' });
    fixture.detectChanges();

    // Second photo's upload fails.
    const badFile = new File(['y'], 'bad.jpg', { type: 'image/jpeg' });
    const badDT = new DataTransfer(); badDT.items.add(badFile);
    component.onPhotoSelected({ target: { files: badDT.files, value: '' } as unknown as HTMLInputElement } as unknown as Event);
    http.expectOne(`${API_BASE}/problem-photos`).flush({ error: 'boom' }, { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    expect(component.photos().length).toBe(2);
    const failed = component.photos().find(p => p.name === 'bad.jpg');
    expect(failed?.error).toBeTrue();
    expect(failed?.uploading).toBeFalse();

    // Submit — only the successful photo id is sent, the failed one is excluded.
    component.submitProblem({ type: 'Other', note: 'x' });
    const req = http.expectOne(`${API_BASE}/stops/${stop.id}/report-problem`);
    expect(req.request.body.photoIds).toEqual(['photo-good']);
    req.flush(stop);
    fixture.detectChanges();
    http.expectOne(`${API_BASE}/dashboard/driver`).flush(dashboard());
    fixture.detectChanges();
    http.expectOne(`${API_BASE}/routes/${ROUTE_ID}/navigation-links`).flush(navLinks);
    fixture.detectChanges();
  });

  it('submitProblem is a no-op while a photo is still uploading', () => {
    const { fixture } = mount();
    const component = fixture.componentInstance;
    const stop = dashboard().nextStop as any;
    component.openProblem(stop);
    fixture.detectChanges();

    const file = new File(['x'], 'evidence.jpg', { type: 'image/jpeg' });
    const dt = new DataTransfer(); dt.items.add(file);
    component.onPhotoSelected({ target: { files: dt.files, value: '' } as unknown as HTMLInputElement } as unknown as Event);
    // No flush yet — upload is still in flight, so `uploading()` is true.
    expect(component.uploading()).toBeTrue();

    component.submitProblem({ type: 'Other', note: 'x' });
    http.expectNone(`${API_BASE}/stops/${stop.id}/report-problem`);
    expect(component.problemFor()).toBe(stop);

    // Drain the still-pending photo upload so `afterEach(http.verify())` stays clean.
    http.expectOne(`${API_BASE}/problem-photos`).flush({ id: 'photo-1' });
    fixture.detectChanges();
  });

  it('reportProblem HTTP-error path: busy resets and the modal stays open', () => {
    const { fixture } = mount();
    const component = fixture.componentInstance;
    const stop = dashboard().nextStop as any;
    component.openProblem(stop);
    fixture.detectChanges();

    component.submitProblem({ type: 'Other', note: 'x' });
    expect(component.busy()).toBeTrue();
    const req = http.expectOne(`${API_BASE}/stops/${stop.id}/report-problem`);
    req.flush({ error: 'boom' }, { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    expect(component.busy()).toBeFalse();
    expect(component.problemFor()).toBe(stop); // modal stays open on failure
  });
});
