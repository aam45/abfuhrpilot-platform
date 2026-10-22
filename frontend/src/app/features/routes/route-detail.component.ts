import { Component, OnInit, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ApiService } from '../../core/api/api.service';
import { GeoPoint, NavigationLinks, RouteCalculationResult, RouteDto, RouteOptimizationResult, RoutingWarning, Stop } from '../../core/models';
import { routingWarningLabel, wasteColorVar } from '../../core/labels';
import { routeStatusLabel, stopStatusLabel } from '../../core/labels';
import { AuthService } from '../../core/auth/auth.service';
import { navigationUrl } from '../../core/map/map-provider';
import { MapUiStateService } from '../../core/map/map-ui-state.service';
import { BadgeComponent, PageHeaderComponent, StateComponent, StopProgressComponent } from '../../design-system/ui';
import { FloatingPanelComponent } from '../../design-system/bottom-sheet';
import { RouteMapComponent, MapPoi } from './route-map.component';

@Component({
  selector: 'ap-route-detail',
  standalone: true,
  imports: [CommonModule, RouterLink, BadgeComponent, PageHeaderComponent, StateComponent, RouteMapComponent, FloatingPanelComponent, StopProgressComponent],
  providers: [MapUiStateService],
  template: `
  <div class="ap-page-enter">
  <a routerLink="/routen" class="back">← Zurück zu Routen</a>
  <ap-state *ngIf="loading()" [loading]="true"></ap-state>

  <ng-container *ngIf="!loading() && route() as r">
    <ap-page-header [title]="r.routeNumber + ' · ' + r.name" [subtitle]="(r.date | date:'EEEE, dd.MM.yyyy') || ''">
      <ap-badge [status]="r.status" [label]="rl[r.status]"></ap-badge>
      <button *ngIf="canEdit && r.status==='Planned'" class="ap-btn ap-btn--primary" (click)="start(r.id)" [disabled]="busy()">▶ Starten</button>
      <button *ngIf="canEdit && r.status==='InProgress'" class="ap-btn ap-btn--primary" (click)="complete(r.id)" [disabled]="busy()">✔ Abschließen</button>
    </ap-page-header>

    <!-- Routing & navigation -->
    <div class="ap-card routing">
      <div class="routing-head">
        <div>
          <h3>Routenplanung &amp; Navigation</h3>
          <span class="prov" [attr.data-tone]="provState().tone">{{ provState().label }}</span>
        </div>
        <div class="routing-actions">
          <button class="ap-btn ap-btn--ghost" (click)="calculate()" [disabled]="routingBusy()">📏 Route berechnen</button>
          <button *ngIf="canReorder()" class="ap-btn ap-btn--ghost" (click)="optimize()" [disabled]="routingBusy()">✨ Route optimieren</button>
          <a class="ap-btn ap-btn--ghost" *ngIf="navLinks() as n" [href]="n.googleMaps" target="_blank" rel="noopener">🧭 In Google Maps öffnen</a>
        </div>
      </div>

      <div class="summary" *ngIf="summary() as s">
        <div><small>Strecke</small><strong>{{ s.km }} km</strong></div>
        <div><small>Dauer (ca.)</small><strong>{{ s.min }} min</strong></div>
        <div><small>Anbieter</small><strong>{{ prov()?.provider }}</strong></div>
        <div *ngIf="profileLabel() as p"><small>Routing-Profil</small><strong>{{ p }}</strong></div>
        <div><small>LKW-Beschränkungen</small>
          <strong [class.truck-on]="truckApplied()" [class.truck-off]="!truckApplied() && !!result()">
            {{ result() ? (truckApplied() ? 'aktiv' : 'nicht aktiv') : '—' }}</strong></div>
      </div>

      <!-- Honest truck warning: never pretend a Müllwagen route is truck-safe. -->
      <div class="truck-warn" *ngIf="truckNotSafe()">
        ⚠ Achtung: Diese Route wurde nicht mit LKW-Beschränkungen berechnet.
      </div>

      <ul class="warnlist" *ngIf="warningList().length">
        <li *ngFor="let w of warningList()" [attr.data-sev]="w.severity">
          {{ warningText(w) }}
        </li>
      </ul>

      <div class="warn" *ngIf="excludedCount() > 0">
        ⚠ {{ excludedCount() }} Stopp(s) ohne Koordinaten wurden vom Routing ausgeschlossen.
      </div>

      <!-- Optimized order preview (before applying) -->
      <div class="preview" *ngIf="opt() as o">
        <div class="preview-head">
          <strong>Vorschau optimierte Reihenfolge</strong>
          <span class="ap-subtle" *ngIf="o.warning">{{ o.warning }}</span>
        </div>
        <ol>
          <li *ngFor="let s of previewStops()">{{ s.address }}</li>
        </ol>
        <div class="preview-actions">
          <button class="ap-btn ap-btn--ghost" (click)="cancelPreview()">Verwerfen</button>
          <button class="ap-btn ap-btn--primary" (click)="applyOptimized()" [disabled]="routingBusy()">Optimierte Reihenfolge übernehmen</button>
        </div>
      </div>
    </div>

    <!-- Dispatch cockpit: map dominant, floating summary, stops panel synced -->
    <div class="cockpit">
      <div class="map-pane">
        <ap-route-map class="mapfill" [bare]="true" [stops]="stops()" [wasteTypeNames]="wasteTypeNames()"
                      [depot]="depot()" [end]="end()" [geometry]="geometry()"
                      [selectedStopId]="ui.selectedStopId()"
                      (stopSelected)="onMapSelect($event)"></ap-route-map>
        <ap-floating-panel spot="top-left">
          <div class="summary" [style.--rail]="fraction(r.wasteTypeName)">
            <div class="sum-top">
              <span class="rn ap-id">{{ r.routeNumber }}</span>
              <span class="fraction"><i [style.background]="fraction(r.wasteTypeName)"></i>{{ r.wasteTypeName }}</span>
            </div>
            <div class="sum-row"><span>Fahrzeug</span><strong>{{ r.vehicleName || '—' }}</strong></div>
            <div class="sum-row"><span>Fahrer</span><strong>{{ r.driverName || '—' }}</strong></div>
            <div class="sum-row"><span>Geplant</span><strong>{{ (r.plannedStartTime | date:'HH:mm') || '—' }}–{{ (r.plannedEndTime | date:'HH:mm') || '—' }}</strong></div>
            <!-- Per-stop segments: the dispatcher sees WHERE problems sit, not just how many -->
            <ap-stop-progress class="sum-segs" *ngIf="stops().length" [stops]="stops()"></ap-stop-progress>
            <small class="ap-subtle ap-id">{{ r.doneCount }} / {{ r.stopCount }} Stopps</small>
          </div>
        </ap-floating-panel>
      </div>

      <aside class="ap-card stops">
        <div class="stops-head">
          <h3>Stopps ({{ stops().length }})</h3>
          <span class="saving ap-subtle" *ngIf="reordering()">Reihenfolge wird gespeichert…</span>
        </div>
        <p class="reorder-hint ap-subtle" *ngIf="canReorder() && stops().length > 1">
          ↕ Ziehen zum Sortieren · Klicken zum Anzeigen auf der Karte.
        </p>
        <ap-state *ngIf="!stops().length" title="Noch keine Stopps" icon="📍"></ap-state>
        <ul *ngIf="stops().length" [class.dragging]="dragIndex() !== null">
          <li *ngFor="let s of stops(); let i = index" [attr.data-tone]="tone(s.status)"
              [style.--rail]="fraction(wasteTypeNames()[s.wasteTypeId])"
              [class.drag-over]="overIndex() === i" [class.dragged]="dragIndex() === i"
              [class.selected]="ui.selectedStopId() === s.id"
              [attr.aria-selected]="ui.selectedStopId() === s.id"
              [draggable]="canReorder()" (click)="selectStop(s.id)"
              (dragstart)="onDragStart(i, $event)" (dragover)="onDragOver(i, $event)"
              (drop)="onDrop(i)" (dragend)="onDragEnd()">
            <span class="handle" *ngIf="canReorder()" title="Ziehen zum Sortieren">⠿</span>
            <span class="seq">{{ s.sequenceNumber }}</span>
            <div class="sbody">
              <div class="stop-top"><strong>{{ s.address }}</strong>
                <ap-badge [status]="s.status" [label]="sl[s.status]"></ap-badge></div>
              <small class="ap-subtle">{{ s.customerName || '' }}
                <span *ngIf="s.completedAt">· erledigt {{ s.completedAt | date:'HH:mm' }}</span></small>
            </div>
            <a class="nav" [href]="navUrl(s)" target="_blank" rel="noopener" title="Navigieren"
               aria-label="Zu diesem Stopp navigieren" (click)="$event.stopPropagation()">↗</a>
          </li>
        </ul>
        <div class="reorder-err" *ngIf="reorderError()">{{ reorderError() }}</div>
      </aside>
    </div>
  </ng-container>
  </div>`,
  styles: [`
    .back { display:inline-block; margin-bottom:14px; color:var(--c-text-muted); font-weight:600; font-size:13px; }
    /* Cockpit: map dominant, stops panel beside it */
    .cockpit { display:grid; grid-template-columns: 1fr 380px; gap:18px; align-items:stretch; margin-top:18px; }
    .map-pane { position:relative; min-height:560px; border-radius:var(--r-lg); overflow:hidden;
      border:1px solid var(--c-border); box-shadow:var(--sh-sm); background:var(--c-surface-2); }
    .mapfill { position:absolute; inset:0; display:block; }
    .summary { padding:12px 14px 12px 18px; position:relative; min-width:220px; }
    .summary::before { content:''; position:absolute; left:0; top:10px; bottom:10px; width:4px; border-radius:99px;
      background:var(--rail, var(--w-hausmuell)); }
    .sum-top { display:flex; align-items:center; gap:10px; margin-bottom:8px; }
    .rn { font-size:12.5px; color:var(--c-primary-strong); background:var(--c-primary-soft); padding:2px 8px; border-radius:6px; }
    .fraction { display:inline-flex; align-items:center; gap:7px; font-weight:650; font-size:13px; }
    .fraction i { width:9px; height:9px; border-radius:3px; display:inline-block; }
    .sum-row { display:flex; justify-content:space-between; gap:16px; font-size:12.5px; padding:2px 0; }
    .sum-row span { color:var(--c-text-subtle); } .sum-row strong { font-weight:600; }
    .sum-segs { display:block; margin:8px 0 4px; }
    .stops { padding:20px 22px; max-height:640px; overflow-y:auto; } h3 { margin-bottom:14px; }
    .stops li.selected { background:var(--c-primary-soft); box-shadow:inset 0 0 0 1px var(--c-primary); }
    .stops li { cursor:pointer; }
    .stops-head { display:flex; align-items:center; justify-content:space-between; }
    .saving { font-size:12px; } .reorder-hint { margin:0 0 8px; font-size:12px; }
    .stops ul { list-style:none; margin:0; padding:0; }
    .stops li { display:flex; gap:12px; align-items:center; padding:12px 8px 12px 14px; border-bottom:1px solid var(--c-border);
      border-radius:10px; position:relative; transition:background var(--t-fast), box-shadow var(--t-fast); }
    .stops li::before { content:''; position:absolute; left:0; top:10px; bottom:10px; width:3px;
      border-radius:99px; background:var(--rail, var(--c-border)); }
    li[draggable=true] { cursor:grab; }
    li.drag-over { background:var(--c-primary-soft); box-shadow:inset 0 0 0 1px var(--c-primary); }
    li.dragged { opacity:.5; }
    .handle { color:var(--c-text-subtle); font-size:16px; cursor:grab; user-select:none; }
    .seq { width:30px; height:30px; flex:0 0 30px; border-radius:9px; background:var(--c-surface-2); border:1px solid var(--c-border);
      display:grid; place-items:center; font-weight:650; font-size:13px; font-family:var(--font-mono); font-variant-numeric:tabular-nums; }
    li[data-tone=done] .seq { background:var(--c-done-soft); color:var(--c-done); }
    li[data-tone=problem] .seq { background:var(--c-problem-soft); color:var(--c-problem); }
    .sbody { flex:1; min-width:0; } .stop-top { display:flex; justify-content:space-between; gap:10px; align-items:center; }
    .nav { flex:0 0 auto; width:30px; height:30px; border-radius:9px; border:1px solid var(--c-border-strong);
      display:grid; place-items:center; color:var(--c-text-muted); font-weight:700; }
    .nav:hover { background:var(--c-surface-2); }
    .reorder-err { color:var(--c-problem); font-size:12px; font-weight:600; margin-top:10px; }
    .routing { padding:18px 20px; margin-bottom:18px; }
    .routing-head { display:flex; align-items:flex-start; justify-content:space-between; gap:14px; flex-wrap:wrap; }
    .routing-head h3 { margin-bottom:6px; }
    .routing-actions { display:flex; gap:8px; flex-wrap:wrap; }
    .prov { display:inline-flex; align-items:center; gap:6px; font-size:12px; font-weight:700; padding:3px 10px; border-radius:999px; }
    .prov::before { content:''; width:6px; height:6px; border-radius:50%; background:currentColor; }
    .prov[data-tone=ok] { color:var(--c-done); background:var(--c-done-soft); }
    .prov[data-tone=fallback] { color:var(--c-info); background:var(--c-info-soft); }
    .prov[data-tone=missing] { color:var(--c-skip); background:var(--c-skip-soft); }
    .prov[data-tone=error] { color:var(--c-problem); background:var(--c-problem-soft); }
    .summary { display:flex; gap:28px; margin-top:14px; flex-wrap:wrap; }
    .summary small { display:block; color:var(--c-text-subtle); font-size:12px; } .summary strong { font-size:17px; }
    .warn { margin-top:12px; background:var(--c-skip-soft); color:var(--c-skip); padding:9px 12px; border-radius:10px; font-size:13px; font-weight:600; }
    .truck-warn { margin-top:12px; background:var(--c-problem-soft); color:var(--c-problem); padding:10px 14px; border-radius:10px; font-size:13px; font-weight:700; }
    .truck-on { color:var(--c-done); } .truck-off { color:var(--c-problem); }
    .warnlist { list-style:none; margin:10px 0 0; padding:0; display:flex; flex-direction:column; gap:5px; }
    .warnlist li { font-size:12.5px; font-weight:600; padding:5px 10px; border-radius:8px; background:var(--c-surface-2); color:var(--c-text-muted); border:1px solid var(--c-border); }
    .warnlist li[data-sev=Warning] { background:var(--c-skip-soft); color:var(--c-skip); border-color:transparent; }
    .preview { margin-top:14px; border-top:1px solid var(--c-border); padding-top:14px; }
    .preview-head { display:flex; justify-content:space-between; gap:12px; margin-bottom:8px; align-items:baseline; }
    .preview ol { margin:0; padding-left:20px; display:flex; flex-direction:column; gap:4px; font-size:14px; }
    .preview-actions { display:flex; justify-content:flex-end; gap:10px; margin-top:14px; }
    @media (max-width:980px){ .cockpit { grid-template-columns:1fr; } .map-pane { min-height:380px; } .stops { max-height:none; } }
  `],
})
export class RouteDetailComponent implements OnInit {
  route = signal<RouteDto | null>(null);
  stops = signal<Stop[]>([]);
  loading = signal(true);
  busy = signal(false);
  reordering = signal(false);
  reorderError = signal('');
  dragIndex = signal<number | null>(null);
  overIndex = signal<number | null>(null);
  wasteTypeNames = signal<Record<string, string>>({});
  depot = signal<MapPoi | null>(null);
  end = signal<MapPoi | null>(null);
  private destinations = signal<Record<string, MapPoi>>({});
  // routing
  calc = signal<RouteCalculationResult | null>(null);
  opt = signal<RouteOptimizationResult | null>(null);
  geometry = signal<GeoPoint[] | null>(null);
  navLinks = signal<NavigationLinks | null>(null);
  routingBusy = signal(false);
  rl = routeStatusLabel; sl = stopStatusLabel;
  private id!: string;
  get canEdit() { return this.auth.hasRole('Admin', 'Dispatcher'); }
  /** Reordering is planning: only Admin/Dispatcher and only before the route starts. */
  canReorder() { const st = this.route()?.status; return this.canEdit && (st === 'Draft' || st === 'Planned'); }

