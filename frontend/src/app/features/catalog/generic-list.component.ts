import { Component, ElementRef, OnInit, ViewChild, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { Observable } from 'rxjs';
import { ApiService } from '../../core/api/api.service';
import { Paged } from '../../core/models';
import { AuthService } from '../../core/auth/auth.service';
import { listConfigs, ListColumn, ListConfigKey } from './list-configs';
import { formConfigs } from './form-configs';
import { EntityFormComponent } from './entity-form.component';
import { ConfirmDialogComponent } from '../../design-system/modal';
import { wasteColorVar } from '../../core/labels';
import { BadgeComponent, PageHeaderComponent, SkeletonComponent, StateComponent, WasteChipComponent } from '../../design-system/ui';
import { ToastService } from '../../design-system/toast.service';
import { SummaryItem, SummaryRowComponent } from '../../design-system/admin';

type DeleteFn = (api: ApiService, id: string) => Observable<void>;

/** Delete endpoints that exist in the backend (orders are closed via reject, not deleted). */
const deleteFns: Partial<Record<ListConfigKey, DeleteFn>> = {
  wasteTypes: (a, id) => a.deleteWasteType(id),
  vehicles: (a, id) => a.deleteVehicle(id),
  drivers: (a, id) => a.deleteDriver(id),
  destinations: (a, id) => a.deleteDestination(id),
  containers: (a, id) => a.deleteContainer(id),
};

/**
 * One data-table screen reused for every catalogue feature. Read + search come
 * from `list-configs`; create/edit use the config-driven `ap-entity-form` modal.
 * Row actions are role-gated (Admin/Dispatcher edit, Admin delete).
 */
@Component({
  selector: 'ap-generic-list',
  standalone: true,
  imports: [CommonModule, BadgeComponent, WasteChipComponent, PageHeaderComponent, StateComponent,
    SkeletonComponent, EntityFormComponent, SummaryRowComponent, ConfirmDialogComponent],
  template: `
  <div class="ap-page-enter">
  <ap-page-header [title]="cfg.title" [subtitle]="cfg.subtitle">
    <button *ngIf="canCreate" class="ap-btn ap-btn--primary" (click)="openCreate()">+ Neu: {{ singular }}</button>
  </ap-page-header>

  <ap-summary-row *ngIf="!loading()" [items]="summary()"></ap-summary-row>

  <div class="ap-toolbar">
    <input #searchBox class="ap-input search" [value]="search()" (input)="onSearch($any($event.target).value)"
           placeholder="Suchen…" [attr.aria-label]="cfg.title + ' durchsuchen'" />
    <span class="count ap-subtle ap-id" *ngIf="!loading()">{{ total() }} Einträge</span>
  </div>

  <ap-skeleton *ngIf="loading()" [rows]="5"></ap-skeleton>
  <ap-state *ngIf="!loading() && !rows().length" class="ap-content-enter" [title]="emptyTitle()"
            [message]="emptyMessage()" [icon]="cfg.icon"></ap-state>

  <div class="ap-card ap-table-shell tbl-card ap-content-enter" *ngIf="!loading() && rows().length">
    <table class="ap-tbl tbl">
      <thead><tr>
        <th *ngFor="let c of cfg.columns">{{ c.label }}</th>
        <th *ngIf="canEdit" class="act-h">Aktionen</th>
      </tr></thead>
      <tbody>
        <tr *ngFor="let r of rows()">
          <td *ngFor="let c of cfg.columns" [ngSwitch]="c.kind">
            <ap-badge *ngSwitchCase="'status'" [status]="r[c.key]" [label]="c.map ? c.map[r[c.key]] : r[c.key]"></ap-badge>
            <ap-waste-chip *ngSwitchCase="'waste'" [label]="c.map ? c.map[r[c.key]] : r[c.key]"
                           [color]="fraction(r[c.key])"></ap-waste-chip>
            <span *ngSwitchCase="'list'">
              <ap-waste-chip *ngFor="let v of r[c.key]" class="mr" [label]="c.map ? c.map[v] : v"
                             [color]="fraction(v)"></ap-waste-chip>
            </span>
            <span *ngSwitchDefault [class.strong]="c.strong">{{ c.map ? (c.map[r[c.key]] || r[c.key]) : (r[c.key] ?? '—') }}</span>
          </td>
          <td *ngIf="canEdit" class="act">
            <button class="act-btn" [attr.aria-label]="singular + ' bearbeiten: ' + rowLabel(r)"
                    (click)="openEdit(r)">Bearbeiten</button>
            <button *ngIf="canDelete" class="act-btn danger" [attr.aria-label]="singular + ' löschen: ' + rowLabel(r)"
                    (click)="remove(r)" [disabled]="busy()">Löschen</button>
          </td>
        </tr>
      </tbody>
    </table>
  </div>

  <!-- Card mode <820px -->
  <div class="cards" *ngIf="!loading() && rows().length">
    <div class="rcard ap-card" *ngFor="let r of rows()">
      <strong class="rcard-title">{{ rowLabel(r) }}</strong>
      <div class="rcard-fields">
        <ng-container *ngFor="let c of cfg.columns">
          <div class="rcard-field" [ngSwitch]="c.kind">
            <small>{{ c.label }}</small>
            <ap-badge *ngSwitchCase="'status'" [status]="r[c.key]" [label]="c.map ? c.map[r[c.key]] : r[c.key]"></ap-badge>
            <span *ngSwitchCase="'list'">
              <ap-waste-chip *ngFor="let v of r[c.key]" class="mr" [label]="c.map ? c.map[v] : v"
                             [color]="fraction(v)"></ap-waste-chip></span>
            <ap-waste-chip *ngSwitchCase="'waste'" [label]="c.map ? c.map[r[c.key]] : r[c.key]"
                           [color]="fraction(r[c.key])"></ap-waste-chip>
            <span *ngSwitchDefault>{{ c.map ? (c.map[r[c.key]] || r[c.key]) : (r[c.key] ?? '—') }}</span>
          </div>
        </ng-container>
      </div>
      <div class="rcard-actions" *ngIf="canEdit">
        <button class="act-btn" (click)="openEdit(r)">Bearbeiten</button>
        <button *ngIf="canDelete" class="act-btn danger" (click)="remove(r)" [disabled]="busy()">Löschen</button>
      </div>
    </div>
  </div>

  <ap-entity-form *ngIf="formOpen()" [configKey]="key" [mode]="editRow() ? 'edit' : 'create'"
                  [row]="editRow()" (saved)="onSaved()" (cancel)="formOpen.set(false)"></ap-entity-form>

  <ap-confirm *ngIf="pendingDelete() as row" [subtitle]="singular"
              [title]="'„' + rowLabel(row) + '“ löschen?'"
              message="Der Eintrag wird dauerhaft entfernt. Diese Aktion kann nicht rückgängig gemacht werden."
              [busy]="busy()" (confirm)="confirmDelete()"
              (cancel)="busy() || pendingDelete.set(null)"></ap-confirm>
  </div>`,
  styles: [`
    .search { max-width:320px; }
    .count { margin-left:auto; font-size:13px; }
    .strong { font-weight:650; }
    .mr { margin-right:6px; }
    .act-h { text-align:right; } .act { text-align:right; white-space:nowrap; }
    .act-btn { border:1px solid var(--c-border-strong); background:var(--c-surface); min-height:36px;
      border-radius:9px; cursor:pointer; margin-left:8px; font-size:13px; font-weight:600; padding:7px 14px;
      color:var(--c-text); transition:background var(--t-fast), border-color var(--t-fast), transform var(--t-fast); }
    .act-btn:active:not(:disabled) { transform:scale(.96); }
    @media (hover:hover) and (pointer:fine) {
      .act-btn:hover { background:var(--c-surface-2); border-color:var(--c-primary); }
      .act-btn.danger:hover { background:var(--c-problem-soft); border-color:var(--c-problem); color:var(--c-problem); }
    }
    /* ≥820px table · <820px record cards */
    .cards { display:none; }
    @media (max-width:820px) {
      .tbl-card { display:none; }
      .cards { display:grid; grid-template-columns:1fr; gap:12px; }
    }
    .rcard { padding:16px 18px; display:flex; flex-direction:column; gap:12px; }
    .rcard-title { font-size:15px; }
    .rcard-fields { display:grid; grid-template-columns:1fr 1fr; gap:10px 14px; }
    .rcard-field small { display:block; color:var(--c-text-subtle); font-size:12px; font-weight:600; margin-bottom:2px; }
    .rcard-actions { display:flex; gap:8px; }
    .rcard-actions .act-btn { margin-left:0; flex:1; min-height:44px; }
  `],
})
export class GenericListComponent implements OnInit {
  rows = signal<any[]>([]);
  total = signal(0);
  loading = signal(true);
  busy = signal(false);
  search = signal('');
  formOpen = signal(false);
  editRow = signal<any | null>(null);
  pendingDelete = signal<any | null>(null);
  key!: ListConfigKey;
  @ViewChild('searchBox') searchBox?: ElementRef<HTMLInputElement>;
  cfg!: { title: string; subtitle: string; icon: string; columns: ListColumn[]; load: (api: ApiService, q: any) => Observable<Paged<any>> };
  private timer: any;

  /**
   * Data-driven "Betriebsleiste": total + the status distribution taken from
   * the config's own status column/map — always truthful, never guessed enums.
   */
  summary = computed<SummaryItem[]>(() => {
    const rows = this.rows();
    const items: SummaryItem[] = [{ value: this.total(), label: this.cfg?.title ? `${this.cfg.title} gesamt` : 'Gesamt' }];
    const statusCol = this.cfg?.columns.find(c => c.kind === 'status');
    if (statusCol) {
      const counts = new Map<string, number>();
      for (const r of rows) {
        const v = r[statusCol.key];
        if (v != null) counts.set(v, (counts.get(v) ?? 0) + 1);
      }
      [...counts.entries()].sort((a, b) => b[1] - a[1]).slice(0, 3).forEach(([status, n]) =>
        items.push({ value: n, label: statusCol.map?.[status] ?? status }));
    } else if (rows.length && typeof rows[0].isActive === 'boolean') {
      items.push({ value: rows.filter(r => r.isActive).length, label: 'Aktiv', tone: 'done' });
      const inactive = rows.filter(r => !r.isActive).length;
      if (inactive) items.push({ value: inactive, label: 'Inaktiv', tone: 'skip' });
    }
    return items;
  });

  constructor(private api: ApiService, private ar: ActivatedRoute, private auth: AuthService, private toast: ToastService) {}

  get singular() { return formConfigs[this.key]?.singular ?? ''; }
  get canCreate() { return this.auth.hasRole('Admin', 'Dispatcher'); }
  get canEdit() { return this.auth.hasRole('Admin', 'Dispatcher'); }
  get canDelete() { return this.auth.hasRole('Admin') && !!deleteFns[this.key]; }

  ngOnInit() {
    this.key = this.ar.snapshot.data['config'] as ListConfigKey;
    this.cfg = listConfigs[this.key];
    this.load();
  }

  onSearch(v: string) { this.search.set(v); clearTimeout(this.timer); this.timer = setTimeout(() => this.load(), 250); }
  load() {
    this.loading.set(true);
    this.cfg.load(this.api, { search: this.search(), pageSize: 100 }).subscribe({
      next: p => { this.rows.set(p.items); this.total.set(p.totalCount); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  openCreate() { this.editRow.set(null); this.formOpen.set(true); }
  openEdit(row: any) { this.editRow.set(row); this.formOpen.set(true); }
  onSaved() {
    const edited = !!this.editRow();
    this.formOpen.set(false);
    this.toast.show(edited ? `${this.singular} aktualisiert.` : `${this.singular} angelegt.`);
    this.load();
  }

  fraction(v?: string | null) { return wasteColorVar(v); }
  rowLabel(row: any): string {
    return row.name || row.fullName || row.containerNumber || row.orderNumber || 'Eintrag';
  }

  /** Action-oriented German empty states — an empty screen is an invitation to act. */
  emptyTitle(): string {
    const map: Record<ListConfigKey, string> = {
      orders: 'Noch keine Aufträge', containers: 'Noch keine Container', vehicles: 'Noch keine Fahrzeuge',
      drivers: 'Noch keine Fahrer', wasteTypes: 'Noch keine Abfallarten', destinations: 'Noch keine Ziele',
    };
    return this.search() ? 'Keine Treffer' : map[this.key];
  }
  emptyMessage(): string {
    if (this.search()) return 'Passen Sie den Suchbegriff an oder leeren Sie die Suche.';
    if (!this.canCreate) return '';
    const map: Record<ListConfigKey, string> = {
      orders: 'Legen Sie den ersten Auftrag an — z. B. eine Container-Abholung oder einen Sperrmüll-Termin.',
      containers: 'Erfassen Sie Ihren Containerbestand, um Abholungen und Tausch zu planen.',
      vehicles: 'Fügen Sie Ihre Fahrzeuge mit Maßen und freigegebenen Abfallarten hinzu.',
      drivers: 'Legen Sie Ihr Fahrpersonal an, um Touren zuweisen zu können.',
      wasteTypes: 'Definieren Sie die Abfallarten Ihres Betriebs — z. B. Hausmüll, Kunststoff, Holz.',
      destinations: 'Hinterlegen Sie Betriebshöfe und Recyclinghöfe als Start- und Zielpunkte.',
    };
    return map[this.key];
  }

  /** Opens the ap-confirm dialog; the actual delete runs in confirmDelete(). */
  remove(row: any) {
    if (!deleteFns[this.key]) return;
    this.pendingDelete.set(row);
  }

  confirmDelete() {
    const row = this.pendingDelete();
    const fn = deleteFns[this.key];
    if (!row || !fn) return;
    this.busy.set(true);
    fn(this.api, row.id).subscribe({
      next: () => {
        this.busy.set(false); this.pendingDelete.set(null);
        this.toast.show(`${this.singular} gelöscht.`); this.load();
        // The dialog's opener (the row's delete button) is gone — park focus
        // on the toolbar search so keyboard users aren't dropped on <body>.
        setTimeout(() => this.searchBox?.nativeElement.focus());
      },
      error: (e) => {
        this.busy.set(false); this.pendingDelete.set(null);
        this.toast.show(e?.error?.detail || 'Löschen fehlgeschlagen.', 'problem');
      },
    });
  }
}
