import { Component, OnInit, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { ApiService } from '../../core/api/api.service';
import { OperationsDashboard } from '../../core/models';
import { routeStatusLabel, wasteColorVar } from '../../core/labels';
import { BadgeComponent, PageHeaderComponent, SkeletonComponent, StatComponent, StateComponent } from '../../design-system/ui';
import { SummaryItem, SummaryRowComponent } from '../../design-system/admin';
import { LiveOpsMapComponent } from './live-ops-map.component';
import { WasteFractionDonutComponent } from './waste-fraction-donut.component';

@Component({
  selector: 'ap-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink, StatComponent, BadgeComponent, PageHeaderComponent, StateComponent, SkeletonComponent, SummaryRowComponent, LiveOpsMapComponent, WasteFractionDonutComponent],
  template: `
  <div class="ap-page-enter">
  <ap-page-header title="Leitstand" subtitle="Operativer Überblick für heute">
    <span class="ops" *ngIf="data() as d" [class.live]="d.activeRoutes > 0" role="status">
      <i aria-hidden="true"></i>{{ opsLabel(d.activeRoutes) }}
    </span>
  </ap-page-header>

  <!-- Exceptions first: the one thing that needs attention right now -->
  <a *ngIf="!loading() && (data()?.problemStops ?? 0) > 0" class="alert" routerLink="/routen">
    <strong>⚠ {{ data()!.problemStops }} Problemstelle{{ data()!.problemStops > 1 ? 'n' : '' }}</strong>
    <span class="alert-msg">Eingreifen erforderlich — betroffene Touren prüfen</span>
    <span class="alert-go">Zu den Routen →</span>
  </a>

  <ng-container *ngIf="loading()">
    <div class="ap-grid stats">
      <div class="ap-card sk-tile" *ngFor="let i of [1,2,3,4]"></div>
    </div>
    <ap-skeleton [rows]="4"></ap-skeleton>
  </ng-container>

  <ng-container *ngIf="!loading() && data() as d">
    <!-- Command console: ONE instrument strip, not four floating tiles -->
    <div class="ap-card console">
      <ap-stat [flat]="true" label="Heute im Einsatz" [value]="d.routesToday" iconName="route" hint="Geplante Touren"></ap-stat>
      <ap-stat [flat]="true" label="Aktive Touren" [value]="d.activeRoutes" iconName="truck" tone="progress" hint="Gerade unterwegs"></ap-stat>
      <ap-stat [flat]="true" label="Problemstellen" [value]="d.problemStops" iconName="warning" [tone]="d.problemStops > 0 ? 'problem' : ''"
               [hint]="d.problemStops > 0 ? 'Eingreifen erforderlich' : 'Keine Probleme gemeldet'"></ap-stat>
      <ap-stat [flat]="true" label="Offene Aufträge" [value]="d.openOrders" iconName="clipboard" hint="Warten auf Einplanung"></ap-stat>
    </div>
    <!-- Secondary numbers as the compact Betriebsleiste, not a second tile row -->
    <ap-summary-row [items]="subStats()"></ap-summary-row>

    <!-- Live-ops map: spatial overview of today's active tours (stop dots only, no live route lines) -->
    <ap-live-ops-map class="map-block" [routes]="activeRoutes()"></ap-live-ops-map>

    <!-- Live: tours on the road right now -->
    <section *ngIf="activeRoutes().length" class="live">
      <h3 class="live-head">Gerade unterwegs</h3>
      <div class="live-grid">
        <a *ngFor="let r of activeRoutes()" class="tour-card ap-card ap-hover-lift" [routerLink]="['/routen', r.id]"
           [style.--rail]="fraction(r.wasteTypeName)">
          <div class="tc-top">
            <span class="rn ap-id">{{ r.routeNumber }}</span>
            <ap-badge [status]="r.status" [label]="rl[r.status]"></ap-badge>
          </div>
          <strong class="tc-name">{{ r.name }}</strong>
          <small class="ap-subtle">{{ r.driverName || '—' }} · {{ r.vehicleName || '—' }}</small>
          <span class="ap-meter tc-bar" aria-hidden="true"><i [style.transform]="fill(r)"></i></span>
          <div class="tc-foot">
            <span class="ap-id ap-subtle">{{ r.doneCount }}/{{ r.stopCount }} Stopps</span>
            <span *ngIf="r.problemCount" class="warn">⚠ {{ r.problemCount }} Problem{{ r.problemCount > 1 ? 'e' : '' }}</span>
          </div>
        </a>
      </div>
    </section>

    <!-- Fraction mix of today's routes (source list backend-capped at 50/day — see component doc).
         Renders nothing on an empty day; the table below owns the honest empty state. -->
    <ap-waste-fraction-donut class="fd-block" [routes]="d.todaysRoutes"></ap-waste-fraction-donut>

    <section class="ap-card ap-table-shell block">
      <div class="block-head"><h3>Heutige Touren</h3><a routerLink="/routen" class="link">Alle ansehen →</a></div>
      <ap-state *ngIf="!d.todaysRoutes.length" class="ap-content-enter" title="Keine Touren für heute"
                message="Legen Sie unter „Routen“ eine neue Tour an und weisen Sie Fahrzeug und Fahrer zu." icon="🗺"></ap-state>
      <table *ngIf="d.todaysRoutes.length" class="ap-tbl tbl ap-content-enter">
        <thead><tr><th>Tour</th><th>Abfallart</th><th>Fahrzeug</th><th>Fahrer</th><th>Fortschritt</th><th>Status</th></tr></thead>
        <tbody>
          <tr *ngFor="let r of d.todaysRoutes" [routerLink]="['/routen', r.id]" class="row"
              tabindex="0" role="link" [attr.aria-label]="'Tour öffnen: ' + r.routeNumber + ' ' + r.name"
              (keydown.enter)="open(r.id)" (keydown.space)="$event.preventDefault(); open(r.id)"
              [style.--rail]="fraction(r.wasteTypeName)">
            <td class="rail-cell"><strong class="ap-id">{{ r.routeNumber }}</strong><div class="ap-subtle">{{ r.name }}</div></td>
            <td><span class="fraction"><i [style.background]="fraction(r.wasteTypeName)"></i>{{ r.wasteTypeName }}</span></td>
            <td>{{ r.vehicleName || '—' }}</td>
            <td>{{ r.driverName || '—' }}</td>
            <td>
              <div class="prog"><span class="ap-meter bar" aria-hidden="true"><i [style.transform]="fill(r)"></i></span>
                <span class="ap-subtle ap-id">{{ r.doneCount }}/{{ r.stopCount }}</span>
                <span *ngIf="r.problemCount" class="warn">⚠ {{ r.problemCount }}</span></div>
            </td>
            <td><ap-badge [status]="r.status" [label]="rl[r.status]"></ap-badge></td>
          </tr>
        </tbody>
      </table>
    </section>
  </ng-container>
  </div>`,
  styles: [`
    /* Ops status pill in the page header */
    .ops { display:inline-flex; align-items:center; gap:8px; padding:7px 14px; border-radius:999px;
      background:var(--c-surface); border:1px solid var(--c-border); font-size:13px; font-weight:650;
      color:var(--c-text-muted); }
    .ops i { width:8px; height:8px; border-radius:50%; background:var(--c-border-strong); }
    .ops.live { color:var(--c-primary-strong); border-color:color-mix(in srgb, var(--c-primary) 35%, var(--c-border)); }
    .ops.live i { background:var(--c-primary); animation:ap-pulse 2s ease-in-out infinite; }

    /* Exceptions strip — the loudest element on the page, but only when real */
    .alert { display:flex; align-items:center; gap:14px; flex-wrap:wrap; margin-bottom:16px;
      padding:13px 18px; border-radius:12px; background:var(--c-problem-soft);
      border:1px solid color-mix(in srgb, var(--c-problem) 30%, var(--c-border));
      color:var(--c-problem); animation:ap-fade-up var(--t-enter) both;
      transition:box-shadow var(--t-fast); }
    .alert strong { font-size:14px; }
    .alert-msg { font-size:13px; font-weight:600; opacity:.85; }
    .alert-go { margin-left:auto; font-size:13px; font-weight:700; white-space:nowrap; }
    @media (hover:hover) and (pointer:fine) { .alert:hover { box-shadow:var(--sh-md); } }

    .stats { grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); }
    /* Command console: one shared card, cells separated by hairlines. */
    .console { display:grid; grid-template-columns:repeat(4, 1fr); margin-bottom:14px; overflow:clip; }
    .console ap-stat + ap-stat { border-left:1px solid var(--c-border); }
    @media (max-width:980px) {
      .console { grid-template-columns:1fr 1fr; }
      .console ap-stat:nth-child(3) { border-left:none; }
      .console ap-stat:nth-child(n+3) { border-top:1px solid var(--c-border); }
    }
    @media (max-width:520px) {
      .console { grid-template-columns:1fr; }
      .console ap-stat { border-left:none !important; }
      .console ap-stat + ap-stat { border-top:1px solid var(--c-border); }
    }
    .map-block { display:block; margin-bottom:14px; }
    .fd-block { display:block; margin-bottom:14px; }
    .sk-tile { height:104px; background:linear-gradient(90deg, var(--c-surface) 25%, var(--c-surface-2) 50%, var(--c-surface) 75%);
      background-size:200% 100%; animation:ap-shimmer 1.4s linear infinite; margin-bottom:14px; }
    /* Live tour cards */
    .live { margin-bottom:24px; }
    .live-head { font-size:15px; margin-bottom:12px; display:flex; align-items:center; gap:8px; }
    .live-head::before { content:''; width:8px; height:8px; border-radius:50%; background:var(--c-progress);
      animation:ap-pulse 2s ease-in-out infinite; }
    @keyframes ap-pulse { 0%,100% { opacity:1; } 50% { opacity:.35; } }
    .live-grid { display:grid; grid-template-columns:repeat(auto-fill, minmax(260px, 1fr)); gap:14px; }
    .tour-card { display:flex; flex-direction:column; gap:6px; padding:16px 18px 16px 22px; position:relative; overflow:hidden; }
    .tour-card::before { content:''; position:absolute; inset:0 auto 0 0; width:4px; background:var(--rail, var(--c-border)); }
    .tc-top { display:flex; justify-content:space-between; align-items:center; }
    .rn { font-size:12.5px; color:var(--c-primary-strong); background:var(--c-primary-soft); padding:2px 8px; border-radius:6px; }
    .tc-name { font-size:15px; }
    .tc-bar { margin-top:4px; }
    .tc-foot { display:flex; justify-content:space-between; align-items:center; font-size:12px; }
    .block-head { display:flex; align-items:center; justify-content:space-between; padding:16px 22px 12px; }
    /* empty state renders its own card — inset it inside the flush table shell */
    .block ap-state { display:block; padding:0 12px 12px; }
    .link { color:var(--c-primary-strong); font-weight:600; font-size:13px; }
    .row { cursor:pointer; }
    .rail-cell { position:relative; padding-left:16px !important; }
    .rail-cell::before { content:''; position:absolute; left:2px; top:12px; bottom:12px; width:3px;
      border-radius:99px; background:var(--rail, var(--c-border)); }
    .fraction { display:inline-flex; align-items:center; gap:7px; }
    .fraction i { width:9px; height:9px; border-radius:3px; display:inline-block; }
    .prog { display:flex; align-items:center; gap:8px; }
    .bar { width:90px; }
    .warn { color:var(--c-problem); font-weight:700; font-size:12px; }
    @media (max-width:720px) { .tbl th:nth-child(3), .tbl td:nth-child(3), .tbl th:nth-child(4), .tbl td:nth-child(4) { display:none; } }
  `],
})
export class DashboardComponent implements OnInit {
  data = signal<OperationsDashboard | null>(null);
  loading = signal(true);
  rl = routeStatusLabel;

  /** Secondary metrics — Betriebsleiste density instead of a second tile row. */
  subStats = computed<SummaryItem[]>(() => {
    const d = this.data();
    if (!d) return [];
    // Day-completion framing "48/123". The denominator is the backend's today-scoped count of
    // ALL stop statuses (totalStops), so InProgress/Skipped/Cancelled stops can never make a
    // day read more complete than it honestly is.
    const totalStops = d.totalStops;
    return [
      {
        value: `${d.completedStops}/${totalStops}`,
        label: 'Stopps heute',
        // Neutral progress reading; 'done' only once the day is actually finished.
        tone: totalStops > 0 && d.completedStops === totalStops ? 'done' : undefined,
      },
      { value: d.vehiclesInUse, label: 'Fahrzeuge im Einsatz', tone: d.vehiclesInUse ? 'progress' : undefined },
    ];
  });

  opsLabel(active: number) {
    return active > 0 ? `${active} Tour${active > 1 ? 'en' : ''} aktiv` : 'Kein aktiver Einsatz';
  }

  constructor(private api: ApiService, private router: Router) {}
  ngOnInit() {
    this.api.operations().subscribe({
      next: d => { this.data.set(d); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }
  open(id: string) { this.router.navigate(['/routen', id]); }
  /** GPU-friendly meter fill (scaleX instead of width — see .ap-meter). */
  fill(r: { doneCount: number; stopCount: number }) {
    return `scaleX(${r.stopCount ? r.doneCount / r.stopCount : 0})`;
  }
  fraction(name?: string | null) { return wasteColorVar(name); }
  activeRoutes() { return (this.data()?.todaysRoutes ?? []).filter(r => r.status === 'InProgress'); }
}