  constructor(private api: ApiService, private ar: ActivatedRoute, private auth: AuthService,
              public ui: MapUiStateService) {}

  /** List click ↔ map marker selection stay in sync via MapUiStateService. */
  selectStop(id: string) { this.ui.selectStop(this.ui.selectedStopId() === id ? null : id); }
  onMapSelect(id: string) { this.ui.selectStop(id); }
  ngOnInit() {
    this.id = this.ar.snapshot.paramMap.get('id')!;
    // Map context (names + depot/end coordinates) — loaded once, reused across reloads.
    this.api.wasteTypes({ pageSize: 200 }).subscribe(p =>
      this.wasteTypeNames.set(Object.fromEntries(p.items.map(w => [w.id, w.name]))));
    this.api.destinations({ pageSize: 200 }).subscribe(p => {
      const map: Record<string, MapPoi> = {};
      for (const d of p.items) if (d.latitude != null && d.longitude != null)
        map[d.id] = { lat: d.latitude, lng: d.longitude, name: d.name };
      this.destinations.set(map);
      this.applyPois();
    });
    this.load();
    this.loadNavLinks();
  }

  load() {
    this.loading.set(true);
    this.api.route(this.id).subscribe({
      next: r => { this.route.set(r); this.loading.set(false); this.applyPois(); },
      error: () => this.loading.set(false),
    });
    this.api.routeStops(this.id).subscribe(s => this.stops.set(s));
  }

