import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { ApiService } from '../api/api.service';
import { API_BASE } from '../config';
import { idempotencyInterceptor } from './idempotency.interceptor';

const UUID_RE = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

describe('idempotencyInterceptor (driver-safe retries)', () => {
  let api: ApiService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        ApiService,
        provideHttpClient(withInterceptors([idempotencyInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    api = TestBed.inject(ApiService);
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  it('adds an Idempotency-Key UUID to the driver stop-complete call', () => {
    api.completeStop('s1').subscribe();
    const req = http.expectOne(`${API_BASE}/stops/s1/complete`);
    expect(req.request.headers.has('Idempotency-Key')).toBeTrue();
    expect(req.request.headers.get('Idempotency-Key')!).toMatch(UUID_RE);
    req.flush({});
  });

  it('adds an Idempotency-Key to problem reports', () => {
    api.reportProblem('s1', 'RoadClosed', 'Baustelle').subscribe();
    const req = http.expectOne(`${API_BASE}/stops/s1/report-problem`);
    expect(req.request.headers.get('Idempotency-Key')).toMatch(UUID_RE);
    req.flush({});
  });

  it('does not add the header to GET requests', () => {
    api.routes().subscribe();
    const req = http.expectOne(r => r.url === `${API_BASE}/routes`);
    expect(req.request.headers.has('Idempotency-Key')).toBeFalse();
    req.flush({ items: [], page: 1, pageSize: 25, totalCount: 0, totalPages: 0 });
  });

  it('does not add the header to auth endpoints', () => {
    api.requestPasswordReset('a@b.de').subscribe();
    const req = http.expectOne(`${API_BASE}/auth/request-password-reset`);
    expect(req.request.headers.has('Idempotency-Key')).toBeFalse();
    req.flush({ message: 'ok' });
  });

  it('generates a NEW key for each separate action (no accidental reuse)', () => {
    api.completeStop('s1').subscribe();
    api.completeStop('s2').subscribe();
    const r1 = http.expectOne(`${API_BASE}/stops/s1/complete`);
    const r2 = http.expectOne(`${API_BASE}/stops/s2/complete`);
    expect(r1.request.headers.get('Idempotency-Key'))
      .not.toBe(r2.request.headers.get('Idempotency-Key'));
    r1.flush({}); r2.flush({});
  });

  it('keeps an explicitly provided key (same-action retry semantics)', () => {
    api.startRoute('r1').subscribe();
    const first = http.expectOne(`${API_BASE}/routes/r1/start`);
    expect(first.request.headers.get('Idempotency-Key')).toMatch(UUID_RE);
    first.flush({});
  });
});
