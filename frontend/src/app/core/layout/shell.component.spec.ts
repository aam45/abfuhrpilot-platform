import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ShellComponent } from './shell.component';

@Component({ standalone: true, template: '' })
class DummyComponent {}

describe('ShellComponent — driver navigation mode', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [ShellComponent],
      providers: [
        provideHttpClient(), provideHttpClientTesting(),
        provideRouter([
          { path: 'fahrer', component: DummyComponent },
          { path: 'fahrerinnen', component: DummyComponent },
          { path: 'benutzer', component: DummyComponent },
          { path: 'dashboard', component: DummyComponent },
        ]),
      ],
    });
  });

  async function mountAt(url: string) {
    const fixture = TestBed.createComponent(ShellComponent);
    await TestBed.inject(Router).navigateByUrl(url);
    fixture.detectChanges();
    return fixture;
  }

  it('hides ALL admin chrome on /fahrer: no topbar, drawer sidebar, floating menu button', async () => {
    const fixture = await mountAt('/fahrer');
    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelector('.topbar')).toBeNull();          // no admin topbar
    expect(el.textContent).not.toContain('Disposition & Tourenplanung');
    expect(el.querySelector('.sidebar')!.classList).toContain('rail'); // off-screen drawer
    expect(el.querySelector('.fab-menu')).toBeTruthy();      // menu floats over the map
    // Menu button opens the drawer → navigation + logout stay reachable.
    (el.querySelector('.fab-menu') as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(el.querySelector('.sidebar.open')).toBeTruthy();
    expect(el.querySelector('.logout')).toBeTruthy();
  });

  it('keeps the full admin shell elsewhere, with workspace-specific topbar context', async () => {
    const fixture = await mountAt('/dashboard');
    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelector('.crumb')!.textContent).toContain('Leitstand');
    expect(el.querySelector('.sidebar')!.classList).not.toContain('rail');
  });

  it('topbar context follows the workspace (Benutzer & Rollen)', async () => {
    const fixture = await mountAt('/benutzer');
    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelector('.topbar')).toBeTruthy(); // admin pages keep the topbar
    expect(el.querySelector('.crumb')!.textContent).toContain('Benutzer & Rollen');
  });

  it('/fahrerinnen (drivers catalogue) is NOT driver navigation mode', async () => {
    const fixture = await mountAt('/fahrerinnen');
    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelector('.topbar')).toBeTruthy();
    expect(el.querySelector('.sidebar')!.classList).not.toContain('rail');
    expect(el.querySelector('.crumb')!.textContent).toContain('Stammdaten');
  });
});
