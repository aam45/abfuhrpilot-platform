import { Component, OnInit, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ApiService } from '../../core/api/api.service';
import { AuthService } from '../../core/auth/auth.service';
import { User, UserRole } from '../../core/models';
import { userRoleLabel } from '../../core/labels';
import { ModalComponent } from '../../design-system/modal';
import { BadgeComponent, PageHeaderComponent, SkeletonComponent, StateComponent } from '../../design-system/ui';
import { ToastService } from '../../design-system/toast.service';
import { AvatarComponent, SummaryItem, SummaryRowComponent } from '../../design-system/admin';

const roleTone: Record<UserRole, string> = { Admin: 'info', Dispatcher: 'progress', Driver: 'done', Viewer: 'open' };

/**
 * Admin-only user management. Create/edit users, change roles, activate/
 * deactivate and reset passwords. The backend guards the "last active admin"
 * invariant and company scoping; this screen surfaces those errors cleanly.
 */
@Component({
  selector: 'ap-users',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, ModalComponent, BadgeComponent, PageHeaderComponent,
    StateComponent, SkeletonComponent, SummaryRowComponent, AvatarComponent],
  template: `
  <div class="ap-page-enter">
  <ap-page-header title="Benutzerverwaltung" subtitle="Zugänge, Rollen und Sicherheit für Ihre Organisation">
    <button class="ap-btn ap-btn--primary" (click)="openCreate()">+ Benutzer einladen</button>
  </ap-page-header>

  <ap-summary-row *ngIf="!loading()" [items]="summary()"></ap-summary-row>

  <div class="ap-toolbar">
    <input class="ap-input search" [value]="search()" (input)="onSearch($any($event.target).value)"
           placeholder="Name oder E-Mail suchen…" aria-label="Benutzer durchsuchen" />
    <div class="chips" role="group" aria-label="Nach Rolle filtern">
      <button *ngFor="let f of roleFilters" class="ap-chip" [class.on]="roleFilter() === f.value"
              [attr.aria-pressed]="roleFilter() === f.value" (click)="roleFilter.set(f.value)">{{ f.label }}</button>
    </div>
    <div class="chips" role="group" aria-label="Nach Status filtern">
      <button *ngFor="let f of statusFilters" class="ap-chip" [class.on]="statusFilter() === f.value"
              [attr.aria-pressed]="statusFilter() === f.value" (click)="statusFilter.set(f.value)">{{ f.label }}</button>
    </div>
    <span class="count ap-subtle ap-id" *ngIf="!loading()">{{ filtered().length }} / {{ total() }}</span>
  </div>

  <ap-skeleton *ngIf="loading()" [rows]="4"></ap-skeleton>
  <ap-state *ngIf="!loading() && !filtered().length" class="ap-content-enter" title="Keine Benutzer gefunden"
            message="Passen Sie Suche oder Filter an — oder laden Sie über „Benutzer einladen“ neue Zugänge ein." icon="👤"></ap-state>

  <div class="ap-card ap-table-shell tbl-wrap ap-content-enter" *ngIf="!loading() && filtered().length">
    <table class="ap-tbl tbl">
      <thead><tr><th>Benutzer</th><th>E-Mail</th><th>Rolle</th><th>Status</th><th class="act-h">Aktionen</th></tr></thead>
      <tbody>
        <tr *ngFor="let u of filtered()">
          <td>
            <div class="who"><ap-avatar [name]="u.fullName"></ap-avatar>
              <span class="strong">{{ u.fullName }} <span class="you" *ngIf="isSelf(u)">Sie</span></span></div>
          </td>
          <td class="mail">{{ u.email }}</td>
          <td><ap-badge [toneName]="tone(u.role)" [label]="rl[u.role]"></ap-badge></td>
          <td><ap-badge [toneName]="u.isActive ? 'done' : 'skip'" [label]="u.isActive ? 'Aktiv' : 'Inaktiv'"></ap-badge></td>
          <td class="act">
            <button class="act-btn" [attr.aria-label]="'Benutzer bearbeiten: ' + u.fullName"
                    (click)="openEdit(u)">Bearbeiten</button>
            <button class="act-btn" [attr.aria-label]="'Passwort zurücksetzen: ' + u.fullName"
                    (click)="openReset(u)">Passwort zurücksetzen</button>
          </td>
        </tr>
      </tbody>
    </table>
  </div>

  <!-- Card mode <820px -->
  <div class="cards ap-content-enter" *ngIf="!loading() && filtered().length">
    <div class="ucard ap-card" *ngFor="let u of filtered()">
      <div class="ucard-top">
        <ap-avatar [name]="u.fullName"></ap-avatar>
        <div class="ucard-who">
          <strong>{{ u.fullName }} <span class="you" *ngIf="isSelf(u)">Sie</span></strong>
          <small class="ap-subtle">{{ u.email }}</small>
        </div>
      </div>
      <div class="ucard-badges">
        <ap-badge [toneName]="tone(u.role)" [label]="rl[u.role]"></ap-badge>
        <ap-badge [toneName]="u.isActive ? 'done' : 'skip'" [label]="u.isActive ? 'Aktiv' : 'Inaktiv'"></ap-badge>
      </div>
      <div class="ucard-actions">
        <button class="act-btn" (click)="openEdit(u)">Bearbeiten</button>
        <button class="act-btn" (click)="openReset(u)">Passwort zurücksetzen</button>
      </div>
    </div>
  </div>

  <!-- Create / edit modal -->
  <ap-modal *ngIf="formOpen()" [title]="editing() ? 'Benutzer bearbeiten' : 'Neuer Benutzer'"
            subtitle="Benutzerkonto" (close)="formOpen.set(false)">
    <form [formGroup]="form" id="user-form" (ngSubmit)="save()" class="grid">
      <div class="field full">
        <label class="ap-label">Name<em>*</em></label>
        <input class="ap-input" formControlName="fullName" placeholder="Max Mustermann" />
        <small class="err" *ngIf="submitted() && form.get('fullName')?.invalid">Pflichtfeld</small>
      </div>
      <div class="field full" *ngIf="!editing()">
        <label class="ap-label">E-Mail<em>*</em></label>
        <input class="ap-input" type="email" formControlName="email" placeholder="name@firma.de" />
        <small class="err" *ngIf="submitted() && form.get('email')?.invalid">Gültige E-Mail erforderlich</small>
      </div>
      <div class="field full" *ngIf="!editing()">
        <label class="ap-label">Passwort<em>*</em></label>
        <input class="ap-input" type="password" formControlName="password" autocomplete="new-password" />
        <small class="ap-subtle">Mind. 8 Zeichen, mit Buchstabe und Ziffer.</small>
        <small class="err" *ngIf="submitted() && form.get('password')?.invalid">Passwort erfüllt die Richtlinie nicht</small>
      </div>
      <div class="field" [class.half]="editing()">
        <label class="ap-label">Rolle<em>*</em></label>
        <select class="ap-select" formControlName="role">
          <option *ngFor="let r of roles" [value]="r">{{ rl[r] }}</option>
        </select>
      </div>
      <label class="check" *ngIf="editing()">
        <input type="checkbox" formControlName="isActive" /><span><strong>Aktiv</strong></span>
      </label>
      <div class="apierr full" *ngIf="error()">{{ error() }}</div>
    </form>
    <ng-container footer>
      <button class="ap-btn ap-btn--ghost" type="button" (click)="formOpen.set(false)">Abbrechen</button>
      <button class="ap-btn ap-btn--primary" type="submit" form="user-form" [disabled]="busy()">
        {{ busy() ? 'Speichern…' : (editing() ? 'Speichern' : 'Anlegen') }}
      </button>
    </ng-container>
  </ap-modal>

  <!-- Reset password modal -->
  <ap-modal *ngIf="resetFor()" title="Passwort zurücksetzen" [subtitle]="resetFor()!.email" (close)="resetFor.set(null)">
    <div class="field full">
      <label class="ap-label">Neues Passwort<em>*</em></label>
      <input class="ap-input" type="password" [value]="newPassword()" (input)="newPassword.set($any($event.target).value)" autocomplete="new-password" />
      <small class="ap-subtle">Mind. 8 Zeichen, mit Buchstabe und Ziffer.</small>
    </div>
    <div class="apierr" *ngIf="error()">{{ error() }}</div>
    <ng-container footer>
      <button class="ap-btn ap-btn--ghost" type="button" (click)="resetFor.set(null)">Abbrechen</button>
      <button class="ap-btn ap-btn--primary" (click)="doReset()" [disabled]="busy()">Passwort setzen</button>
    </ng-container>
  </ap-modal>
  </div>`,
  styles: [`
    .search { max-width:300px; } .count { margin-left:auto; font-size:13px; }
    .chips { display:flex; gap:6px; flex-wrap:wrap; }
    .who { display:flex; align-items:center; gap:12px; }
    .mail { color:var(--c-text-muted); }
    .strong { font-weight:650; }
    .you { font-size:11px; font-weight:700; color:var(--c-primary-strong); background:var(--c-primary-soft); padding:1px 7px; border-radius:999px; margin-left:6px; }
    .act-h, .act { text-align:right; white-space:nowrap; }
    .act-btn { border:1px solid var(--c-border-strong); background:var(--c-surface); min-height:36px;
      border-radius:9px; cursor:pointer; margin-left:8px; font-size:13px; font-weight:600; padding:7px 14px;
      color:var(--c-text); transition:background var(--t-fast), border-color var(--t-fast), transform var(--t-fast); }
    .act-btn:active:not(:disabled) { transform:scale(.96); }
    @media (hover:hover) and (pointer:fine) {
      .act-btn:hover { background:var(--c-surface-2); border-color:var(--c-primary); }
    }
    /* ≥820px table · <820px cards */
    .cards { display:none; }
    @media (max-width:820px) {
      .tbl-wrap { display:none; }
      .cards { display:grid; grid-template-columns:1fr; gap:12px; }
    }
    .ucard { padding:16px 18px; display:flex; flex-direction:column; gap:12px; }
    .ucard-top { display:flex; gap:12px; align-items:center; }
    .ucard-who { min-width:0; } .ucard-who strong { display:block; }
    .ucard-badges { display:flex; gap:8px; }
    .ucard-actions { display:flex; gap:8px; flex-wrap:wrap; }
    .ucard-actions .act-btn { margin-left:0; min-height:44px; flex:1; }
    .grid { display:grid; grid-template-columns:1fr 1fr; gap:16px; }
    .field { display:flex; flex-direction:column; grid-column:1 / -1; } .field.half { grid-column:auto; } .full { grid-column:1 / -1; }
    .ap-label em { color:var(--c-problem); margin-left:2px; font-style:normal; }
    .check { display:flex; gap:10px; align-items:center; padding:12px 14px; border:1px solid var(--c-border); border-radius:var(--r-md); background:var(--c-surface-2); cursor:pointer; }
    .check input { width:16px; height:16px; }
    .err { color:var(--c-problem); font-size:12px; margin-top:4px; font-weight:600; }
    small { margin-top:5px; }
    .apierr { background:var(--c-problem-soft); color:var(--c-problem); padding:10px 12px; border-radius:10px; font-size:13px; font-weight:600; }
    @media (max-width:560px){ .grid { grid-template-columns:1fr; } .field.half { grid-column:1 / -1; } }
  `],
})
export class UsersComponent implements OnInit {
  rows = signal<User[]>([]);
  total = signal(0);
  loading = signal(true);
  busy = signal(false);
  search = signal('');
  error = signal('');
  submitted = signal(false);
  formOpen = signal(false);
  editing = signal<User | null>(null);
  resetFor = signal<User | null>(null);
  newPassword = signal('');
  roles: UserRole[] = ['Admin', 'Dispatcher', 'Driver', 'Viewer'];
  rl = userRoleLabel;
  form!: FormGroup;
  private timer: any;

