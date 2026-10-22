import {
  Component, ElementRef, Input, OnChanges, OnDestroy, SimpleChanges, ViewChild, signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import * as L from 'leaflet';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { ApiService } from '../../core/api/api.service';
import { RouteDto, Stop } from '../../core/models';
import { routeStatusLabel, wasteColorVar } from '../../core/labels';
import { MAP_CONFIG } from '../../core/config';
import { createMapProvider } from '../../core/map/map-provider';
import { BadgeComponent, StateComponent } from '../../design-system/ui';

interface TourMarkerGroup {
  route: RouteDto;
  color: string;
  stops: Stop[];
}

/**
 * Leitstand live-ops map — spatial overview of today's active tours.
 * Stop dots only (fraction-colored), no live GPS route polylines: that needs a
 * backend batching/caching decision for on-demand routing geometry.
 * Deliberately its own lean component — NOT an extension of `RouteMapComponent`,
 * which is single-route/single-GPS-puck by design for the driver cockpit.
 */
@Component({
  selector: 'ap-live-ops-map',
  standalone: true,
  imports: [CommonModule, RouterLink, StateComponent, BadgeComponent],
  template: `
  <div class="live-ops ap-card">
    <ap-state *ngIf="!routes.length" title="Keine aktive Tour"
      message="Sobald eine Tour startet, erscheint sie hier auf der Karte." icon="🗺"></ap-state>

    <ap-state *ngIf="routes.length && !leafletFailed() && !loadingStops() && !markerGroups().length"
      title="Keine Koordinaten für aktive Touren hinterlegt"
      message="Für die aktiven Touren sind keine Stopp-Koordinaten gespeichert." icon="🗺"></ap-state>

    <ap-state *ngIf="leafletFailed()" title="Karte nicht verfügbar" icon="🗺"></ap-state>

    <div *ngIf="routes.length && !leafletFailed() && markerGroups().length" class="map-wrap">
      <div #mapEl class="leaflet-map"></div>

      <div class="chips" role="list" aria-label="Aktive Touren">
        <a *ngFor="let g of markerGroups()" role="listitem" class="chip"
           [routerLink]="['/routen', g.route.id]" [style.--dot]="g.color">
          <span class="ap-id">{{ g.route.routeNumber }}</span>
          <ap-badge status="InProgress" [label]="rl['InProgress']"></ap-badge>
        </a>
      </div>

      <div class="stats ap-id" aria-live="polite">{{ statsLine() }}</div>
    </div>
  </div>`,
  styles: [`
    /* Leitstand map panel: the one deliberately Tanne-dark instrument on an
       otherwise Papier-light dispatch screen. */
    .live-ops { padding:0; overflow:clip; }
    .map-wrap { position:relative; }
    .leaflet-map { width:100%; height:340px; background:var(--c-shell); }

    .chips { position:absolute; top:12px; left:12px; right:12px; display:flex; flex-wrap:wrap; gap:8px;
      z-index:var(--z-overlay); pointer-events:none; }
    .chip { pointer-events:auto; display:inline-flex; align-items:center; gap:8px; padding:6px 10px 6px 12px;
      border-radius:999px; background:var(--c-shell-2); border:1px solid var(--c-shell-border);
      color:var(--c-shell-text); text-decoration:none; transition:background var(--t-fast), transform var(--t-fast); }
    .chip::before { content:''; width:8px; height:8px; border-radius:2px; background:var(--dot, var(--w-hausmuell)); flex:none; }
    .chip .ap-id { color:var(--c-shell-text); }
    @media (hover:hover) and (pointer:fine) {
      .chip:hover { background:var(--c-shell-border); }
    }
    .chip:active { transform:scale(0.97); }

    .stats { position:absolute; bottom:12px; left:12px; padding:5px 12px; border-radius:8px;
      background:var(--c-shell-2); border:1px solid var(--c-shell-border); color:var(--c-shell-text-dim);
      font-size:12px; z-index:var(--z-overlay); }
  `],
})
export class LiveOpsMapComponent implements OnChanges, OnDestroy {
  @Input() routes: RouteDto[] = [];

  rl = routeStatusLabel;
  loadingStops = signal(false);
  leafletFailed = signal(false);
  markerGroups = signal<TourMarkerGroup[]>([]);

  private provider = createMapProvider(MAP_CONFIG);
  private map?: L.Map;
  private layer?: L.LayerGroup;

  @ViewChild('mapEl') set mapEl(el: ElementRef<HTMLDivElement> | undefined) {
    if (!el) {
      // The `*ngIf` host was torn down (routes went empty, or coords disappeared) —
      // drop the detached Leaflet instance so a later re-appearance re-initializes
      // cleanly instead of leaking the old map and leaving the new container blank.
      this.map?.remove();
      this.map = undefined;
      this.layer = undefined;
      return;
    }
    if (!this.map) this.initMap(el.nativeElement);
    else this.renderMarkers();
  }

  constructor(private api: ApiService, private router: Router) {}

  ngOnChanges(changes: SimpleChanges) {
    if (changes['routes']) this.fetchStops();
  }

  ngOnDestroy() { this.map?.remove(); }

  statsLine(): string {
    const groups = this.markerGroups();
    const done = groups.reduce((sum, g) => sum + g.route.doneCount, 0);
    const total = groups.reduce((sum, g) => sum + g.route.stopCount, 0);
    const n = groups.length;
    return `${done}/${total} Stopps · ${n} Tour${n === 1 ? '' : 'en'} aktiv`;
  }

  private fetchStops() {
    const routes = this.routes;
    if (!routes.length) {
      this.markerGroups.set([]);
      this.loadingStops.set(false);
      return;
    }
    this.loadingStops.set(true);
    const calls = routes.map(r => this.api.routeStops(r.id).pipe(catchError(() => of([] as Stop[]))));
    forkJoin(calls).subscribe(results => {
      const groups: TourMarkerGroup[] = routes
        .map((route, i) => ({
          route,
          color: wasteColorVar(route.wasteTypeName),
          stops: (results[i] ?? []).filter(s => s.latitude != null && s.longitude != null),
        }))
        .filter(g => g.stops.length > 0);
      this.markerGroups.set(groups);
      this.loadingStops.set(false);
      this.renderMarkers();
    });
  }

  private initMap(el: HTMLDivElement) {
    try {
      const spec = this.provider.tileLayer()!;
      this.map = L.map(el, { attributionControl: true, scrollWheelZoom: true }).setView([52.52, 13.405], 12);
      L.tileLayer(spec.urlTemplate, { attribution: spec.attribution, maxZoom: spec.maxZoom }).addTo(this.map);
      this.layer = L.layerGroup().addTo(this.map);
      this.renderMarkers();
      setTimeout(() => this.map?.invalidateSize(), 0);
    } catch {
      this.leafletFailed.set(true);
      this.map = undefined;
    }
  }

  private renderMarkers() {
    if (!this.map || !this.layer) return;
    this.layer.clearLayers();
    const latlngs: L.LatLngTuple[] = [];
    for (const g of this.markerGroups()) {
      for (const s of g.stops) {
        const marker = L.marker([s.latitude!, s.longitude!], { icon: this.dotIcon(g.color) });
        marker.bindPopup(this.popupHtml(g.route, s));
        marker.on('click', () => this.router.navigate(['/routen', g.route.id]));
        marker.addTo(this.layer!);
        latlngs.push([s.latitude!, s.longitude!]);
      }
    }
    if (latlngs.length) {
      const bounds = L.latLngBounds(latlngs);
      if (bounds.isValid()) this.map.fitBounds(bounds, { padding: [30, 30], maxZoom: 14 });
    }
  }

  private dotIcon(color: string): L.DivIcon {
    const html = `<div style="width:14px;height:14px;border-radius:4px;background:${color};` +
      `border:2px solid #fff;box-shadow:0 1px 4px rgba(0,0,0,.5)"></div>`;
    return L.divIcon({ className: '', html, iconSize: [14, 14], iconAnchor: [7, 7] });
  }

  private popupHtml(route: RouteDto, s: Stop): string {
    return `<div style="min-width:160px;font:13px system-ui,sans-serif">` +
      `<strong>${this.esc(route.routeNumber)}</strong> · ${this.esc(route.wasteTypeName ?? '')}<br>` +
      `<span style="color:#667085">Stopp ${s.sequenceNumber}</span> · ${this.esc(s.address)}</div>`;
  }

  private esc(v: string): string {
    return v.replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]!));
  }
}
