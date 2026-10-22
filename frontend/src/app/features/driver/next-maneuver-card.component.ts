import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NavigationStep } from '../../core/models';

const MANEUVER_ICON: Record<string, string> = {
  turn_left: '⬅', turn_right: '➡', turn_slight_left: '↖', turn_slight_right: '↗',
  turn_sharp_left: '⬅', turn_sharp_right: '➡', continue: '⬆', arrive: '⚑', roundabout: '↻', unknown: '⬆',
};

/**
 * Next-maneuver card. With a real provider step it shows
 * "In 180 m · Rechts abbiegen · auf Hauptstraße"; without steps it shows the
 * HONEST fallback ("Weiter zum nächsten Stopp / Keine Abbiegehinweise
 * verfügbar") in the same layout — never an invented turn instruction.
 */
@Component({
  selector: 'ap-next-maneuver-card',
  standalone: true,
  imports: [CommonModule],
  template: `
  <div class="mv" role="status">
    <div class="mv-icon" aria-hidden="true">{{ icon() }}</div>
    <div class="mv-body">
      <small class="mv-label">Nächster Hinweis</small>
      <ng-container *ngIf="step; else fallback">
        <strong class="mv-main"><span class="ap-id mv-dist" *ngIf="step.distanceMeters > 0">In {{ formatDistance(step.distanceMeters) }}</span>
          {{ step.instruction }}</strong>
        <small class="mv-road ap-subtle" *ngIf="step.roadName">auf {{ step.roadName }}</small>
      </ng-container>
      <ng-template #fallback>
        <!-- Hierarchy: 1) distance/action  2) stop  3) provider limitation -->
        <strong class="mv-main">
          <ng-container *ngIf="distanceMeters !== null; else noDist">
            <span class="ap-id mv-dist">Noch {{ formatDistance(distanceMeters!) }}</span> bis zum Stopp
          </ng-container>
          <ng-template #noDist>Weiter zum nächsten Stopp</ng-template>
        </strong>
        <small class="mv-to" *ngIf="fallbackTo">Weiter zu {{ fallbackTo }}<ng-container *ngIf="fallbackMeta"> · {{ fallbackMeta }}</ng-container></small>
        <small class="mv-road ap-subtle">Abbiegehinweise vom Routing-Anbieter nicht verfügbar</small>
      </ng-template>
    </div>
  </div>`,
  styles: [`
    /* Dark navigation card — high contrast, readable while driving. */
    .mv { display:flex; gap:14px; align-items:center; padding:14px 18px;
      background:rgba(14,26,20,.95); color:#fff; border-radius:16px;
      box-shadow:var(--sh-lg); backdrop-filter:blur(6px); }
    .mv-icon { flex:0 0 54px; width:54px; height:54px; border-radius:14px; display:grid; place-items:center;
      background:rgba(255,255,255,.1); color:#fff; font-size:28px; }
    .mv-body { min-width:0; }
    .mv-label { display:block; font-size:12.5px; font-weight:750; color:var(--c-primary); }
    .mv-main { display:block; font-size:19px; font-weight:750; line-height:1.2; color:#fff; }
    .mv-dist { color:var(--c-primary); margin-right:6px; }
    .mv-road { display:block; font-size:12.5px; }
    .mv .ap-subtle { color:rgba(255,255,255,.6); }
    .mv-to { display:block; font-size:14px; font-weight:650; margin-top:2px; color:rgba(255,255,255,.88); }
  `],
})
export class NextManeuverCardComponent {
  @Input() step: NavigationStep | null = null;
  /** Fallback guidance: next-stop address („Weiter zu Marktplatz 3"). */
  @Input() fallbackTo: string | null = null;
  /** Fallback meta line, e.g. „Stopp 2 · Regelabholung". */
  @Input() fallbackMeta: string | null = null;
  /** Live distance to the next stop (meters), if GPS provides one. */
  @Input() distanceMeters: number | null = null;

  icon(): string {
    if (!this.step) return MANEUVER_ICON['continue'];
    const key = this.step.modifier ? `${this.step.maneuverType}_${this.step.modifier}` : this.step.maneuverType;
    return MANEUVER_ICON[key] ?? MANEUVER_ICON[this.step.maneuverType] ?? MANEUVER_ICON['unknown'];
  }

  formatDistance(m: number): string {
    return m < 1000 ? `${Math.round(m)} m` : `${(m / 1000).toFixed(1).replace('.', ',')} km`;
  }
}
