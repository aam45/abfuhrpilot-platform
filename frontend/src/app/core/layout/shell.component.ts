import { Component, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { IconComponent } from '../../design-system/icons';
import { ToastStackComponent } from '../../design-system/ui';

interface NavItem { path: string; label: string; icon: string; roles?: string[]; group: string; }
interface NavGroup { label: string; items: NavItem[]; }

@Component({
  selector: 'ap-shell',
  standalone: true,
  imports: [CommonModule, RouterLink, RouterLinkActive, RouterOutlet, IconComponent, ToastStackComponent],
  template: `
  <div class="shell">
    <!-- Driver navigation gets a compact icon rail so the map owns the screen. -->
    <aside class="sidebar" [class.open]="mobileOpen()" [class.rail]="driverMode()">
      <div class="brand">
        <span class="logo">♻</span>
        <div class="brand-text"><strong>AbfuhrPilot</strong><small>MüllRoute</small></div>
      </div>
      <nav aria-label="Hauptnavigation">
        <div class="nav-group" *ngFor="let g of visibleGroups()" role="group" [attr.aria-label]="g.label">
          <div class="nav-group-label" aria-hidden="true">{{ g.label }}</div>
          <a *ngFor="let n of g.items" [routerLink]="n.path" routerLinkActive="active"
             [attr.aria-label]="n.label" [title]="n.label" (click)="mobileOpen.set(false)">
            <ap-icon class="ic" [name]="n.icon" [size]="17"></ap-icon><span class="lbl">{{ n.label }}</span>
          </a>
        </div>
      </nav>
      <div class="spacer"></div>
      <div class="userbox">
        <div class="avatar" [title]="auth.user()?.fullName || ''">{{ initials() }}</div>
        <div class="who"><strong>{{ auth.user()?.fullName }}</strong><small>{{ roleLabel() }}</small></div>
        <button class="logout" (click)="auth.logout()" title="Abmelden" aria-label="Abmelden">
          <ap-icon name="signout" [size]="17"></ap-icon></button>
      </div>
    </aside>

    <!-- Driver navigation: no admin topbar — a single floating menu button. -->
    <button *ngIf="driverMode()" class="fab-menu" (click)="mobileOpen.set(true)"
            aria-label="Menü öffnen" [attr.aria-expanded]="mobileOpen()">
      <ap-icon name="menu" [size]="20"></ap-icon></button>
    <div *ngIf="driverMode() && mobileOpen()" class="drawer-backdrop" (click)="mobileOpen.set(false)"></div>

    <div class="main">
      <header class="topbar" *ngIf="!driverMode()">
        <button class="burger" (click)="mobileOpen.set(!mobileOpen())"
                aria-label="Navigation öffnen" [attr.aria-expanded]="mobileOpen()">
          <ap-icon name="menu" [size]="20"></ap-icon></button>
        <div class="crumb">{{ context() }}</div>
        <div class="top-actions">
          <span class="pill">{{ today }}</span>
        </div>
      </header>
      <main class="content" [class.flush]="driverMode()"><router-outlet></router-outlet></main>
    </div>
    <ap-toast-stack></ap-toast-stack>
  </div>`,
  styles: [`
    .shell { display:flex; min-height:100vh; }
    /* Tanne shell — depot at first light */
    .sidebar { width:var(--sidebar-w); flex:0 0 var(--sidebar-w); background:var(--c-shell);
      border-right:1px solid var(--c-shell-border); display:flex; flex-direction:column;
      padding:18px 14px; position:sticky; top:0; height:100vh; }
    .brand { display:flex; align-items:center; gap:12px; padding:6px 8px 18px; margin-bottom:14px;
      border-bottom:1px solid var(--c-shell-border); }
    .logo { width:40px; height:40px; border-radius:12px; background:linear-gradient(160deg, var(--c-primary) 0%, var(--c-primary-deep) 100%);
      color:#fff; display:grid; place-items:center; font-size:21px; box-shadow:0 4px 14px var(--c-primary-glow); }
    .brand strong { display:block; font-size:15px; color:#fff; letter-spacing:-.01em; }
    .brand small { color:var(--c-shell-text-dim); font-size:11.5px; text-transform:uppercase; letter-spacing:.08em; }
    nav { display:flex; flex-direction:column; overflow-y:auto; min-height:0; }
    .nav-group { display:flex; flex-direction:column; gap:2px; }
    .nav-group + .nav-group { margin-top:16px; }
    .nav-group-label { padding:0 12px; margin-bottom:5px; font-size:11.5px; font-weight:650;
      color:var(--c-shell-text-dim); opacity:.75; }
    nav a { display:flex; align-items:center; gap:11px; padding:9px 12px; min-height:38px; border-radius:10px;
      color:var(--c-shell-text-dim); font-weight:550; font-size:14px; position:relative;
      transition:background var(--t-fast), color var(--t-fast); }
    nav a:hover, nav a:focus-visible { background:var(--c-shell-2); color:var(--c-shell-text); }
    nav a.active { background:var(--c-shell-2); color:#fff; }
    nav a.active::before { content:''; position:absolute; left:0; top:9px; bottom:9px; width:3px;
      border-radius:99px; background:var(--c-primary); box-shadow:0 0 10px var(--c-primary-glow); }
    nav a .ic { width:20px; justify-content:center; opacity:.9; }
    nav a.active .ic { opacity:1; }
    .spacer { flex:1; min-height:16px; }
    .userbox { display:flex; align-items:center; gap:10px; padding:10px; border-radius:12px;
      background:var(--c-shell-2); border:1px solid var(--c-shell-border); margin-top:14px; }
    .avatar { width:34px; height:34px; border-radius:9px; background:var(--c-primary); color:#fff;
      display:grid; place-items:center; font-weight:700; font-size:13px; }
    .who strong { display:block; font-size:13px; color:var(--c-shell-text); }
    .who small { color:var(--c-shell-text-dim); font-size:12px; }
    .who { flex:1; min-width:0; } .who strong { overflow:hidden; text-overflow:ellipsis; white-space:nowrap; }
    .logout { border:none; background:transparent; cursor:pointer; color:var(--c-shell-text-dim);
      border-radius:8px; padding:6px; display:inline-grid; place-items:center;
      transition:color var(--t-fast), background var(--t-fast); }
    .logout:hover { color:#fff; background:var(--c-shell-border); }
    .main { flex:1; min-width:0; display:flex; flex-direction:column; }
    .topbar { height:var(--topbar-h); display:flex; align-items:center; gap:16px; padding:0 24px;
      border-bottom:1px solid var(--c-border); background:rgba(255,255,255,.85); backdrop-filter:blur(8px);
      position:sticky; top:0; z-index:5; }
    .crumb { font-weight:650; color:var(--c-text-muted); font-size:13px; }
    .top-actions { margin-left:auto; }
    .pill { background:var(--c-surface-2); border:1px solid var(--c-border); padding:6px 12px;
      border-radius:999px; font-size:13px; font-weight:600; font-variant-numeric:tabular-nums; }
    .burger { display:none; border:none; background:transparent; cursor:pointer; color:var(--c-text);
      padding:6px; border-radius:8px; place-items:center; }
    .content { padding:28px 32px 48px; max-width:1440px; width:100%; margin:0 auto; }
    /* Driver navigation: the map owns the full viewport width/height. */
    .content.flush { padding:0; max-width:none; }
    /* Driver navigation: NO persistent sidebar — it becomes an overlay drawer
       behind a floating menu button, so the map owns the whole viewport. */
    .sidebar.rail { position:fixed; z-index:30; left:0; top:0; height:100vh;
      width:var(--sidebar-w); transform:translateX(-100%);
      transition:transform var(--t-med); box-shadow:var(--sh-lg); }
    .sidebar.rail.open { transform:none; }
    .fab-menu { position:fixed; top:12px; left:12px; z-index:26;
      width:44px; height:44px; border-radius:50%; border:none; cursor:pointer;
      background:rgba(14,26,20,.94); color:#fff; display:grid; place-items:center;
      box-shadow:var(--sh-lg); backdrop-filter:blur(6px);
      transition:transform var(--t-fast); }
    .fab-menu:active { transform:scale(.94); }
    .drawer-backdrop { position:fixed; inset:0; z-index:28; background:rgba(16,24,40,.45);
      animation:backdrop-fade var(--t-med) both; }
    @keyframes backdrop-fade { from { opacity:0; } }
    @media (max-width: 900px) {
      .sidebar { position:fixed; z-index:30; left:0; top:0; transform:translateX(-100%); transition:transform var(--t-med); box-shadow:var(--sh-lg); }
      .sidebar.open { transform:none; }
      .burger { display:grid; }
    }
  `],
})
export class ShellComponent {
  mobileOpen = signal(false);
  /** Driver navigation mode: no admin chrome, drawer sidebar. */
  driverMode = signal(false);
  private url = signal('/');
  /** Topbar context follows the workspace, not a generic product label. */
  context = computed(() => {
    const u = this.url();
    if (u.startsWith('/benutzer')) return 'Benutzer & Rollen';
    if (u.startsWith('/dashboard')) return 'Leitstand';
    if (u.startsWith('/routen')) return 'Tourenplanung';
    if (u.startsWith('/auftraege')) return 'Aufträge';
    if (/^\/(container|fahrzeuge|fahrerinnen|abfallarten|ziele)/.test(u)) return 'Stammdaten';
    return 'Disposition & Tourenplanung';
  });
  today = new Date().toLocaleDateString('de-DE', { weekday: 'short', day: '2-digit', month: 'short', year: 'numeric' });

  private readonly nav: NavItem[] = [
    { path: '/dashboard', label: 'Leitstand', icon: 'grid', roles: ['Admin', 'Dispatcher', 'Viewer'], group: 'Betrieb' },
    { path: '/routen', label: 'Routen', icon: 'route', roles: ['Admin', 'Dispatcher', 'Viewer'], group: 'Betrieb' },
    { path: '/auftraege', label: 'Aufträge', icon: 'clipboard', roles: ['Admin', 'Dispatcher', 'Viewer'], group: 'Betrieb' },
    { path: '/fahrer', label: 'Fahrer-Ansicht', icon: 'wheel', group: 'Betrieb' },
    { path: '/container', label: 'Container', icon: 'package', roles: ['Admin', 'Dispatcher', 'Viewer'], group: 'Stammdaten' },
    { path: '/fahrzeuge', label: 'Fahrzeuge', icon: 'truck', roles: ['Admin', 'Dispatcher', 'Viewer'], group: 'Stammdaten' },
    { path: '/fahrerinnen', label: 'Fahrer', icon: 'person', roles: ['Admin', 'Dispatcher', 'Viewer'], group: 'Stammdaten' },
    { path: '/abfallarten', label: 'Abfallarten', icon: 'bin', roles: ['Admin', 'Dispatcher', 'Viewer'], group: 'Stammdaten' },
    { path: '/ziele', label: 'Ziele / Höfe', icon: 'depot', roles: ['Admin', 'Dispatcher', 'Viewer'], group: 'Stammdaten' },
    { path: '/benutzer', label: 'Benutzer', icon: 'users', roles: ['Admin'], group: 'Verwaltung' },
  ];

  constructor(public auth: AuthService, router: Router) {
    const apply = (u: string) => {
      this.url.set(u);
      // Exactly /fahrer (driver navigation) — NOT /fahrerinnen (drivers catalogue).
      this.driverMode.set(/^\/fahrer($|[/?#])/.test(u));
    };
    apply(router.url);
    router.events.pipe(filter(e => e instanceof NavigationEnd))
      .subscribe(e => apply((e as NavigationEnd).urlAfterRedirects));
  }

  visibleNav = computed(() => {
    const role = this.auth.role();
    return this.nav.filter(n => !n.roles || (role && n.roles.includes(role)));
  });

  /** Nav grouped for the sidebar; empty groups (role-filtered) disappear. */
  visibleGroups = computed<NavGroup[]>(() => {
    const groups: NavGroup[] = [];
    for (const n of this.visibleNav()) {
      const g = groups.find(x => x.label === n.group);
      g ? g.items.push(n) : groups.push({ label: n.group, items: [n] });
    }
    return groups;
  });

  roleLabel = computed(() => {
    const map: Record<string, string> = { Admin: 'Administrator', Dispatcher: 'Disponent', Driver: 'Fahrer', Viewer: 'Betrachter' };
    return map[this.auth.role() ?? ''] ?? '';
  });

  initials = computed(() => {
    const n = this.auth.user()?.fullName ?? '';
    return n.split(' ').map(x => x[0]).slice(0, 2).join('').toUpperCase();
  });
}