  /** Resolves start-depot / end-destination markers once both route + destinations are loaded. */
  private applyPois() {
    const r = this.route(); const dests = this.destinations();
    this.depot.set(r?.startDepotId ? dests[r.startDepotId] ?? null : null);
    this.end.set(r?.endDestinationId ? dests[r.endDestinationId] ?? null : null);
  }
  tone(s: string) { return { Done: 'done', Problem: 'problem', InProgress: 'progress', Skipped: 'skip' }[s] ?? 'open'; }
  fraction(name?: string | null) { return wasteColorVar(name); }
  navUrl(s: Stop) { return navigationUrl(s.latitude, s.longitude, s.address); }
  private run(o: any) { this.busy.set(true); o.subscribe({ next: () => { this.busy.set(false); this.load(); }, error: () => this.busy.set(false) }); }
  start(id: string) { this.run(this.api.startRoute(id)); }
  complete(id: string) { this.run(this.api.completeRoute(id)); }

  // ---- routing & navigation ----
  result = computed(() => this.opt() ?? this.calc());
  prov() { return this.result(); }
  warningList = computed<RoutingWarning[]>(() => this.result()?.warnings ?? []);
  truckApplied = computed(() => this.result()?.truckRestrictionsApplied === true);
  /** True when the result explicitly says truck rules were NOT applied — honest warning. */
  truckNotSafe = computed(() => this.warningList().some(w =>
    w.code === 'TRUCK_RESTRICTIONS_NOT_ENFORCED' || w.code === 'PROVIDER_USING_CAR_FALLBACK'));
  profileLabel = computed(() => {
    const p = this.result()?.routingProfileUsed;
    if (!p) return null;
    if (p === 'car') return 'Auto';
    if (p === 'straight-line') return 'Luftlinie (Schätzung)';
    return 'LKW (' + p + ')';
  });
  warningText(w: RoutingWarning) { return routingWarningLabel[w.code] ?? w.message; }
  provState = computed(() => {
    const r = this.result();
    if (!r) return { label: 'Routing bereit', tone: 'fallback' };
    const w = r.warning ?? '';
    if (w.includes('fehlgeschlagen')) return { label: 'Routing fehlgeschlagen', tone: 'error' };
    if (r.provider === 'GraphHopper' && r.providerConfigured) return { label: 'GraphHopper aktiv', tone: 'ok' };
    if (!r.providerConfigured) return { label: 'Lokaler Fallback aktiv (API-Key fehlt)', tone: 'missing' };
    return { label: 'Lokaler Fallback aktiv', tone: 'fallback' };
  });
  summary = computed(() => {
    const r = this.result();
    if (!r || !r.totalDistanceMeters) return null;
    return { km: (r.totalDistanceMeters / 1000).toFixed(1), min: Math.round(r.totalDurationSeconds / 60) };
  });
  excludedCount = computed(() => this.result()?.excludedStopIds.length ?? 0);
  previewStops = computed(() => {
    const o = this.opt(); if (!o) return [];
    const byId = new Map(this.stops().map(s => [s.id, s]));
    return o.order.map(x => byId.get(x.stopId)).filter((s): s is Stop => !!s);
  });

