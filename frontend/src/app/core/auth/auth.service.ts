import { Injectable, computed, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, firstValueFrom, of } from 'rxjs';
import { catchError, map, shareReplay, tap } from 'rxjs/operators';
import { API_BASE } from '../config';
import { AuthResponse, User, UserRole } from '../models';

const TOKEN_KEY = 'ap_token';
const REFRESH_KEY = 'ap_refresh';
const USER_KEY = 'ap_user';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly _user = signal<User | null>(this.restoreUser());
  private readonly _token = signal<string | null>(localStorage.getItem(TOKEN_KEY));
  private refresh$: Observable<string | null> | null = null; // single-flight refresh

  readonly user = this._user.asReadonly();
  readonly isAuthenticated = computed(() => !!this._token());
  readonly role = computed<UserRole | null>(() => this._user()?.role ?? null);
  readonly isDriver = computed(() => this.role() === 'Driver');

  constructor(private http: HttpClient, private router: Router) {}

  get token(): string | null { return this._token(); }
  private get refreshToken(): string | null { return localStorage.getItem(REFRESH_KEY); }

  async login(email: string, password: string): Promise<void> {
    const res = await firstValueFrom(
      this.http.post<AuthResponse>(`${API_BASE}/auth/login`, { email, password }));
    this.setSession(res);
    this.router.navigateByUrl(res.user.role === 'Driver' ? '/fahrer' : '/dashboard');
  }

  async register(companyName: string, fullName: string, email: string, password: string): Promise<void> {
    const res = await firstValueFrom(
      this.http.post<AuthResponse>(`${API_BASE}/auth/register`, { companyName, fullName, email, password }));
    this.setSession(res);
    this.router.navigateByUrl('/dashboard');
  }

  /**
   * Exchanges the refresh token for a new access token. Concurrent callers share
   * one in-flight request (single-flight). Resolves to the new token or null.
   */
  refresh(): Observable<string | null> {
    if (this.refresh$) return this.refresh$;
    const rt = this.refreshToken;
    if (!rt) return of(null);

    this.refresh$ = this.http.post<AuthResponse>(`${API_BASE}/auth/refresh`, { refreshToken: rt }).pipe(
      tap(res => this.setSession(res)),
      map(res => res.token),
      catchError(() => { this.clearSession(); return of(null); }),
      tap(() => { this.refresh$ = null; }),
      shareReplay(1),
    );
    return this.refresh$;
  }

  logout(): void {
    const rt = this.refreshToken;
    if (rt) this.http.post(`${API_BASE}/auth/logout`, { refreshToken: rt }).subscribe({ error: () => {} });
    this.clearSession();
    this.router.navigateByUrl('/login');
  }

  hasRole(...roles: UserRole[]): boolean {
    const r = this.role();
    return !!r && roles.includes(r);
  }

  private setSession(res: AuthResponse): void {
    localStorage.setItem(TOKEN_KEY, res.token);
    localStorage.setItem(REFRESH_KEY, res.refreshToken);
    localStorage.setItem(USER_KEY, JSON.stringify(res.user));
    this._token.set(res.token);
    this._user.set(res.user);
  }

  private clearSession(): void {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(REFRESH_KEY);
    localStorage.removeItem(USER_KEY);
    this._token.set(null);
    this._user.set(null);
  }

  private restoreUser(): User | null {
    const raw = localStorage.getItem(USER_KEY);
    return raw ? JSON.parse(raw) as User : null;
  }
}