  // ---- console filters + summary (client-side over the loaded page) ----
  roleFilter = signal<UserRole | 'all'>('all');
  statusFilter = signal<'all' | 'active' | 'inactive'>('all');
  roleFilters: { value: UserRole | 'all'; label: string }[] = [
    { value: 'all', label: 'Alle Rollen' },
    { value: 'Admin', label: 'Administratoren' },
    { value: 'Dispatcher', label: 'Disponenten' },
    { value: 'Driver', label: 'Fahrer' },
    { value: 'Viewer', label: 'Betrachter' },
  ];
  statusFilters: { value: 'all' | 'active' | 'inactive'; label: string }[] = [
    { value: 'all', label: 'Alle' }, { value: 'active', label: 'Aktiv' }, { value: 'inactive', label: 'Inaktiv' },
  ];
  filtered = computed(() => this.rows().filter(u =>
    (this.roleFilter() === 'all' || u.role === this.roleFilter()) &&
    (this.statusFilter() === 'all' || (this.statusFilter() === 'active') === u.isActive)));
  summary = computed<SummaryItem[]>(() => {
    const all = this.rows();
    return [
      { value: this.total(), label: 'Benutzer' },
      { value: all.filter(u => u.isActive).length, label: 'Aktiv', tone: 'done' },
      { value: all.filter(u => u.role === 'Admin').length, label: 'Administratoren', tone: 'info' },
      { value: all.filter(u => !u.isActive).length, label: 'Inaktiv', tone: all.some(u => !u.isActive) ? 'skip' : undefined },
    ];
  });

