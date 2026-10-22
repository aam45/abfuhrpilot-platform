import {
  AfterViewInit, Component, ElementRef, EventEmitter, Input, OnChanges, OnDestroy, Output, SimpleChanges, ViewChild, computed, signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import * as L from 'leaflet';
import { GeoPoint, Stop } from '../../core/models';
import { pickupTypeLabel, statusTone, stopStatusLabel } from '../../core/labels';
import { MAP_CONFIG } from '../../core/config';
import { createMapProvider, MapMarker, navigationUrl, toneColor } from '../../core/map/map-provider';

interface PlottedStop { x: number; y: number; label: string; tone: string; status: string; address: string; }
export interface MapPoi { lat: number; lng: number; name: string; }

/**
 * Route map card with three rendering tiers behind the `MapProvider` abstraction:
 *  1. interactive Leaflet/OSM tiles (default, no key) — numbered status markers,
 *     polyline, depot/end markers, click popups;
 *  2. static-map image (Google/Mapbox/OSM-static) when a key provider is set;
 *  3. dependency-free schematic SVG fallback (no coords, or map failure).
 */
@Component({
  selector: 'ap-route-map',
  standalone: true,
  imports: [CommonModule],
  template: `
  <div class="map" [class.ap-card]="!bare" [class.bare]="bare">
    <div class="head" *ngIf="!bare">
      <h3>Karte</h3>
      <div class="head-right">
        <a *ngIf="fullRouteUrl()" class="link" [href]="fullRouteUrl()" target="_blank" rel="noopener">🧭 Route in Google Maps</a>
        <span class="src ap-subtle">{{ providerLabel() }}</span>
      </div>
    </div>

    <!-- No coordinates at all -->
    <div *ngIf="!plotted().length" class="empty">
      <div class="emoji">🗺</div>
      <p class="ap-muted">Keine Koordinaten für diese Stopps hinterlegt.</p>
    </div>

    <!-- 1) Interactive Leaflet map -->
    <div *ngIf="useInteractive()" #mapEl class="leaflet"></div>

    <!-- 2) Static tiles image -->
    <img *ngIf="!useInteractive() && plotted().length && staticUrl() && !imgFailed()" class="tiles" [src]="staticUrl()!"
         alt="Streckenkarte" (error)="imgFailed.set(true)" />

    <!-- 3) Schematic fallback -->
    <div *ngIf="useSchematic()" class="schematic">
      <svg [attr.viewBox]="'0 0 ' + W + ' ' + H" preserveAspectRatio="xMidYMid meet" role="img" aria-label="Schematische Streckenkarte">
        <defs>
          <pattern id="grid" width="40" height="40" patternUnits="userSpaceOnUse">
            <path d="M 40 0 L 0 0 0 40" fill="none" stroke="var(--c-border)" stroke-width="1" opacity="0.5"/>
          </pattern>
        </defs>
        <rect [attr.width]="W" [attr.height]="H" fill="url(#grid)"></rect>
        <polyline [attr.points]="polyline()" fill="none" stroke="var(--c-border-strong)" stroke-width="2" stroke-dasharray="4 4"></polyline>
        <g *ngFor="let p of plotted()">
          <circle [attr.cx]="p.x" [attr.cy]="p.y" r="15" [attr.fill]="toneVar(p.tone)" stroke="#fff" stroke-width="2"></circle>
          <text [attr.x]="p.x" [attr.y]="p.y + 4" text-anchor="middle" font-size="12" font-weight="700" fill="#fff">{{ p.label }}</text>
          <title>{{ p.label }}. {{ p.address }} — {{ sl[p.status] }}</title>
        </g>
      </svg>
      <p class="hint ap-subtle" *ngIf="leafletFailed()">Interaktive Karte nicht verfügbar — schematische Ansicht.</p>
    </div>

    <!-- Legend -->
    <div class="legend" *ngIf="plotted().length && !bare">
      <span *ngFor="let t of legend"><i [style.background]="toneVar(t.tone)"></i>{{ t.label }}</span>
    </div>
  </div>`,
  styles: [`
    .map { padding:18px 20px; }
    .map.bare { padding:0; height:100%; border:none; border-radius:0; background:transparent; }
    .map.bare .leaflet { height:100%; border:none; border-radius:0; }
    .map.bare .empty, .map.bare .schematic { padding:18px; background:var(--c-surface-2); height:100%; }
    .head { display:flex; align-items:center; justify-content:space-between; margin-bottom:12px; }
    .head-right { display:flex; align-items:center; gap:12px; }
    .link { color:var(--c-primary-strong); font-weight:600; font-size:13px; }
    .src { font-size:12px; }
    .leaflet { width:100%; height:380px; border-radius:var(--r-md); border:1px solid var(--c-border); z-index:0; }
    .tiles { width:100%; border-radius:var(--r-md); border:1px solid var(--c-border); display:block; }
    .schematic svg { width:100%; height:auto; background:var(--c-surface-2); border:1px solid var(--c-border); border-radius:var(--r-md); }
    .hint { margin:8px 0 0; font-size:12px; }
    .empty { text-align:center; padding:36px 0; } .empty .emoji { font-size:30px; }
    .legend { display:flex; gap:16px; flex-wrap:wrap; margin-top:12px; }
    .legend span { display:inline-flex; align-items:center; gap:6px; font-size:12px; color:var(--c-text-muted); font-weight:600; }
    .legend i { width:10px; height:10px; border-radius:50%; display:inline-block; }
  `],
})
export class RouteMapComponent implements OnChanges, AfterViewInit, OnDestroy {
  @Input() stops: Stop[] = [];
  @Input() wasteTypeNames: Record<string, string> = {};
  @Input() depot: MapPoi | null = null;
  @Input() end: MapPoi | null = null;
  /** Detailed route line from a routing provider; overrides the straight marker line. */
  @Input() geometry: GeoPoint[] | null = null;
  /** Bare mode: no card chrome — the map fills its container (map-first screens). */
  @Input() bare = false;
  /** Driver mode: navigation marker hierarchy — done stops fade, upcoming shrink,
   * the selected (next) stop and the GPS puck dominate. Dispatcher keeps planning markers. */
  @Input() driverMode = false;
  /** Extra bottom padding for fitBounds so floating sheets don't cover markers. */
  @Input() fitPaddingBottom = 0;
  /** Highlighted stop (larger marker); set by list/marker selection. */
  @Input() selectedStopId: string | null = null;
  /** Driver's live GPS position — rendered as the Signal-green puck. */
  @Input() currentPosition: GeoPoint | null = null;
  /** Increment to recenter the map on the current position ("Standort zentrieren"). */
  @Input() recenterTick = 0;
  /** Follow mode: keep the camera on the moving position; paused on user pan. */
  @Input() followPosition = false;
  @Output() stopSelected = new EventEmitter<string>();
  /** Fired when the user drags the map manually — the screen pauses follow mode. */
  @Output() userMoved = new EventEmitter<void>();

  readonly W = 640;
  readonly H = 360;
  provider = createMapProvider(MAP_CONFIG);
  sl: Record<string, string> = stopStatusLabel;
  imgFailed = signal(false);
  leafletFailed = signal(false);
  legend = [
    { tone: 'open', label: 'Offen' }, { tone: 'progress', label: 'In Arbeit' },
    { tone: 'done', label: 'Erledigt' }, { tone: 'skip', label: 'Übersprungen' }, { tone: 'problem', label: 'Problem' },
  ];

  private markers = signal<MapMarker[]>([]);
  plotted = signal<PlottedStop[]>([]);
  private map?: L.Map;
  private layer?: L.LayerGroup;
  private puck?: L.Marker;

  @ViewChild('mapEl') set mapEl(el: ElementRef<HTMLDivElement> | undefined) {
    if (el && !this.map) this.initMap(el.nativeElement);
  }

  ngOnChanges(changes: SimpleChanges) {
    // GPS ticks / recenter must not re-render layers or re-fit bounds.
    const keys = Object.keys(changes);
    const positionOnly = keys.every(k => k === 'currentPosition' || k === 'recenterTick' || k === 'followPosition');
    if (positionOnly && this.map) {
      if (changes['currentPosition']) {
        this.updatePuck();
        if (this.followPosition && this.currentPosition) {
          this.map.panTo([this.currentPosition.latitude, this.currentPosition.longitude],
            { animate: !RouteMapComponent.reducedMotion() });
        }
      }
      if (changes['recenterTick'] && !changes['recenterTick'].firstChange) this.recenter();
      return;
    }

    this.imgFailed.set(false);
    const withCoords = this.stops.filter(s => s.latitude != null && s.longitude != null);
    this.markers.set(withCoords.map(s => ({
      lat: s.latitude!, lng: s.longitude!, label: String(s.sequenceNumber), tone: statusTone(s.status),
    })));
    this.plotted.set(this.project(withCoords));
    if (this.map) { this.renderLayers(); this.updatePuck(); }
  }

  /** Signal-green location puck (halo pulse disabled by reduced-motion globally). */
  private updatePuck() {
    if (!this.map) return;
    const p = this.currentPosition;
    if (!p) { this.puck?.remove(); this.puck = undefined; return; }
    const latlng: L.LatLngTuple = [p.latitude, p.longitude];
    if (this.puck) { this.puck.setLatLng(latlng); return; }
    const icon = L.divIcon({
      className: '', iconSize: [22, 22], iconAnchor: [11, 11],
      html: `<div class="ap-puck"><span class="ap-puck-halo"></span></div>`,
    });
    this.puck = L.marker(latlng, { icon, zIndexOffset: 900, interactive: false }).addTo(this.map);
  }

  private recenter() {
    const p = this.currentPosition;
    if (!this.map || !p) return;
    this.map.setView([p.latitude, p.longitude], Math.max(this.map.getZoom(), 15));
  }

  ngAfterViewInit() { /* map initialised lazily via the ViewChild setter */ }
  ngOnDestroy() { this.map?.remove(); }

  private static reducedMotion(): boolean {
    return typeof matchMedia !== 'undefined' && matchMedia('(prefers-reduced-motion: reduce)').matches;
  }

  // ---- rendering-mode decisions ----
  useInteractive = computed(() => this.provider.interactive && !this.leafletFailed() && this.plotted().length > 0);
  useSchematic = computed(() =>
    this.plotted().length > 0 &&
    ((this.provider.interactive && this.leafletFailed()) ||
     (!this.provider.interactive && (!this.staticUrl() || this.imgFailed()))));
  providerLabel = computed(() =>
    this.leafletFailed() ? 'Schema' : (this.provider.hasTiles ? this.provider.name : 'Schema'));

  staticUrl = computed(() => this.provider.staticImageUrl(this.markers(), this.W, this.H));

  fullRouteUrl = computed(() => {
    const m = this.markers();
    if (!m.length) return null;
    const dest = m[m.length - 1];
    const waypoints = m.slice(0, -1).map(p => `${p.lat},${p.lng}`).join('|');
    const wp = waypoints ? `&waypoints=${encodeURIComponent(waypoints)}` : '';
    return `https://www.google.com/maps/dir/?api=1&destination=${dest.lat},${dest.lng}${wp}`;
  });

  polyline = computed(() => this.plotted().map(p => `${p.x},${p.y}`).join(' '));
  toneVar(tone: string) { return `var(--c-${tone})`; }

  // ---- Leaflet ----
  private initMap(el: HTMLDivElement) {
    try {
      const spec = this.provider.tileLayer()!;
      this.map = L.map(el, { attributionControl: true, scrollWheelZoom: true }).setView([52.52, 13.405], 12);
      L.tileLayer(spec.urlTemplate, { attribution: spec.attribution, maxZoom: spec.maxZoom }).addTo(this.map);
      this.layer = L.layerGroup().addTo(this.map);
      // Drag is always user-initiated (pan/zoom via code doesn't fire it).
      this.map.on('dragstart', () => this.userMoved.emit());
      this.renderLayers();
      setTimeout(() => this.map?.invalidateSize(), 0);
    } catch {
      this.leafletFailed.set(true); // → schematic fallback
      this.map = undefined;
    }
  }

  private renderLayers() {
    if (!this.map || !this.layer) return;
    this.layer.clearLayers();
    const stops = this.stops.filter(s => s.latitude != null && s.longitude != null);
    const latlngs: L.LatLngTuple[] = [];

    for (const s of stops) {
      const tone = statusTone(s.status);
      const selected = s.id === this.selectedStopId;
      // Upcoming (open) stops stay neutral dark so the ringed current stop dominates.
      const color = tone === 'open' ? '#33463c' : toneColor(tone);
      // Driver mode: navigation hierarchy — smaller upcoming pins, faded finished ones.
      const finished = tone === 'done' || tone === 'skip';
      const size = selected ? 38 : this.driverMode ? 22 : 28;
      const opacity = this.driverMode && finished && !selected ? 0.55 : 1;
      const marker = L.marker([s.latitude!, s.longitude!],
        { icon: this.pinIcon(String(s.sequenceNumber), color, selected, size, opacity), zIndexOffset: selected ? 500 : 0 });
      marker.bindPopup(this.popupHtml(s));
      marker.on('click', () => this.stopSelected.emit(s.id));
      marker.addTo(this.layer);
      latlngs.push([s.latitude!, s.longitude!]);
    }
    if (this.geometry && this.geometry.length > 1) {
      // Real provider geometry → solid route line.
      L.polyline(this.geometry.map(p => [p.latitude, p.longitude] as L.LatLngTuple),
        { color: '#15803d', weight: 4, opacity: 0.75 }).addTo(this.layer);
    } else if (latlngs.length > 1) {
      // No geometry → dashed straight line between stops.
      L.polyline(latlngs, { color: '#15803d', weight: 3, opacity: 0.7, dashArray: '6 6' }).addTo(this.layer);
    }
    if (this.depot) this.poiMarker(this.depot, '🏭', 'Start / Betriebshof');
    if (this.end && (this.end.lat !== this.depot?.lat || this.end.lng !== this.depot?.lng)) this.poiMarker(this.end, '🏁', 'Ziel');

    const bounds = L.latLngBounds([
      ...latlngs,
      ...(this.depot ? [[this.depot.lat, this.depot.lng] as L.LatLngTuple] : []),
      ...(this.end ? [[this.end.lat, this.end.lng] as L.LatLngTuple] : []),
    ]);
    if (bounds.isValid()) this.map.fitBounds(bounds, {
      paddingTopLeft: [30, 70],                                  // floating status strip
      paddingBottomRight: [30, 30 + this.fitPaddingBottom],      // bottom sheet awareness
      maxZoom: 15,
    });
  }

  private poiMarker(p: MapPoi, emoji: string, kind: string) {
    const icon = L.divIcon({
      className: '', iconSize: [30, 30], iconAnchor: [15, 15],
      html: `<div style="font-size:22px;line-height:30px;text-align:center;filter:drop-shadow(0 1px 2px rgba(0,0,0,.4))">${emoji}</div>`,
    });
    L.marker([p.lat, p.lng], { icon }).bindPopup(`<strong>${kind}</strong><br>${this.esc(p.name)}`).addTo(this.layer!);
  }

  private pinIcon(label: string, color: string, selected = false, size?: number, opacity = 1): L.DivIcon {
    const px = size ?? (selected ? 38 : 28);
    const font = selected ? 15 : px <= 22 ? 10.5 : 12;
    const ring = selected ? `0 0 0 4px ${color}44, 0 2px 8px rgba(0,0,0,.45)` : '0 1px 4px rgba(0,0,0,.4)';
    const html =
      `<div style="width:${px}px;height:${px}px;border-radius:50%;background:${color};color:#fff;border:2px solid #fff;` +
      `box-shadow:${ring};opacity:${opacity};display:flex;align-items:center;justify-content:center;` +
      `font:700 ${font}px/1 system-ui,sans-serif">${label}</div>`;
    return L.divIcon({ className: '', html, iconSize: [px, px], iconAnchor: [px / 2, px / 2], popupAnchor: [0, -px / 2] });
  }

  private popupHtml(s: Stop): string {
    const waste = this.wasteTypeNames[s.wasteTypeId] ?? '—';
    const rows = [
      ['Stopp', `Nr. ${s.sequenceNumber}`],
      ['Adresse', this.esc(s.address)],
      ['Abfallart', this.esc(waste)],
      ['Abholart', pickupTypeLabel[s.pickupType] ?? s.pickupType],
      ['Status', stopStatusLabel[s.status] ?? s.status],
    ].map(([k, v]) => `<div style="display:flex;gap:8px;justify-content:space-between"><span style="color:#667085">${k}</span><strong>${v}</strong></div>`).join('');
    const nav = navigationUrl(s.latitude, s.longitude, s.address);
    return `<div style="min-width:200px;font:13px system-ui,sans-serif">${rows}` +
      `<a href="${nav}" target="_blank" rel="noopener" style="display:inline-block;margin-top:8px;color:#15803d;font-weight:700">🧭 Navigieren</a></div>`;
  }

  private esc(v: string): string {
    return v.replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]!));
  }

  /** Normalise lat/lng into the SVG viewBox with padding; handles no-spread. */
  private project(stops: Stop[]): PlottedStop[] {
    if (!stops.length) return [];
    const lats = stops.map(s => s.latitude!), lngs = stops.map(s => s.longitude!);
    const minLat = Math.min(...lats), maxLat = Math.max(...lats);
    const minLng = Math.min(...lngs), maxLng = Math.max(...lngs);
    const pad = 40;
    const spanLat = maxLat - minLat || 1, spanLng = maxLng - minLng || 1;
    return stops.map(s => ({
      x: pad + ((s.longitude! - minLng) / spanLng) * (this.W - 2 * pad),
      y: pad + (1 - (s.latitude! - minLat) / spanLat) * (this.H - 2 * pad),
      label: String(s.sequenceNumber),
      tone: statusTone(s.status),
      status: s.status,
      address: s.address,
    }));
  }
}
