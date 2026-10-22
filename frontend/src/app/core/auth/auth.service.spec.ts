import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter, Router } from '@angular/router';
import { AuthService } from './auth.service';
import { API_BASE } from '../config';
import { AuthResponse } from '../models';

function authResponse(role: any = 'Admin'): AuthResponse {
  return {
    token: 'access-tok', expiresAt: new Date(Date.now() + 3600e3).toISOString(),
    refreshToken: 'refresh-tok', refreshExpiresAt: new Date(Date.now() + 14 * 864e5).toISOString(),
    user: { id: 'u1', fullName: 'Anna', email: 'a@b.de', role, companyId: 'c1', driverId: null, isActive: true },
  };
}

describe('AuthService', () => {
  let service: AuthService;
  let http: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [AuthService, provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    service = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
    spyOn(TestBed.inject(Router), 'navigateByUrl').and.resolveTo(true); // no real navigation in tests
  });
  afterEach(() => http.verify());

  it('stores token, refresh token and user on login', async () => {
    const p = service.login('a@b.de', 'pw');
    http.expectOne(`${API_BASE}/auth/login`).flush(authResponse());
    await p;
    expect(service.token).toBe('access-tok');
    expect(localStorage.getItem('ap_refresh')).toBe('refresh-tok');
    expect(service.isAuthenticated()).toBeTrue();
    expect(service.hasRole('Admin')).toBeTrue();
    expect(service.hasRole('Driver')).toBeFalse();
  });

  it('flags driver role correctly', async () => {
    const p = service.login('d@b.de', 'pw');
    http.expectOne(`${API_BASE}/auth/login`).flush(authResponse('Driver'));
    await p;
    expect(service.isDriver()).toBeTrue();
  });

  it('shares a single in-flight refresh request', () => {
    localStorage.setItem('ap_refresh', 'refresh-tok');
    let a: string | null = 'x', b: string | null = 'y';
    service.refresh().subscribe(v => (a = v));
    service.refresh().subscribe(v => (b = v));
    const reqs = http.match(`${API_BASE}/auth/refresh`);
    expect(reqs.length).toBe(1); // single-flight
    reqs[0].flush(authResponse());
    expect(a).toBe('access-tok');
    expect(b).toBe('access-tok');
  });

  it('clears the session and calls the server on logout', () => {
    localStorage.setItem('ap_refresh', 'refresh-tok');
    (service as any)._token.set('access-tok');
    service.logout();
    http.expectOne(`${API_BASE}/auth/logout`).flush({});
    expect(service.token).toBeNull();
    expect(localStorage.getItem('ap_refresh')).toBeNull();
  });
});
