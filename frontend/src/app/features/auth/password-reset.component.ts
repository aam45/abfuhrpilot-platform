import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ApiService } from '../../core/api/api.service';

/**
 * Password self-service. Without a `?token=` it requests a reset link; with a
 * token it sets a new password. In dev the API returns the token so the link
 * can be followed without a real email (see `Auth:ExposeTokensForDev`).
 */
@Component({
  selector: 'ap-password-reset',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
  <div class="wrap ap-page-enter">
    <div class="panel ap-card">
      <div class="brand"><span class="logo">♻</span><strong>AbfuhrPilot</strong></div>

      <ng-container *ngIf="!token()">
        <h1>Passwort vergessen</h1>
        <p class="ap-muted sub">Geben Sie Ihre E-Mail an. Wir senden einen Link zum Zurücksetzen.</p>
        <div class="ap-field">
          <label class="ap-label" for="email">E-Mail</label>
          <input id="email" class="ap-input" type="email" [(ngModel)]="email" name="email" />
        </div>
        <button class="ap-btn ap-btn--primary ap-btn--block ap-btn--lg" (click)="requestReset()" [disabled]="busy()">
          {{ busy() ? 'Senden…' : 'Link anfordern' }}
        </button>
        <div class="ok" *ngIf="message()">
          {{ message() }}
          <a *ngIf="devLink()" [href]="devLink()" class="devlink">Dev-Link öffnen</a>
        </div>
      </ng-container>

      <ng-container *ngIf="token()">
        <h1>Neues Passwort</h1>
        <p class="ap-muted sub">Wählen Sie ein neues Passwort (mind. 8 Zeichen, Buchstabe + Ziffer).</p>
        <div class="ap-field">
          <label class="ap-label" for="pw">Neues Passwort</label>
          <input id="pw" class="ap-input" type="password" [(ngModel)]="newPassword" name="pw" autocomplete="new-password" />
        </div>
        <div class="err" *ngIf="error()">{{ error() }}</div>
        <button class="ap-btn ap-btn--primary ap-btn--block ap-btn--lg" (click)="doReset()" [disabled]="busy()">
          {{ busy() ? 'Speichern…' : 'Passwort setzen' }}
        </button>
      </ng-container>

      <a routerLink="/login" class="back">← Zurück zur Anmeldung</a>
    </div>
  </div>`,
  styles: [`
    .wrap { min-height:100vh; display:grid; place-items:center; padding:24px; background:var(--c-bg); }
    .panel { width:min(440px,100%); padding:32px; }
    .brand { display:flex; align-items:center; gap:10px; margin-bottom:20px; }
    .logo { width:40px; height:40px; border-radius:12px; background:linear-gradient(160deg, var(--c-primary) 0%, var(--c-primary-deep) 100%);
      color:#fff; display:grid; place-items:center; font-size:20px; box-shadow:0 4px 14px var(--c-primary-glow); }
    h1 { font-size:23px; } .sub { margin:6px 0 22px; }
    .ok { background:var(--c-done-soft); color:var(--c-done); padding:10px 12px; border-radius:10px; font-size:13px; font-weight:600; margin-top:14px; }
    .devlink { display:block; margin-top:6px; color:var(--c-primary-strong); }
    .err { background:var(--c-problem-soft); color:var(--c-problem); padding:10px 12px; border-radius:10px; font-size:13px; font-weight:600; margin-bottom:12px; }
    .back { display:inline-block; margin-top:18px; color:var(--c-text-muted); font-weight:600; font-size:13px; }
  `],
})
export class PasswordResetComponent implements OnInit {
  email = '';
  newPassword = '';
  token = signal<string | null>(null);
  busy = signal(false);
  message = signal('');
  error = signal('');
  devLink = signal<string | null>(null);

  constructor(private api: ApiService, private ar: ActivatedRoute, private router: Router) {}
  ngOnInit() { this.token.set(this.ar.snapshot.queryParamMap.get('token')); }

  requestReset() {
    this.busy.set(true); this.message.set(''); this.devLink.set(null);
    this.api.requestPasswordReset(this.email.trim()).subscribe({
      next: r => {
        this.busy.set(false); this.message.set(r.message);
        if (r.devToken) this.devLink.set(`/passwort?token=${encodeURIComponent(r.devToken)}`);
      },
      error: () => { this.busy.set(false); this.message.set('Anfrage gesendet.'); },
    });
  }

  doReset() {
    this.busy.set(true); this.error.set('');
    this.api.resetPassword(this.token()!, this.newPassword).subscribe({
      next: () => this.router.navigate(['/login'], { queryParams: { reset: '1' } }),
      error: e => { this.busy.set(false); this.error.set(e?.error?.detail || 'Zurücksetzen fehlgeschlagen.'); },
    });
  }
}
