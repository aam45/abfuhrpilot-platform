import { Component, computed, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { wasteColorVar } from '../../core/labels';

export interface FractionSlice {
  name: string;
  count: number;
  color: string;
}

interface DonutArc {
  color: string;
  dasharray: string;
  dashoffset: string;
}

/** Circumference-100 donut radius (2·π·r = 100 → percentage math maps 1:1 to dash units). */
const R = 15.9155;

/** Synthetic slice name for routes without a waste type. */
const NO_FRACTION = 'Ohne Abfallart';

/**
 * Neutral color for the synthetic "Ohne Abfallart" slice. Deliberately NOT
 * `wasteColorVar()` — its safe fallback is Hausmüll's color, and an unnamed
 * fraction must not visually impersonate Hausmüll in the donut. Component-local
 * decision only; the table dots' existing fallback parity stays untouched.
 */
const NO_FRACTION_COLOR = 'var(--c-border-strong)';

/**
 * Waste-fraction distribution donut — Leitstand instrument built from real
 * tour data.
 *
 * Dumb presentational component: groups today's routes by `wasteTypeName` and
 * renders an SVG stroke-dasharray donut plus a hairline legend with the explicit
 * route count per fraction (counts, never percentages — counts are the honest
 * unit). Colors come from `wasteColorVar()`, the same mapping the table's
 * fraction dots and rails use — except the synthetic "Ohne Abfallart" slice,
 * which is deliberately neutral (see `NO_FRACTION_COLOR`).
 *
 * DATA HONESTY: the source list (`OperationsDashboardDto.TodaysRoutes`) is
 * capped at 50 routes/day by the backend (`DashboardService.GetOperationsAsync`
 * uses `PageSize = 50`), so this donut reflects "today's routes shown", not a
 * guaranteed total for very large fleets.
 *
 * Empty input renders nothing at all — no fake gray donut; the dashboard table
 * already carries the honest "Keine Touren für heute" empty state.
 */
@Component({
  selector: 'ap-waste-fraction-donut',
  standalone: true,
  imports: [CommonModule],
  template: `
  <section *ngIf="total() > 0" class="ap-card fd">
    <div class="fd-head">
      <h3>Fraktionen heute</h3>
      <span class="ap-subtle fd-total"><span class="ap-id">{{ total() }}</span> Tour{{ total() === 1 ? '' : 'en' }}</span>
    </div>
    <div class="fd-body">
      <!-- Decorative summary of the legend — the legend list carries the accessible content. -->
      <svg class="donut" viewBox="0 0 42 42" aria-hidden="true" focusable="false">
        <circle *ngFor="let a of arcs()" cx="21" cy="21" [attr.r]="r" fill="none"
                stroke-linecap="butt" stroke-width="5"
                [attr.stroke]="a.color"
                [attr.stroke-dasharray]="a.dasharray"
                [attr.stroke-dashoffset]="a.dashoffset"/>
      </svg>
      <ul class="legend" aria-label="Touren je Abfallart">
        <li *ngFor="let s of slices()">
          <i class="dot" [style.background]="s.color" aria-hidden="true"></i>
          <span class="name">{{ s.name }}</span>
          <span class="count"><span class="ap-id">{{ s.count }}</span> Tour{{ s.count === 1 ? '' : 'en' }}</span>
        </li>
      </ul>
    </div>
  </section>`,
  styles: [`
    .fd { padding:16px 22px 18px; }
    .fd-head { display:flex; align-items:baseline; justify-content:space-between; gap:12px; margin-bottom:10px; }
    .fd-head h3 { font-size:15px; }
    .fd-total { font-size:12.5px; }
    .fd-total .ap-id { color:var(--c-text-muted); }
    .fd-body { display:flex; align-items:center; gap:26px; flex-wrap:wrap; }
    .donut { width:116px; height:116px; flex:0 0 116px; }
    /* Legend: hairline meta rows, de-boxed — no chips, no backgrounds. */
    .legend { list-style:none; margin:0; padding:0; flex:1 1 220px; min-width:200px;
      columns:2; column-gap:32px; }
    .legend li { display:flex; align-items:center; gap:9px; padding:6px 0; font-size:13px;
      border-bottom:1px solid var(--c-border); break-inside:avoid; }
    .dot { width:9px; height:9px; border-radius:3px; flex:none; }
    .name { font-weight:600; min-width:0; overflow:hidden; text-overflow:ellipsis; white-space:nowrap; }
    .count { margin-left:auto; color:var(--c-text-muted); font-size:12.5px; white-space:nowrap; }
    .count .ap-id { color:var(--c-text); }
    @media (max-width:640px) { .legend { columns:1; } }
  `],
})
export class WasteFractionDonutComponent {
  /** Today's routes as shown on the dashboard (backend-capped at 50/day — see class doc). */
  routes = input.required<ReadonlyArray<{ wasteTypeName?: string | null }>>();

  readonly r = R;

  total = computed(() => this.routes().length);

  /** Routes grouped by fraction name, largest first (stable name tiebreak for calm scanning). */
  slices = computed<FractionSlice[]>(() => {
    const counts = new Map<string, number>();
    for (const route of this.routes()) {
      const name = route.wasteTypeName?.trim() || NO_FRACTION;
      counts.set(name, (counts.get(name) ?? 0) + 1);
    }
    return [...counts.entries()]
      .map(([name, count]) => ({
        name,
        count,
        color: name === NO_FRACTION ? NO_FRACTION_COLOR : wasteColorVar(name),
      }))
      .sort((a, b) => b.count - a.count || a.name.localeCompare(b.name, 'de'));
  });

  /** stroke-dasharray arcs on a circumference-100 circle; 1-unit hairline gaps between slices. */
  arcs = computed<DonutArc[]>(() => {
    const slices = this.slices();
    const total = this.total();
    if (!total) return [];
    const gap = slices.length > 1 ? 1 : 0;
    let start = 0;
    return slices.map(s => {
      const pct = (s.count / total) * 100;
      const len = Math.max(pct - gap, 0.5);
      // Offset 25 puts the dash start at 12 o'clock; gap/2 centers each hairline gap.
      const offset = 25 - start - gap / 2;
      start += pct;
      return {
        color: s.color,
        dasharray: `${len.toFixed(3)} ${(100 - len).toFixed(3)}`,
        dashoffset: offset.toFixed(3),
      };
    });
  });
}
