import { Component, OnInit, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { ApiService } from '../../core/api/api.service';
import { RouteDto } from '../../core/models';
import { routeStatusLabel, wasteColorVar } from '../../core/labels';
import { AuthService } from '../../core/auth/auth.service';
import { BadgeComponent, PageHeaderComponent, SkeletonComponent, StateComponent } from '../../design-system/ui';
import { SummaryItem, SummaryRowComponent } from '../../design-system/admin';

@Component({
  selector: 'ap-routes-list',
  standalone: true,
  imports: [CommonModule, RouterLink, BadgeComponent, PageHeaderComponent, StateComponent, SkeletonComponent, SummaryRowComponent],
  template: `
  <div class="ap-page-enter">
  <ap-page-header title="Routen" subtitle="Touren planen und überwachen">
    <a *ngIf="canEdit" routerLink="/routen/neu" class="ap-btn ap-btn--primary">+ Neue Route</a>
  </ap-page-header>

  <ap-summary-row *ngIf="!loading()" [items]="summary()"></ap-summary-row>

  <div class="filters" role="group" aria-label="Nach Status filtern">
    <button *ngFor="let f of filters" class="ap-chip" [class.on]="active===f.value"
            [attr.aria-pressed]="active===f.value" (click)="setFilter(f.value)">{{ f.label }}</button>
  </div>

  <ap-skeleton *ngIf="loading()" [rows]="6"></ap-skeleton>
  <ap-state *ngIf="!loading() && !rows().length" class="ap-content-enter" title="Keine Routen gefunden"
            message="Passen Sie den Filter an oder legen Sie über „Neue Route“ eine Tour an." icon="🗺"></ap-state>

  <!-- Card mode on small screens: tour cards instead of a table -->
  <div class="cards ap-content-enter" *ngIf="!loading() && rows().length">
    <a *ngFor="let r of rows()" class="tour-card ap-card" [routerLink]="['/routen', r.id]"
       [style.--rail]="fraction(r.wasteTypeName)">
      <div class="tc-top">
        <span class="rn ap-id">{{ r.routeNumber }}</span>
        <ap-badge [status]="r.status" [label]="rl[r.status]"></ap-badge>
      </div>
      <strong class="tc-name">{{ r.name }}</strong>
      <small class="ap-subtle">{{ r.date | date:'dd.MM.yyyy' }} · {{ r.driverName || '—' }} · {{ r.vehicleName || '—' }}</small>
      <span class="ap-meter tc-bar" aria-hidden="true"><i [style.transform]="fill(r)"></i></span>
      <div class="tc-foot">
        <span class="fraction"><i [style.background]="fraction(r.wasteTypeName)"></i>{{ r.wasteTypeName }}</span>
        <span class="ap-id ap-subtle">{{ r.doneCount }}/{{ r.stopCount }}</span>
        <span *ngIf="r.problemCount" class="warn">⚠ {{ r.problemCount }}</span>
      </div>
    </a>
  </div>

  <div class="ap-card ap-table-shell tbl-wrap ap-content-enter" *ngIf="!loading() && rows().length">
    <table class="ap-tbl tbl">
      <thead><tr><th>Tour</th><th>Datum</th><th>Abfallart</th><th>Fahrzeug</th><th>Fahrer</th><th>Fortschritt</th><th>Status</th></tr></thead>
      <tbody>
        <tr *ngFor="let r of rows()" [routerLink]="['/routen', r.id]" class="row"
            tabindex="0" role="link" [attr.aria-label]="'Tour öffnen: ' + r.routeNumber + ' ' + r.name"
            (keydown.enter)="open(r.id)" (keydown.space)="$event.preventDefault(); open(r.id)"
            [style.--rail]="fraction(r.wasteTypeName)">
          <td class="rail-cell"><strong class="ap-id">{{ r.routeNumber }}</strong><div class="ap-subtle">{{ r.name }}</div></td>
          <td class="ap-id">{{ r.date | date:'dd.MM.yyyy' }}</td>
          <td><span class="fraction"><i [style.background]="fraction(r.wasteTypeName)"></i>{{ r.wasteTypeName }}</span></td>
          <td>{{ r.vehicleName || '—' }}</td>
          <td>{{ r.driverName || '—' }}</td>
          <td><div class="prog"><span class="ap-meter bar" aria-hidden="true"><i [style.transform]="fill(r)"></i></span>
            <span class="ap-subtle ap-id">{{ r.doneCount }}/{{ r.stopCount }}</span>
            <span *ngIf="r.problemCount" class="warn">⚠ {{ r.problemCount }}</span></div></td>
          <td><ap-badge [status]="r.status" [label]="rl[r.status]"></ap-badge></td>
        </tr>
      </tbody>
    </table>
  </div>
  </div>`,
  styles: [`
    .filters { display:flex; gap:8px; margin-bottom:18px; flex-wrap:wrap; }
    .row { cursor:pointer; }
    .rail-cell { position:relative; padding-left:16px !important; }
    .rail-cell::before { content:''; position:absolute; left:2px; top:12px; bottom:12px; width:3px;
      border-radius:99px; background:var(--rail, var(--c-border)); }
    .fraction { display:inline-flex; align-items:center; gap:7px; }
    .fraction i { width:9px; height:9px; border-radius:3px; display:inline-block; }
    .prog { display:flex; align-items:center; gap:8px; }
    .bar { width:90px; }
    .warn { color:var(--c-problem); font-weight:700; font-size:12px; }
    /* ≥820px: table · <820px: tour cards */
    .cards { display:none; }
    @media (max-width:820px) {
      .tbl-wrap { display:none; }
      .cards { display:grid; grid-template-columns:1fr; gap:12px; }
    }
    .tour-card { display:flex; flex-direction:column; gap:6px; padding:16px 18px 16px 22px; position:relative; overflow:hidden; }
    .tour-card::before { content:''; position:absolute; inset:0 auto 0 0; width:4px; background:var(--rail, var(--c-border)); }
    .tc-top { display:flex; justify-content:space-between; align-items:center; }
    .rn { font-size:12.5px; color:var(--c-primary-strong); background:var(--c-primary-soft); padding:2px 8px; border-radius:6px; }
    .tc-name { font-size:15px; }
    .tc-bar { margin-top:4px; }
    .tc-foot { display:flex; justify-content:space-between; align-items:center; gap:10px; font-size:12px; flex-wrap:wrap; }
  `],
})
export class RoutesListComponent implements OnInit {
  rows = signal<RouteDto[]>([]);
  summary = computed<SummaryItem[]>(() => {
    const r = this.rows();
    const problems = r.reduce((n, x) => n + (x.problemCount ?? 0), 0);
    return [
      { value: r.length, label: 'Touren' },
      { value: r.filter(x => x.status === 'InProgress').length, label: 'In Arbeit', tone: 'progress' },
      { value: r.filter(x => x.status === 'Completed').length, label: 'Abgeschlossen', tone: 'done' },
      { value: problems, label: 'Probleme', tone: problems ? 'problem' : undefined },
    ];
  });
  loading = signal(true);
  active: string = '';
  rl = routeStatusLabel;
  filters = [
    { label: 'Alle', value: '' }, { label: 'Geplant', value: 'Planned' },
    { label: 'In Arbeit', value: 'InProgress' }, { label: 'Abgeschlossen', value: 'Completed' },
  ];
  get canEdit() { return this.auth.hasRole('Admin', 'Dispatcher'); }

  constructor(private api: ApiService, private auth: AuthService, private router: Router) {}
  ngOnInit() { this.load(); }
  setFilter(v: string) { this.active = v; this.load(); }
  open(id: string) { this.router.navigate(['/routen', id]); }
  load() {
    this.loading.set(true);
    this.api.routes({ status: this.active, pageSize: 100 }).subscribe({
      next: p => { this.rows.set(p.items); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }
  /** GPU-friendly meter fill (scaleX instead of width — see .ap-meter). */
  fill(r: RouteDto) { return `scaleX(${r.stopCount ? r.doneCount / r.stopCount : 0})`; }
  fraction(name?: string | null) { return wasteColorVar(name); }
}