  calculate() {
    this.routingBusy.set(true); this.opt.set(null);
    this.api.calculateRoute(this.id).subscribe({
      next: r => { this.calc.set(r); this.geometry.set(r.geometry?.points ?? null); this.routingBusy.set(false); },
      error: () => this.routingBusy.set(false),
    });
  }
  optimize() {
    this.routingBusy.set(true);
    this.api.optimizeRoute(this.id).subscribe({
      next: r => { this.opt.set(r); this.geometry.set(r.geometry?.points ?? null); this.routingBusy.set(false); },
      error: () => this.routingBusy.set(false),
    });
  }
  applyOptimized() {
    const o = this.opt(); if (!o) return;
    // Optimized ids first, then any excluded (no-coord) stops appended → full set for reorder.
    const orderedIds = o.order.map(x => x.stopId);
    const excluded = this.stops().map(s => s.id).filter(id => !orderedIds.includes(id));
    const full = [...orderedIds, ...excluded];
    this.routingBusy.set(true);
    this.api.applyOptimizedOrder(this.id, full).subscribe({
      next: stops => { this.stops.set(stops); this.opt.set(null); this.routingBusy.set(false); },
      error: () => this.routingBusy.set(false),
    });
  }
  cancelPreview() { this.opt.set(null); }

  private loadNavLinks() {
    this.api.navigationLinks(this.id).subscribe({ next: n => this.navLinks.set(n), error: () => {} });
  }

