import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { UsersComponent } from './users.component';
import { API_BASE } from '../../core/config';

const emptyPage = { items: [], page: 1, pageSize: 100, totalCount: 0, totalPages: 0 };

describe('UsersComponent (admin user management)', () => {
  let http: HttpTestingController;

  function init() {
    const fixture = TestBed.createComponent(UsersComponent);
    fixture.detectChanges(); // ngOnInit → initial list load
    http.expectOne(r => r.url === `${API_BASE}/users`).flush(emptyPage);
    return fixture.componentInstance;
  }

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      imports: [UsersComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  const PAGE = { items: [
    { id: 'u1', fullName: 'Anna Admin', email: 'a@x.de', role: 'Admin', companyId: 'c1', isActive: true },
    { id: 'u2', fullName: 'Doro Dispo', email: 'd@x.de', role: 'Dispatcher', companyId: 'c1', isActive: true },
    { id: 'u3', fullName: 'Fritz Fahrer', email: 'f@x.de', role: 'Driver', companyId: 'c1', isActive: false },
  ], page: 1, pageSize: 100, totalCount: 3, totalPages: 1 };

  function initWithRows() {
    const fixture = TestBed.createComponent(UsersComponent);
    fixture.detectChanges();
    http.expectOne(r => r.url === `${API_BASE}/users`).flush(PAGE);
    fixture.detectChanges();
    return fixture;
  }

  it('shows the console summary row (Betriebsleiste)', () => {
    const fixture = initWithRows();
    const text = (fixture.nativeElement as HTMLElement).textContent!;
    expect(fixture.nativeElement.querySelector('ap-summary-row')).toBeTruthy();
    expect(text).toContain('Benutzer');
    expect(text).toContain('Aktiv');
    expect(text).toContain('Administratoren');
  });

  it('role and status filter chips narrow the table client-side', () => {
    const fixture = initWithRows();
    const c = fixture.componentInstance;
    expect(c.filtered().length).toBe(3);
    c.roleFilter.set('Admin');
    expect(c.filtered().map(u => u.fullName)).toEqual(['Anna Admin']);
    c.roleFilter.set('all');
    c.statusFilter.set('inactive');
    expect(c.filtered().map(u => u.fullName)).toEqual(['Fritz Fahrer']);
  });

  it('renders labelled actions with aria-labels (no tiny icon buttons)', () => {
    const fixture = initWithRows();
    const btns = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('.tbl .act-btn')) as HTMLElement[];
    expect(btns.some(b => b.textContent!.trim() === 'Bearbeiten')).toBeTrue();
    expect(btns.some(b => b.textContent!.trim() === 'Passwort zurücksetzen')).toBeTrue();
    expect(btns[0].getAttribute('aria-label')).toContain('Benutzer bearbeiten:');
  });

  it('blocks create and sends nothing when required fields are empty', () => {
    const c = init();
    c.openCreate();
    c.save();
    expect(c.form.invalid).toBeTrue();
    http.expectNone(`${API_BASE}/users`);
  });

  it('posts a new user with password and role', () => {
    const c = init();
    c.openCreate();
    c.form.patchValue({ fullName: 'Max', email: 'max@x.de', password: 'Passw0rd!', role: 'Dispatcher' });
    c.save();

    const req = http.expectOne(`${API_BASE}/users`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ fullName: 'Max', email: 'max@x.de', password: 'Passw0rd!', role: 'Dispatcher' });
    req.flush({ id: 'u9' });
    http.expectOne(r => r.url === `${API_BASE}/users`).flush(emptyPage); // reload after success
  });

  it('edits a user without sending email or password', () => {
    const c = init();
    c.openEdit({ id: 'u1', fullName: 'Anna', email: 'anna@x.de', role: 'Admin', companyId: 'c1', isActive: true } as any);
    c.form.patchValue({ fullName: 'Anna Neu', role: 'Dispatcher', isActive: false });
    c.save();

    const req = http.expectOne(`${API_BASE}/users/u1`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({ fullName: 'Anna Neu', role: 'Dispatcher', isActive: false });
    expect(req.request.body.password).toBeUndefined();
    req.flush({ id: 'u1' });
    http.expectOne(r => r.url === `${API_BASE}/users`).flush(emptyPage);
  });

  it('resets a password via the dedicated endpoint', () => {
    const c = init();
    c.openReset({ id: 'u1', email: 'anna@x.de' } as any);
    c.newPassword.set('NewPass99');
    c.doReset();
    const req = http.expectOne(`${API_BASE}/users/u1/reset-password`);
    expect(req.request.body).toEqual({ newPassword: 'NewPass99' });
    req.flush(null);
  });
});
