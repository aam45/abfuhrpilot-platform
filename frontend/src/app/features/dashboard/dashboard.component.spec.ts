import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { DashboardComponent } from './dashboard.component';
import { OperationsDashboard } from '../../core/models';
import { API_BASE } from '../../core/config';

function ops(overrides: Partial<OperationsDashboard> = {}): OperationsDashboard {
  return {
    routesToday: 0, activeRoutes: 0, completedStops: 0, openStops: 0,
    problemStops: 0, totalStops: 0, openOrders: 0, vehiclesInUse: 0, todaysRoutes: [],
    ...overrides,
  };
}

describe('DashboardComponent — Betriebsleiste day-completion framing', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [DashboardComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  const text = (fixture: any) => (fixture.nativeElement as HTMLElement).textContent ?? '';

  function mount(dto: OperationsDashboard) {
    const fixture = TestBed.createComponent(DashboardComponent);
    fixture.detectChanges(); // ngOnInit → operations load
    http.expectOne(`${API_BASE}/dashboard/operations`).flush(dto);
    fixture.detectChanges();
    return fixture;
  }

  it('renders ONE "Stopps heute" item as completed/total using the backend day total', () => {
    const fixture = mount(ops({ completedStops: 48, openStops: 70, problemStops: 5, totalStops: 123 }));
    expect(text(fixture)).toContain('48/123');
    expect(text(fixture)).toContain('Stopps heute');
    // The two old separate items are gone.
    expect(text(fixture)).not.toContain('Stopps erledigt');
    expect(text(fixture)).not.toContain('Offene Stopps');
  });

  it('zero-data day renders an honest "0/0" — no NaN, no division', () => {
    const fixture = mount(ops());
    expect(text(fixture)).toContain('0/0');
    expect(text(fixture)).toContain('Stopps heute');
    expect(text(fixture)).not.toContain('NaN');
  });

  it('marks the metric done only when every counted stop is completed', () => {
    const fixture = mount(ops({ completedStops: 12, totalStops: 12 }));
    const component = fixture.componentInstance;
    expect(component.subStats()[0].value).toBe('12/12');
    expect(component.subStats()[0].tone).toBe('done');
  });

  it('stays neutral while stops are still open', () => {
    const fixture = mount(ops({ completedStops: 3, openStops: 4, problemStops: 1, totalStops: 8 }));
    expect(fixture.componentInstance.subStats()[0].value).toBe('3/8');
    expect(fixture.componentInstance.subStats()[0].tone).toBeUndefined();
  });

  it('skipped/in-progress stops widen the denominator — a day with skipped stops never reads done', () => {
    // 5 done, 0 open, 0 problem, but 2 skipped/in-progress → 5/7 and NOT the done tone.
    const fixture = mount(ops({ completedStops: 5, openStops: 0, problemStops: 0, totalStops: 7 }));
    expect(fixture.componentInstance.subStats()[0].value).toBe('5/7');
    expect(fixture.componentInstance.subStats()[0].tone).toBeUndefined();
  });

  it('Problemstellen banner renders straight off problemStops — the backend count may include unresolved problems from previous days (product decision 2026-07-07), the frontend adds no date assumption', () => {
    // problemStops > 0 with an otherwise-empty day (0/0 stops today) must still raise the alert:
    // an unresolved problem from yesterday does not expire at midnight.
    const fixture = mount(ops({ problemStops: 2 }));
    const banner = (fixture.nativeElement as HTMLElement).querySelector('.alert');
    expect(banner).withContext('exceptions-first alert banner').not.toBeNull();
    expect(banner!.textContent).toContain('2 Problemstellen');
  });

  it('no unresolved problems anywhere → no alert banner', () => {
    const fixture = mount(ops({ completedStops: 3, totalStops: 3 }));
    expect((fixture.nativeElement as HTMLElement).querySelector('.alert')).toBeNull();
  });

  it('renders the fraction donut from todaysRoutes; hides it on an empty day', () => {
    const route = (id: string, wasteTypeName: string) => ({
      id, routeNumber: `R-${id}`, name: `Tour ${id}`, date: '2026-07-08', wasteTypeId: 'w1',
      wasteTypeName, status: 'Planned' as const, stopCount: 5, doneCount: 0, problemCount: 0,
    });
    const fixture = mount(ops({
      routesToday: 2,
      todaysRoutes: [route('1', 'Hausmüll'), route('2', 'Papier')],
    }));
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('ap-waste-fraction-donut .fd')).withContext('donut card').not.toBeNull();
    expect(el.querySelectorAll('ap-waste-fraction-donut .legend li').length).toBe(2);

    const empty = mount(ops());
    expect((empty.nativeElement as HTMLElement).querySelector('ap-waste-fraction-donut .fd')).toBeNull();
  });
});
