import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

export interface SummaryItem { value: string | number; label: string; tone?: string; }

/**
 * "Betriebsleiste" — the console signature: a slim strip of dense metric chips
 * (mono tabular numerals + label) that opens every admin page. Compact
 * enterprise metrics, deliberately NOT big dashboard KPI tiles.
 * `tone` tints the value + left edge (done/progress/problem/skip/info).
 */
@Component({
  selector: 'ap-summary-row',
  standalone: true,
  imports: [CommonModule],
  template: `
  <div class="row" role="list">
    <div class="item" role="listitem" *ngFor="let it of items" [attr.data-tone]="it.tone || null">
      <i class="dot" aria-hidden="true"></i>
      <strong class="ap-id">{{ it.value }}</strong><span>{{ it.label }}</span>
    </div>
  </div>`,
  styles: [`
    /* Quiet meta strip: hairline-separated readings, not a row of boxes.
       The tone dot replaces the old left color stripe. */
    .row { display:flex; flex-wrap:wrap; row-gap:8px; margin-bottom:18px; padding:2px 2px; }
    .item { display:inline-flex; align-items:baseline; gap:8px; }
    .item + .item { border-left:1px solid var(--c-border); margin-left:18px; padding-left:18px; }
    .dot { width:7px; height:7px; border-radius:50%; align-self:center; background:var(--c-border-strong); }
    .item strong { font-size:16px; font-weight:750; font-variant-numeric:tabular-nums; line-height:1; }
    .item span { font-size:12.5px; color:var(--c-text-muted); font-weight:600; white-space:nowrap; }
    [data-tone=done] .dot { background:var(--c-done); } [data-tone=done] strong { color:var(--c-done); }
    [data-tone=progress] .dot { background:var(--c-progress); } [data-tone=progress] strong { color:var(--c-primary-strong); }
    [data-tone=problem] .dot { background:var(--c-problem); } [data-tone=problem] strong { color:var(--c-problem); }
    [data-tone=skip] .dot { background:var(--c-skip); } [data-tone=skip] strong { color:var(--c-skip); }
    [data-tone=info] .dot { background:var(--c-info, #1e5b96); } [data-tone=info] strong { color:var(--c-info, #1e5b96); }
  `],
})
export class SummaryRowComponent {
  @Input() items: SummaryItem[] = [];
}

/** Initials avatar — Tanne square, used in user/driver tables. */
@Component({
  selector: 'ap-avatar',
  standalone: true,
  template: `<span class="av" aria-hidden="true">{{ initials }}</span>`,
  styles: [`
    .av { width:36px; height:36px; border-radius:10px; background:var(--c-shell); color:#fff;
      display:inline-grid; place-items:center; font-size:13px; font-weight:700; flex:0 0 36px; }
  `],
})
export class AvatarComponent {
  @Input() set name(v: string | null | undefined) {
    this.initials = (v ?? '').split(' ').map(x => x[0]).filter(Boolean).slice(0, 2).join('').toUpperCase() || '·';
  }
  initials = '·';
}