  constructor(private api: ApiService, private fb: FormBuilder, private auth: AuthService, private toast: ToastService) {}

  ngOnInit() {
    this.form = this.fb.group({
      fullName: ['', Validators.required],
      email: ['', [Validators.required, Validators.email]],
      password: ['', [Validators.required, Validators.minLength(8)]],
      role: ['Viewer' as UserRole, Validators.required],
      isActive: [true],
    });
    this.load();
  }

  tone(role: UserRole) { return roleTone[role]; }
  isSelf(u: User) { return this.auth.user()?.id === u.id; }
  onSearch(v: string) { this.search.set(v); clearTimeout(this.timer); this.timer = setTimeout(() => this.load(), 250); }

  load() {
    this.loading.set(true);
    this.api.users({ search: this.search(), pageSize: 100 }).subscribe({
      next: p => { this.rows.set(p.items); this.total.set(p.totalCount); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  openCreate() {
    this.editing.set(null); this.submitted.set(false); this.error.set('');
    this.form.reset({ fullName: '', email: '', password: '', role: 'Viewer', isActive: true });
    this.form.get('email')!.enable(); this.form.get('password')!.enable();
    this.formOpen.set(true);
  }
  openEdit(u: User) {
    this.editing.set(u); this.submitted.set(false); this.error.set('');
    this.form.reset({ fullName: u.fullName, email: u.email, password: 'unused-x1', role: u.role, isActive: u.isActive });
    this.form.get('email')!.disable(); this.form.get('password')!.disable(); // not editable here
    this.formOpen.set(true);
  }

  save() {
    this.submitted.set(true); this.error.set('');
    const edit = this.editing();
    if (edit) {
      if (this.form.get('fullName')!.invalid || this.form.get('role')!.invalid) return;
      const { fullName, role, isActive } = this.form.getRawValue();
      this.busy.set(true);
      this.api.updateUser(edit.id, { fullName, role, isActive }).subscribe({
        next: () => this.done('Benutzer aktualisiert.'),
        error: e => this.fail(e),
      });
    } else {
      if (this.form.invalid) return;
      const { fullName, email, password, role } = this.form.getRawValue();
      this.busy.set(true);
      this.api.createUser({ fullName, email, password, role }).subscribe({
        next: () => this.done('Benutzer angelegt.'),
        error: e => this.fail(e),
      });
    }
  }

  openReset(u: User) { this.resetFor.set(u); this.newPassword.set(''); this.error.set(''); }
  doReset() {
    const u = this.resetFor(); if (!u) return;
    this.busy.set(true); this.error.set('');
    this.api.resetUserPassword(u.id, this.newPassword()).subscribe({
      next: () => { this.busy.set(false); this.resetFor.set(null); this.toast.show('Passwort zurückgesetzt.'); },
      error: e => this.fail(e),
    });
  }

  private done(msg: string) { this.busy.set(false); this.formOpen.set(false); this.toast.show(msg); this.load(); }
  private fail(e: any) { this.busy.set(false); this.error.set(e?.error?.detail || 'Speichern fehlgeschlagen.'); }
}
