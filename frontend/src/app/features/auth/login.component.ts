import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { IconComponent } from '../../design-system/icons';

@Component({
  selector: 'ap-login',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, IconComponent],
  template: `
  <div class="wrap ap-page-enter">
    <div class="panel">
      <div class="brand"><span class="logo">♻</span><div><strong>AbfuhrPilot</strong><small>MüllRoute Disposition</small></div></div>
      <h1>Anmelden</h1>
      <p class="ap-muted sub">Willkommen zurück. Bitte melden Sie sich an.</p>

      <form (ngSubmit)="submit()">
        <div class="ap-field">
          <label class="ap-label" for="email">E-Mail</label>
          <input id="email" class="ap-input" type="email" [(ngModel)]="email" name="email" autocomplete="username" required />
        </div>
        <div class="ap-field">
          <label class="ap-label" for="pw">Passwort</label>
          <input id="pw" class="ap-input" type="password" [(ngModel)]="password" name="password" autocomplete="current-password" required />
        </div>
        <div class="err" *ngIf="error()">{{ error() }}</div>
        <button class="ap-btn ap-btn--primary ap-btn--block ap-btn--lg" type="submit" [disabled]="busy()">
          {{ busy() ? 'Anmelden…' : 'Anmelden' }}
        </button>
        <a routerLink="/passwort" class="forgot">Passwort vergessen?</a>
      </form>

      <div class="demo">
        <span class="ap-subtle">Demo-Zugänge (Passwort: <code>Passw0rd!</code>)</span>
        <div class="chips">
          <button type="button" (click)="fill('admin@demo.de')">Admin</button>
          <button type="button" (click)="fill('dispo@demo.de')">Disponent</button>
          <button type="button" (click)="fill('fahrer@demo.de')">Fahrer</button>
          <button type="button" (click)="fill('viewer@demo.de')">Betrachter</button>
        </div>
      </div>
    </div>
    <div class="hero">
      <div class="hero-inner">
        <h2>Tourenplanung, die den Abfall versteht.</h2>
        <p>Routen, Container und Sonderaufträge – nach Abfallart, Fahrzeug und Fahrer.
           Für Disposition und Fahrer in einem System.</p>

        <!-- The German fraction color code IS the brand signature: structure as information -->
        <div class="fractions" aria-label="Abfallarten">
          <span class="fr" *ngFor="let f of fractions"><i [style.background]="f.c"></i>{{ f.n }}</span>
        </div>

        <ul class="features">
          <li><span class="fic"><ap-icon name="truck" [size]="18"></ap-icon></span>
            <div><strong>Fahrzeug-Freigabe je Abfallart</strong>
              <small>Nur zugelassene Fahrzeuge werden eingeplant</small></div></li>
          <li><span class="fic"><ap-icon name="package" [size]="18"></ap-icon></span>
            <div><strong>Container- &amp; Sonderaufträge</strong>
              <small>Vom Auftrag bis zur erledigten Tour</small></div></li>
          <li><span class="fic"><ap-icon name="route" [size]="18"></ap-icon></span>
            <div><strong>Stopp-genaue Fahrer-Navigation</strong>
              <small>Eine klare nächste Aktion, auch offline</small></div></li>
        </ul>
      </div>
    </div>
  </div>`,
  styles: [`
    .wrap { min-height:100vh; display:grid; grid-template-columns: 1fr 1fr; }
    .panel { display:flex; flex-direction:column; justify-content:center; padding:48px 8vw; max-width:560px; margin:0 auto; width:100%; }
    .brand { display:flex; align-items:center; gap:12px; margin-bottom:36px; }
    .logo { width:46px; height:46px; border-radius:13px; background:linear-gradient(160deg, var(--c-primary) 0%, var(--c-primary-deep) 100%);
      color:#fff; display:grid; place-items:center; font-size:24px; box-shadow:0 6px 18px var(--c-primary-glow); }
    .brand strong { display:block; font-size:16px; }
    .brand small { color:var(--c-text-subtle); font-size:11.5px; text-transform:uppercase; letter-spacing:.08em; }
    h1 { font-size:30px; letter-spacing:-.02em; } .sub { margin:6px 0 26px; }
    .err { background:var(--c-problem-soft); color:var(--c-problem); padding:10px 12px; border-radius:10px; font-size:13px; margin-bottom:14px; font-weight:600; }
    .forgot { display:inline-block; margin-top:14px; color:var(--c-primary-strong); font-weight:600; font-size:13px; }
    .demo { margin-top:28px; font-size:13px; padding-top:22px; border-top:1px dashed var(--c-border); }
    .demo code { font-family:var(--font-mono); background:var(--c-surface-2); padding:1px 6px; border-radius:6px; }
    .chips { display:flex; gap:8px; margin-top:12px; flex-wrap:wrap; }
    .chips button { border:1px solid var(--c-border-strong); background:var(--c-surface); padding:9px 16px;
      border-radius:999px; font-weight:600; font-size:13.5px; cursor:pointer; min-height:38px;
      transition:background var(--t-fast), border-color var(--t-fast), transform var(--t-fast); }
    @media (hover:hover) and (pointer:fine) {
      .chips button:hover { background:var(--c-primary-soft); border-color:var(--c-primary); color:var(--c-primary-strong); }
    }
    .chips button:active { transform:scale(.97); }
    /* Tanne brand panel — same identity as the app shell */
    .hero { background:
      radial-gradient(90% 70% at 85% 10%, rgba(34,197,94,.16) 0%, transparent 60%),
      linear-gradient(165deg, var(--c-shell-2) 0%, var(--c-shell) 60%, #081009 100%);
      color:var(--c-shell-text); display:flex; align-items:center; padding:48px; }
    .hero-inner { max-width:460px; }
    .hero h2 { color:#fff; font-size:32px; line-height:1.18; letter-spacing:-.02em; }
    .hero p { margin:18px 0 24px; opacity:.85; font-size:15px; }
    /* Fraction color code — the depot shift-board signature, data-true not decorative */
    .fractions { display:flex; flex-wrap:wrap; gap:8px 16px; padding:14px 0 22px;
      border-top:1px solid var(--c-shell-border); margin-bottom:6px; }
    .fr { display:inline-flex; align-items:center; gap:7px; font-size:12.5px; font-weight:600;
      color:var(--c-shell-text); }
    .fr i { width:10px; height:10px; border-radius:3px; }
    .features { list-style:none; padding:0; margin:0; display:flex; flex-direction:column; }
    .features li { display:flex; align-items:center; gap:14px; padding:13px 0; }
    .features li + li { border-top:1px solid var(--c-shell-border); }
    .fic { width:38px; height:38px; flex:0 0 38px; border-radius:11px; display:grid; place-items:center;
      background:rgba(34,197,94,.12); border:1px solid rgba(34,197,94,.25); color:var(--c-primary); }
    .features strong { display:block; font-size:14px; color:#fff; font-weight:650; }
    .features small { font-size:12.5px; color:var(--c-shell-text-dim); }
    @media (max-width: 900px) { .wrap { grid-template-columns:1fr; } .hero { display:none; } }
  `],
})
export class LoginComponent {
  email = 'admin@demo.de';
  password = 'Passw0rd!';
  busy = signal(false);
  error = signal('');
  /** German waste-fraction color code — the brand signature, rendered data-true. */
  fractions = [
    { n: 'Hausmüll', c: 'var(--w-hausmuell)' },
    { n: 'Kunststoff', c: 'var(--w-kunststoff)' },
    { n: 'Holz', c: 'var(--w-holz)' },
    { n: 'Bio', c: 'var(--w-bio)' },
    { n: 'Sperrmüll', c: 'var(--w-sperr)' },
  ];

  constructor(private auth: AuthService) {}
  fill(email: string) { this.email = email; this.password = 'Passw0rd!'; }

  async submit() {
    this.busy.set(true); this.error.set('');
    try { await this.auth.login(this.email, this.password); }
    catch (e: any) { this.error.set(e?.error?.detail || 'Anmeldung fehlgeschlagen. Bitte Daten prüfen.'); }
    finally { this.busy.set(false); }
  }
}
