import { Component, OnInit, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/api/api.service';
import { Destination, DriverDashboard, GeoPoint, InAppNavigation, NavigationLinks, NavigationStep, ProblemType, RouteDto, Stop } from '../../core/models';
import { pickupTypeLabel, problemTypeLabel, routeStatusLabel, routingWarningLabel, stopStatusLabel, wasteColorVar } from '../../core/labels';
import { navigationUrl } from '../../core/map/map-provider';
import { NAV_CONFIG } from '../../core/config';
import { MapUiStateService } from '../../core/map/map-ui-state.service';
import { NavigationStateService } from '../../core/map/navigation-state.service';
import { BadgeComponent, StateComponent, StopProgressComponent } from '../../design-system/ui';
import { BottomSheetComponent } from '../../design-system/bottom-sheet';
import { RouteMapComponent } from '../routes/route-map.component';
import { NextManeuverCardComponent } from './next-maneuver-card.component';
import { PhotoEntry, ProblemReportModalComponent } from './problem-report-modal.component';

/** The one obvious next thing to do — Uber-inspired single primary action. */
export interface PrimaryAction { label: string; kind: 'start-route' | 'start-stop' | 'complete-stop' | 'complete-route'; }

export function derivePrimaryAction(route: RouteDto | null | undefined, nextStop: Stop | null | undefined,
  doneCount: number, totalCount: number): PrimaryAction | null {
  if (!route) return null;
  if (route.status === 'Planned') return { label: '▶ Route starten', kind: 'start-route' };
  if (route.status !== 'InProgress') return null;
  if (!nextStop || (totalCount > 0 && doneCount === totalCount))
    return { label: '✔ Tour abschließen', kind: 'complete-route' };
  if (nextStop.status === 'Open') return { label: 'Stopp starten', kind: 'start-stop' };
  return { label: '✔ Stopp erledigt', kind: 'complete-stop' };
}

/**
 * Map-first driver field app ("Bordcomputer"): full-bleed route map, floating
 * fraction-railed status strip, floating Navigieren pill, and a bottom sheet —
 * peek = next stop + ONE primary action; half = secondary actions + details;
 * expanded = full tour timeline. Problem report (with photos) stays a modal.
 */
@Component({
  selector: 'ap-driver',
  standalone: true,
  imports: [CommonModule, FormsModule, BadgeComponent, StateComponent, StopProgressComponent,
    BottomSheetComponent, RouteMapComponent, NextManeuverCardComponent, ProblemReportModalComponent],
  providers: [MapUiStateService, NavigationStateService],
  template: `
  <div class="field" *ngIf="!loading(); else loadingTpl">
    <div class="none" *ngIf="!data()?.todaysRoute">
      <ap-state title="Keine Tour zugewiesen"
                message="Für heute ist Ihnen keine Route zugewiesen. Bitte an die Disposition wenden." icon="🚛"></ap-state>
    </div>

    <ng-container *ngIf="data()?.todaysRoute as r">
      <!-- Map is the surface of the job -->
      <ap-route-map class="mapfill" [bare]="true" [driverMode]="true" [stops]="data()!.stops"
                    [fitPaddingBottom]="ui.bottomPadding()"
                    [selectedStopId]="nextStop()?.id ?? null"
                    [geometry]="navGeometry()"
                    [currentPosition]="nav.position()"
                    [recenterTick]="recenterTick()"
                    [followPosition]="nav.follow()"
                    (userMoved)="nav.pauseFollow()"></ap-route-map>

      <!-- Navigation HUD: slim, dark, high contrast — not a dashboard card -->
      <div class="hud" [style.--rail]="fraction(r.wasteTypeName)" role="status" aria-label="Tour-Status">
        <span class="rn ap-id">{{ r.routeNumber }}</span>
        <strong class="hud-name">{{ r.name }}</strong>
        <span class="hud-frak"><i [style.background]="fraction(r.wasteTypeName)"></i>{{ r.wasteTypeName }}</span>
        <span class="hud-count ap-id">{{ data()!.doneCount }} / {{ data()!.totalCount }}</span>
        <span class="hud-status">{{ rl[r.status] }}</span>
        <i class="hud-bar" role="progressbar" [attr.aria-valuenow]="data()!.doneCount"
           [attr.aria-valuemax]="data()!.totalCount" aria-label="Tour-Fortschritt"><b [style.transform]="'scaleX(' + progress() / 100 + ')'"></b></i>
      </div>

      <!-- Navigation layer (in-app; external maps are fallback only) -->
      <div class="nav-stack" *ngIf="nav.navMode()">
        <ap-next-maneuver-card [step]="nextManeuver()"
                               [fallbackTo]="nextStop()?.address ?? null"
                               [fallbackMeta]="fallbackMeta()"
                               [distanceMeters]="nav.distanceToTarget()"></ap-next-maneuver-card>

        <!-- GPS permission panel: actionable, never blocks stop actions -->
        <div class="geo-panel" *ngIf="showGeoPanel()" role="status">
          <strong>Standortfreigabe erforderlich</strong>
          <p>Erlauben Sie den Standortzugriff, damit AbfuhrPilot Sie auf der Tour führen kann.</p>
          <div class="geo-panel-actions">
            <button class="ap-btn ap-btn--primary" (click)="nav.retry()">Standort erneut anfragen</button>
            <button class="ap-btn ap-btn--ghost" (click)="gpsDismissed.set(true)">Ohne Standort fortfahren</button>
            <button *ngIf="canSimulate()" class="ap-btn ap-btn--ghost" (click)="startSim()">Navigation simulieren</button>
          </div>
        </div>

        <div class="geo-banner subtle" *ngIf="nav.geoState() === 'searching' && !nav.simulated()" role="status">Standort wird gesucht …</div>
        <div class="geo-banner subtle" *ngIf="nav.geoState() === 'unavailable' && !nav.simulated()" role="status">
          Standort nicht verfügbar
          <button class="ap-btn ap-btn--ghost mini" (click)="nav.retry()">Erneut versuchen</button>
        </div>
        <div class="geo-banner sim" *ngIf="nav.simulated()" role="status">
          Simulierter Standort aktiv
          <button class="ap-btn ap-btn--ghost mini" (click)="nav.simPaused() ? nav.resumeSimulation() : nav.pauseSimulation()">
            {{ nav.simPaused() ? 'Fortsetzen' : 'Pausieren' }}</button>
        </div>

        <!-- Demo/dev: simulate movement along the route (config-gated, always labeled) -->
        <button *ngIf="canSimulate() && !showGeoPanel()" class="geo-banner sim-btn" (click)="startSim()">
          ▷ Navigation simulieren
        </button>

        <div class="offroute-banner" *ngIf="nav.offRoute()" role="alert">
          <span>Sie scheinen von der Route abzuweichen.</span>
          <button class="ap-btn ap-btn--ghost" (click)="recalculateNavigation(r.id)">Route neu berechnen</button>
        </div>

        <!-- Grouped safety notices (quiet chips, not a wall of banners) -->
        <div class="safety" *ngIf="safetyNotices().length">
          <span class="safety-chip" *ngFor="let n of safetyNotices()">⚠ {{ n }}</span>
        </div>
      </div>

      <!-- Floating in-app navigation toggle -->
      <button *ngIf="r.status === 'InProgress'" class="nav-pill" [class.nav-on]="nav.navMode()"
              (click)="toggleNavigation(r.id)"
              [attr.aria-pressed]="nav.navMode()">
        {{ nav.navMode() ? '◼' : '▶' }}<span>{{ nav.navMode() ? 'Navigation beenden' : 'Navigation starten' }}</span>
      </button>

      <!-- Recenter / follow control -->
      <button *ngIf="nav.navMode() && nav.position()" class="map-ctl" [class.following]="nav.follow()"
              (click)="recenter()" aria-label="Standort zentrieren"
              [title]="nav.follow() ? 'Folgt Standort' : 'Standort zentrieren'">⌖</button>

      <!-- Bottom sheet: peek → half → expanded -->
      <ap-bottom-sheet [state]="ui.sheetState()" (stateChange)="ui.setSheetState($event)"
                       [accent]="fraction(r.wasteTypeName)">
        <div sheet-peek>
          <div class="peek-row" *ngIf="nextStop() as s; else allDone">
            <div class="seq" aria-hidden="true">{{ s.sequenceNumber }}</div>
            <div class="peek-body">
              <div class="peek-label">Nächster Stopp</div>
              <div class="addr">{{ s.address }}</div>
              <small class="ap-subtle">{{ pt[s.pickupType] }}<ng-container *ngIf="s.customerName"> · {{ s.customerName }}</ng-container></small>
              <small class="dist ap-id" *ngIf="distanceText() as d">{{ d }}</small>
            </div>
          </div>
          <ng-template #allDone>
            <div class="peek-row done-row" *ngIf="r.status !== 'Completed'; else completed">
              <div class="seq seq-done" aria-hidden="true">✔</div>
              <div class="peek-body"><div class="addr">Alle Stopps bearbeitet</div>
                <small class="ap-subtle">Tour kann abgeschlossen werden.</small></div>
            </div>
            <ng-template #completed>
              <div class="peek-row done-row">
                <div class="seq seq-done" aria-hidden="true">✔</div>
                <div class="peek-body"><div class="addr">Tour abgeschlossen</div>
                  <!-- Destination CTA when the route names a real end point; otherwise the honest quiet text. -->
                  <small class="ap-subtle" *ngIf="destinationCta() as cta; else quietEnd">{{ cta.name }}</small>
                  <ng-template #quietEnd><small class="ap-subtle">Gute Fahrt zurück zum Betriebshof.</small></ng-template>
                </div>
              </div>
              <a *ngIf="destinationCta() as cta" class="ap-btn ap-btn--primary ap-btn--lg ap-btn--block prim"
                 [href]="cta.href" target="_blank" rel="noopener"
                 [attr.aria-label]="cta.label + ': ' + cta.name + ' — Navigation öffnen'">{{ cta.label }}</a>
            </ng-template>
          </ng-template>
          <ap-stop-progress class="peek-segs" *ngIf="data()!.stops.length"
                            [stops]="data()!.stops"></ap-stop-progress>
          <!-- Tour totals: counts always, km/ETA only when the provider delivered them -->
          <div class="peek-tour ap-id" *ngIf="data()!.totalCount">
            <span>{{ data()!.doneCount }}/{{ data()!.totalCount }} erledigt</span>
            <span *ngIf="tourMeta() as t">{{ t }}</span>
          </div>
          <button *ngIf="primary() as p" class="ap-btn ap-btn--primary ap-btn--lg ap-btn--block prim"
                  (click)="onPrimary()" [disabled]="busy()">{{ p.label }}</button>
          <p *ngIf="r.status === 'Planned'" class="hint ap-subtle">Starten Sie die Route, um Stopps zu bearbeiten.</p>
        </div>

        <!-- half: secondary actions + tour details -->
        <div class="secondary" *ngIf="r.status === 'InProgress' && nextStop()">
          <button class="ap-btn ap-btn--ghost" (click)="act(nextStop()!.id, 'skip')" [disabled]="busy()">Überspringen</button>
          <button class="ap-btn ap-btn--ghost danger-ghost" (click)="openProblem(nextStop()!)" [disabled]="busy()">⚠ Problem melden</button>
        </div>
        <div class="meta">
          <div><small>Abfallart</small><strong class="fraction"><i [style.background]="fraction(r.wasteTypeName)"></i>{{ r.wasteTypeName }}</strong></div>
          <div><small>Fahrzeug</small><strong>{{ r.vehicleName || '—' }}</strong></div>
          <!-- External maps: emergency fallback only — never the primary flow. -->
          <div class="fallback-links">
            <small>Notfall-Fallback</small>
            <a *ngIf="nextStop() as s" class="maps-link" [href]="mapUrl(s)" target="_blank" rel="noopener">
              Nächster Stopp — In externer Karte öffnen ↗</a>
            <a *ngIf="navLinks() as n" class="maps-link" [href]="n.googleMaps" target="_blank" rel="noopener">
              Gesamtroute — In externer Karte öffnen ↗</a>
          </div>
        </div>

        <!-- expanded: route context + full timeline -->
        <div sheet-expanded>
          <div class="warn-detail" *ngIf="nav.navMode() && navWarnings().length">
            <h3 class="tl-head">Hinweise zur Route</h3>
            <ul class="warn-list"><li *ngFor="let w of navWarnings()">⚠ {{ w }}</li></ul>
          </div>
          <h3 class="tl-head">Tour-Verlauf</h3>
          <!-- Connector rail carries the tour's fraction color (vertical timeline rail
               using the fraction color) — tinted toward
               --c-border so pale fractions stay visible and loud ones stay calm.
               Status truth stays exclusively on the dots. -->
          <ul class="timeline" [style.--rail]="fraction(r.wasteTypeName)">
            <li *ngFor="let s of data()!.stops" [attr.data-tone]="tone(s.status)">
              <span class="dot"></span>
              <div class="ts-body">
                <div class="ts-top"><strong>{{ s.sequenceNumber }}. {{ s.address }}</strong>
                  <ap-badge [status]="s.status" [label]="sl[s.status]"></ap-badge></div>
                <small class="ap-subtle" *ngIf="s.problemReason">Problem: {{ pl[s.problemReason] }}</small>
                <small class="ap-subtle" *ngIf="s.completedAt">Erledigt um {{ s.completedAt | date:'HH:mm' }}</small>
              </div>
            </li>
          </ul>
        </div>
      </ap-bottom-sheet>
    </ng-container>

    <!-- Problem modal (photos included) -->
    <ap-problem-report-modal *ngIf="problemFor() as s" [stop]="s" [problemTypes]="problemTypes"
                              [labels]="pl" [photos]="photos()" [busy]="busy()" [uploading]="uploading()"
                              (closed)="closeProblem()" (submitted)="submitProblem($event)"
                              (photoSelected)="onPhotoSelected($event)" (photoRemoved)="removePhoto($event)">
    </ap-problem-report-modal>
  </div>
  <ng-template #loadingTpl><div class="load"><ap-state [loading]="true"></ap-state></div></ng-template>`,
  styles: [`
    /* Daylight cab readability: the global --c-text-subtle (#93a09a, ~2.7:1 on white)
       is tuned for dense dispatcher scanning, not a sunlit tablet. Darken it for the
       whole driver surface (incl. problem-report modal) without touching the token.
       #697670 = 4.74:1 on white — AA for normal text.
       Stays lighter than --c-text-muted (5.56:1) so subtle < muted hierarchy holds. */
    :host { --c-text-subtle:#697670; }

    /* No admin chrome on /fahrer — the map IS the page (full viewport). */
    .field { position:relative; height:100vh; height:100dvh; margin:0; overflow:hidden;
      border-radius:0; animation:ap-fade-up var(--t-enter) both; }
    .mapfill { position:absolute; inset:0; display:block; }
    .load, .none { padding:28px 24px; }
    .none { position:relative; z-index:5; max-width:560px; margin:0 auto; }

    /* Route pill — compact navigation overlay, not a dashboard strip.
       Sits right of the shell's floating menu button (44px + margins). */
    .hud { position:absolute; top:14px; left:68px; right:12px; z-index:15;
      width:max-content; max-width:calc(100% - 80px); margin:0 auto;
      display:flex; align-items:center; gap:10px; padding:8px 18px 10px;
      background:rgba(14,26,20,.94); color:#fff; border-radius:999px; box-shadow:var(--sh-lg);
      backdrop-filter:blur(6px); overflow:hidden; }
    .hud .rn { font-size:12.5px; color:var(--c-primary); white-space:nowrap; }
    .hud-name { min-width:0; font-size:13.5px; font-weight:650; white-space:nowrap;
      overflow:hidden; text-overflow:ellipsis; max-width:34vw; }
    .hud-frak { display:inline-flex; align-items:center; gap:6px; font-size:12px; font-weight:650;
      color:rgba(255,255,255,.8); white-space:nowrap; }
    .hud-frak i { width:8px; height:8px; border-radius:3px; }
    .hud-count { font-size:12.5px; font-weight:700; white-space:nowrap; }
    .hud-status { font-size:12.5px; font-weight:750; color:var(--c-primary); white-space:nowrap; }
    .hud-bar { position:absolute; left:0; right:0; bottom:0; height:3px; background:rgba(255,255,255,.12); }
    .hud-bar b { display:block; height:100%; width:100%; background:var(--c-primary);
      transform-origin:left; transform:scaleX(0); transition:transform var(--t-enter); }
    @media (max-width:640px) { .hud-frak, .hud-status { display:none; } .hud-name { max-width:30vw; } }

    /* Floating in-app navigation toggle — high-contrast mobility control */
    .nav-pill { position:absolute; right:16px; bottom:calc(34vh + 18px); z-index:16;
      display:inline-flex; align-items:center; gap:8px; padding:14px 20px; min-height:56px;
      background:var(--c-shell); color:#fff; font-weight:700; font-size:15px; border-radius:999px;
      border:none; cursor:pointer;
      box-shadow:var(--sh-lg); transition:transform var(--t-fast), box-shadow var(--t-fast), background var(--t-fast); }
    @media (hover:hover) and (pointer:fine) { .nav-pill:hover { transform:translateY(-1px); } }
    .nav-pill:active { transform:scale(.97); }
    .nav-pill.nav-on { background:var(--c-primary-deep); }
    @media (min-width:1000px) { .nav-pill { right:440px; bottom:20px; } }

    /* Navigation layer: maneuver card + banners under the route pill */
    .nav-stack { position:absolute; top:66px; left:12px; z-index:15; display:flex; flex-direction:column;
      gap:8px; max-width:min(400px, calc(100% - 24px)); }
    .geo-banner { background:var(--c-skip-soft); color:var(--c-skip); border-radius:12px;
      padding:9px 14px; font-size:13px; font-weight:650; box-shadow:var(--sh-md);
      animation:ap-fade-up var(--t-enter) both; }
    .geo-banner.subtle { background:rgba(255,255,255,.94); color:var(--c-text-muted);
      display:flex; align-items:center; gap:10px; }
    .geo-banner.sim { background:var(--c-info-soft); color:var(--c-info); }
    .geo-banner .mini { min-height:32px; padding:4px 10px; font-size:12px; }
    .sim-btn { border:1px dashed var(--c-border-strong); background:rgba(255,255,255,.94);
      color:var(--c-text-muted); cursor:pointer; text-align:left; font-weight:650; }

    .geo-panel { background:var(--c-surface); border-radius:14px; padding:14px 16px;
      box-shadow:var(--sh-lg); animation:ap-fade-up var(--t-enter) both; }
    .geo-panel strong { display:block; font-size:14.5px; margin-bottom:4px; }
    .geo-panel p { margin:0 0 12px; font-size:13px; color:var(--c-text-muted); line-height:1.45; }
    .geo-panel-actions { display:flex; gap:10px; flex-wrap:wrap; }
    .geo-panel-actions .ap-btn { min-height:56px; }

    /* Grouped safety chips */
    .safety { display:flex; flex-wrap:wrap; gap:6px; }
    .safety-chip { background:rgba(255,255,255,.92); color:var(--c-skip); border:1px solid var(--c-border);
      border-radius:99px; padding:4px 12px; font-size:12px; font-weight:650; box-shadow:var(--sh-sm); }

    .map-ctl.following { border-color:var(--c-primary); color:var(--c-primary-strong);
      box-shadow:0 0 0 3px var(--c-primary-glow), var(--sh-lg); }
    .offroute-banner { background:var(--c-problem-soft); color:var(--c-problem); border-radius:12px;
      padding:10px 14px; font-size:13px; font-weight:700; box-shadow:var(--sh-md);
      display:flex; align-items:center; gap:12px; flex-wrap:wrap;
      animation:ap-fade-up var(--t-enter) both; }
    .offroute-banner .ap-btn { min-height:56px; padding:7px 14px; }

    /* Recenter map control */
    .map-ctl { position:absolute; left:16px; bottom:calc(34vh + 18px); z-index:16;
      width:56px; height:56px; border-radius:50%; border:1px solid var(--c-border);
      background:var(--c-surface); color:var(--c-text); font-size:24px; cursor:pointer;
      box-shadow:var(--sh-lg); transition:transform var(--t-fast); }
    .map-ctl:active { transform:scale(.94); }
    @media (min-width:1000px) { .map-ctl { bottom:20px; } }

    .dist { display:block; margin-top:3px; font-size:13px; font-weight:700; color:var(--c-primary-strong); }
    .fallback-links { display:flex; flex-direction:column; gap:4px; }

    /* Sheet peek — compact app row, not a hero card */
    .peek-row { display:flex; gap:12px; align-items:center; margin-bottom:10px; }
    .seq { flex:0 0 44px; width:44px; height:44px; border-radius:12px; display:grid; place-items:center;
      background:var(--c-shell); color:#fff; font-family:var(--font-mono); font-size:19px; font-weight:700;
      font-variant-numeric:tabular-nums; }
    .seq-done { background:var(--c-done); font-size:21px; }
    .peek-body { min-width:0; }
    .peek-label { font-size:12.5px; font-weight:750; color:var(--c-primary-strong); }
    .addr { font-size:17px; font-weight:700; line-height:1.25; overflow-wrap:break-word; }
    .peek-segs { display:block; margin:0 0 6px; }
    .peek-tour { display:flex; justify-content:space-between; gap:10px; margin:0 0 10px;
      font-size:12px; color:var(--c-text-subtle); }
    .prim { font-size:17px; }
    .hint { margin:10px 0 0; font-size:13px; }

    /* Sheet half content */
    .secondary { display:grid; grid-template-columns:1fr 1fr; gap:10px; margin:4px 0 14px; }
    .secondary .ap-btn { min-height:56px; }
    .danger-ghost { color:var(--c-problem); border-color:color-mix(in srgb, var(--c-problem) 40%, var(--c-border-strong)); }
    .danger-ghost:hover { background:var(--c-problem-soft); }
    .meta { display:flex; flex-direction:column; gap:10px; padding-bottom:6px; }
    .meta small { display:block; color:var(--c-text-subtle); font-size:12px; font-weight:600; margin-bottom:2px; }
    .meta strong { font-size:14px; }
    .fraction { display:inline-flex; align-items:center; gap:7px; }
    .fraction i { width:10px; height:10px; border-radius:3px; display:inline-block; }
    .maps-link { color:var(--c-primary-strong); font-weight:600; font-size:14px; }

    /* Expanded sheet */
    .warn-list { list-style:none; margin:0 0 6px; padding:0; }
    .warn-list li { font-size:13px; color:var(--c-skip); padding:3px 0; }
    .tl-head { font-size:14px; margin:12px 0 10px; }
    .timeline { list-style:none; margin:0; padding:0; }
    .timeline li { display:flex; gap:14px; padding:0 0 14px; position:relative; }
    .timeline li:not(:last-child)::after { content:''; position:absolute; left:6px; top:16px; bottom:0; width:2px;
      background:color-mix(in srgb, var(--rail, var(--c-border)) 45%, var(--c-border)); }
    .dot { width:14px; height:14px; border-radius:50%; background:var(--c-border-strong); margin-top:3px; flex:0 0 14px; z-index:1; }
    li[data-tone=done] .dot { background:var(--c-done); } li[data-tone=problem] .dot { background:var(--c-problem); }
    li[data-tone=progress] .dot { background:var(--c-progress); } li[data-tone=skip] .dot { background:var(--c-skip); }
    .ts-body { flex:1; } .ts-top { display:flex; justify-content:space-between; gap:10px; align-items:center; }

  `],
})
export class DriverComponent implements OnInit {
  data = signal<DriverDashboard | null>(null);
  loading = signal(true);
  busy = signal(false);
  problemFor = signal<Stop | null>(null);
  photos = signal<PhotoEntry[]>([]);
  navLinks = signal<NavigationLinks | null>(null);
  problemTypes: ProblemType[] = ['BlockedByCar', 'BinNotAvailable', 'ContainerNotFound', 'AddressNotFound', 'RoadClosed', 'CustomerNotAvailable', 'WrongWasteType', 'VehicleIssue', 'Other'];
  rl = routeStatusLabel; sl = stopStatusLabel; pl = problemTypeLabel;
  pt: Record<string, string> = pickupTypeLabel;

  nextStop = computed(() => this.data()?.nextStop ?? null);
  primary = computed<PrimaryAction | null>(() => {
    const d = this.data();
    return derivePrimaryAction(d?.todaysRoute, d?.nextStop, d?.doneCount ?? 0, d?.totalCount ?? 0);
  });

  // ---- end-of-tour destination (completed state) ----
  /** Resolved end destination — lazily fetched only when the tour is completed. */
  endDestination = signal<Destination | null>(null);
  private destinationRequestedFor: string | null = null;
  /** CTA wording reuses the product's destination language (destinationTypeLabel: Betriebshof, Recyclinghof, …). */
  private static readonly DEST_CTA: Record<string, string> = {
    Depot: 'Zum Betriebshof', RecyclingCenter: 'Zum Recyclinghof', Landfill: 'Zur Deponie',
    TransferStation: 'Zum Umschlagplatz', CustomerSite: 'Zum Kundenstandort',
  };
  /** ONE dominant closing action, or null → the honest quiet text (never a broken/empty CTA). */
  destinationCta = computed<{ label: string; name: string; href: string } | null>(() => {
    const dest = this.endDestination();
    if (!dest) return null;
    const label = DriverComponent.DEST_CTA[dest.type];
    const hasTarget = (dest.latitude != null && dest.longitude != null) || !!dest.address;
    if (!label || !hasTarget) return null;
    return { label, name: dest.name, href: navigationUrl(dest.latitude, dest.longitude, dest.address) };
  });

  // ---- in-app navigation state ----
  navData = signal<InAppNavigation | null>(null);
  recenterTick = signal(0);
  /** „Ohne Standort fortfahren" hides the permission panel for this session. */
  gpsDismissed = signal(false);
  showGeoPanel = computed(() => this.nav.geoState() === 'denied' && !this.gpsDismissed() && !this.nav.simulated());
  canSimulate = computed(() =>
    NAV_CONFIG.enableSimulation && !this.nav.simulated() && !this.nav.position()
    && (this.navData()?.geometry?.length ?? 0) > 1);
  /** Fallback guidance meta: „Stopp 2 · Regelabholung". */
  fallbackMeta = computed(() => {
    const s = this.nextStop();
    return s ? `Stopp ${s.sequenceNumber} · ${this.pt[s.pickupType] ?? s.pickupType}` : null;
  });
  /** Grouped quiet safety notices (the loud cases have their own banners). */
  safetyNotices = computed(() => {
    const out: string[] = [];
    const n = this.navData();
    if (!this.nav.navMode() || !n) return out;
    if (!n.geometry?.length) out.push('Keine Route berechnet');
    if (!n.truckRestrictionsApplied) out.push('LKW-Beschränkungen nicht aktiv');
    if (!this.nextManeuver()) out.push('Abbiegehinweise nicht verfügbar');
    return out;
  });
  /** Full warning detail for the expanded sheet (chips stay compact). */
  navWarnings = computed(() =>
    (this.navData()?.warnings ?? []).map(w => routingWarningLabel[w.code] ?? w.message));
  navGeometry = computed<GeoPoint[] | null>(() =>
    this.nav.navMode() ? (this.navData()?.geometry ?? null) : null);
  /** First real provider step, or null → honest fallback card. */
  nextManeuver = computed<NavigationStep | null>(() => {
    for (const leg of this.navData()?.legs ?? []) if (leg.steps.length) return leg.steps[0];
    return null;
  });
  /** "12,9 km · 1 h 45 min" — only from real provider totals, only while navigating. */
  tourMeta = computed<string | null>(() => {
    const n = this.navData();
    if (!this.nav.navMode() || !n?.totalDistanceMeters) return null;
    const km = (n.totalDistanceMeters / 1000).toFixed(1).replace('.', ',');
    const min = Math.round(n.totalDurationSeconds / 60);
    const dur = min >= 60 ? `${Math.floor(min / 60)} h ${min % 60} min` : `${min} min`;
    return `${km} km · ${dur}`;
  });

  distanceText = computed<string | null>(() => {
    if (!this.nav.navMode()) return null;
    const d = this.nav.distanceToTarget();
    if (d !== null) return `Noch ${d < 1000 ? d + ' m' : (d / 1000).toFixed(1).replace('.', ',') + ' km'} bis zum Stopp`;
    return this.nav.geoState() === 'active' ? null : 'Entfernung verfügbar nach Standortfreigabe';
  });

  constructor(private api: ApiService, public ui: MapUiStateService, public nav: NavigationStateService) {}
  ngOnInit() { this.load(); }

  load() {
    this.loading.set(true);
    this.api.driverDashboard().subscribe({
      next: d => {
        this.data.set(d); this.loading.set(false);
        const routeId = d.todaysRoute?.id;
        this.navLinks.set(null);
        this.syncNavTarget();
        this.resolveEndDestination();
        if (routeId) this.api.navigationLinks(routeId).subscribe({ next: n => this.navLinks.set(n), error: () => {} });
      },
      error: () => this.loading.set(false),
    });
  }

  /** Fetches the closing destination only on entering the completed state, once per id —
   *  reuses the existing GET /destinations list (no new endpoint) and stays silent on failure. */
  private resolveEndDestination() {
    const r = this.data()?.todaysRoute;
    const id = r?.status === 'Completed' ? (r.endDestinationId ?? r.startDepotId ?? null) : null;
    // Reset the once-per-id latch whenever we leave the completed state, so a recurring
    // destination id (same Betriebshof every day, or a route reopened by dispatch) refetches.
    if (!id) { this.destinationRequestedFor = null; this.endDestination.set(null); return; }
    if (this.destinationRequestedFor === id) return;
    this.destinationRequestedFor = id;
    this.api.destinations({ pageSize: 200 }).subscribe({
      // Deactivated destinations must never become a driver CTA — fall back to the quiet text.
      // Note: list is capped at PageQuery.MaxPageSize (200); >200 destinations degrade to the quiet text.
      next: p => this.endDestination.set(p.items.find(dest => dest.id === id && dest.isActive) ?? null),
      // Honest fallback: quiet text now, but clear the latch so the next dashboard load retries
      // instead of a single transient error suppressing the CTA for the tab's lifetime.
      error: () => { this.destinationRequestedFor = null; this.endDestination.set(null); },
    });
  }

  private syncNavTarget() {
    const s = this.data()?.nextStop;
    this.nav.target.set(s && s.latitude != null && s.longitude != null
      ? { latitude: s.latitude, longitude: s.longitude } : null);
  }

  toggleNavigation(routeId: string) {
    if (this.nav.navMode()) {
      this.nav.endNavigation();
      this.navData.set(null);
      this.nav.routeGeometry.set([]);
      this.gpsDismissed.set(false);
      return;
    }
    this.nav.startNavigation();
    this.fetchNavigation(routeId);
  }

  /** „Route neu berechnen" after off-route — re-fetches in-app navigation. */
  recalculateNavigation(routeId: string) { this.fetchNavigation(routeId); }

  private fetchNavigation(routeId: string) {
    this.api.inAppNavigation(routeId).subscribe({
      next: n => { this.navData.set(n); this.nav.routeGeometry.set(n.geometry ?? []); },
      error: () => { /* map stays in route view; actions unaffected */ },
    });
  }

  /** Start the demo simulation (also dismisses the GPS panel). */
  startSim() {
    this.gpsDismissed.set(true);
    this.nav.startSimulation();
  }

  /** Recenter + re-enable follow mode („Folgt Standort"). */
  recenter() {
    this.nav.resumeFollow();
    this.recenterTick.update(t => t + 1);
  }

  progress() { const d = this.data(); return d && d.totalCount ? (d.doneCount / d.totalCount) * 100 : 0; }
  tone(s: string) { return { Done: 'done', Problem: 'problem', InProgress: 'progress', Skipped: 'skip' }[s] ?? 'open'; }
  fraction(name?: string | null) { return wasteColorVar(name); }
  mapUrl(s: Stop) {
    return s.latitude != null && s.longitude != null
      ? `https://www.google.com/maps/dir/?api=1&destination=${s.latitude},${s.longitude}`
      : `https://www.google.com/maps/search/?api=1&query=${encodeURIComponent(s.address)}`;
  }

  onPrimary() {
    const p = this.primary(); const d = this.data();
    if (!p || !d?.todaysRoute) return;
    switch (p.kind) {
      case 'start-route': return this.startRoute(d.todaysRoute.id);
      case 'complete-route': return this.completeRoute(d.todaysRoute.id);
      case 'start-stop': return this.act(d.nextStop!.id, 'start');
      case 'complete-stop': return this.act(d.nextStop!.id, 'done');
    }
  }

  private run(obs: any) { this.busy.set(true); obs.subscribe({ next: () => { this.busy.set(false); this.load(); }, error: () => this.busy.set(false) }); }
  startRoute(id: string) { this.run(this.api.startRoute(id)); }
  completeRoute(id: string) { this.run(this.api.completeRoute(id)); }
  act(id: string, kind: 'start' | 'done' | 'skip') {
    const map = { start: this.api.startStop(id), done: this.api.completeStop(id), skip: this.api.skipStop(id) };
    this.run(map[kind]);
  }

  openProblem(s: Stop) {
    this.problemFor.set(s);
    this.clearPhotos();
  }
  closeProblem() { this.clearPhotos(); this.problemFor.set(null); }

  onPhotoSelected(ev: Event) {
    const input = ev.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;
    const entry: PhotoEntry = { name: file.name, objectUrl: URL.createObjectURL(file), uploading: true };
    this.photos.update(list => [...list, entry]);
    this.api.uploadProblemPhoto(file).subscribe({
      next: dto => this.photos.update(list => list.map(p => p === entry ? { ...p, id: dto.id, uploading: false } : p)),
      error: () => this.photos.update(list => list.map(p => p === entry ? { ...p, uploading: false, error: true } : p)),
    });
  }
  removePhoto(i: number) {
    this.photos.update(list => {
      const p = list[i];
      if (p) URL.revokeObjectURL(p.objectUrl);
      return list.filter((_, idx) => idx !== i);
    });
  }
  uploading() { return this.photos().some(p => p.uploading); }
  private clearPhotos() {
    this.photos().forEach(p => URL.revokeObjectURL(p.objectUrl));
    this.photos.set([]);
  }

  submitProblem(input: { type: ProblemType; note: string }) {
    const s = this.problemFor(); if (!s || this.uploading()) return;
    const photoIds = this.photos().filter(p => p.id && !p.error).map(p => p.id!) as string[];
    this.busy.set(true);
    this.api.reportProblem(s.id, input.type, input.note, photoIds).subscribe({
      next: () => { this.busy.set(false); this.closeProblem(); this.load(); },
      error: () => this.busy.set(false),
    });
  }
}