  // ---- drag-to-reorder (native HTML5 DnD, no extra dependency) ----
  onDragStart(i: number, ev: DragEvent) {
    if (!this.canReorder()) return;
    this.dragIndex.set(i);
    ev.dataTransfer?.setData('text/plain', String(i));
    if (ev.dataTransfer) ev.dataTransfer.effectAllowed = 'move';
  }
  onDragOver(i: number, ev: DragEvent) {
    if (this.dragIndex() === null) return;
    ev.preventDefault();            // allow drop
    this.overIndex.set(i);
  }
  onDrop(target: number) {
    const from = this.dragIndex();
    this.overIndex.set(null);
    if (from === null || from === target) { this.dragIndex.set(null); return; }
    const list = [...this.stops()];
    const [moved] = list.splice(from, 1);
    list.splice(target, 0, moved);
    // optimistic re-number for immediate feedback
    this.stops.set(list.map((s, idx) => ({ ...s, sequenceNumber: idx + 1 })));
    this.dragIndex.set(null);
    this.persistOrder(list.map(s => s.id));
  }
  onDragEnd() { this.dragIndex.set(null); this.overIndex.set(null); }

  private persistOrder(stopIds: string[]) {
    this.reordering.set(true); this.reorderError.set('');
    this.api.reorderStops(this.id, stopIds).subscribe({
      next: updated => { this.stops.set(updated); this.reordering.set(false); },
      error: (e) => {
        this.reordering.set(false);
        this.reorderError.set(e?.error?.detail || 'Neusortierung fehlgeschlagen.');
        this.load(); // revert to server truth
      },
    });
  }
}
