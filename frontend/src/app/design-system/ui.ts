import { Component, EventEmitter, Input, OnDestroy, Output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { statusTone } from '../core/labels';
import { IconComponent } from './icons';
import { ToastService } from './toast.service';

/** Status badge — colored pill driven by a status tone (open/progress/done/skip/problem). */
@Component({
  selector: 'ap-badge',
  standalone: true,
  imports: [CommonModule],
  template: `<span class="badge" [attr.data-tone]="tone">{{ label }}</span>`,
  styles: [`
    .badge { display:inline-flex; align-items:center; gap:6px; padding:3px 10px; border-radius:999px;
      font-size:12px; font-weight:600; line-height:1.6; white-space:nowrap;
      transition:background var(--t-med), color var(--t-med); }
    .badge::before { content:''; width:6px; height:6px; border-radius:50%; background:currentColor; opacity:.9; }
    [data-tone=open]{ color:var(--c-open); background:var(--c-open-soft); }
    /* Live state = the ONLY filled badge: weight encodes "moving right now";
       every settled state (open/done/skip) recedes as a tint. Dot pulses in
       sync with the Leitstand live-section header. */
    [data-tone=progress]{ color:#fff; background:var(--c-progress); }
    [data-tone=progress]::before { animation:badge-pulse 2s ease-in-out infinite; }
    @keyframes badge-pulse { 0%,100% { opacity:1; } 50% { opacity:.35; } }
    @media (prefers-reduced-motion: reduce) { [data-tone=progress]::before { animation:none; } }
    [data-tone=done]{ color:var(--c-done); background:var(--c-done-soft); }
    [data-tone=skip]{ color:var(--c-skip); background:var(--c-skip-soft); }
    [data-tone=problem]{ color:var(--c-problem); background:var(--c-problem-soft); }
    [data-tone=info]{ color:var(--c-info); background:var(--c-info-soft); }
  `],
})
export class BadgeComponent {
  @Input() label = '';
  @Input() set status(v: string) { this.tone = statusTone(v); }
  @Input() set toneName(v: string) { this.tone = v; }
  tone = 'open';
}

/** Waste-fraction chip with a fraction-specific accent dot. */
@Component({
  selector: 'ap-waste-chip',
  standalone: true,
  imports: [CommonModule],
  template: `<span class="chip"><i [style.background]="color"></i>{{ label }}</span>`,
  styles: [`
    .chip { display:inline-flex; align-items:center; gap:7px; padding:3px 10px; border-radius:999px;
      font-size:12px; font-weight:600; background:var(--c-surface-2); border:1px solid var(--c-border); }
    i { width:8px; height:8px; border-radius:2px; display:inline-block; }
  `],
})
export class WasteChipComponent {
  @Input() label = '';
  @Input() color = 'var(--w-hausmuell)';
}

/** KPI / stat cell — operational console metric, not a marketing tile.
 *  Icon chip leads, value+label stack tight beside it: reads as one dense
 *  instrument row instead of the big-number-small-label SaaS template.
 *  `flat` drops the card chrome so several cells can share ONE console card
 *  (segmented by dividers) — see the Leitstand command strip. */
@Component({
  selector: 'ap-stat',
  standalone: true,
  imports: [CommonModule, IconComponent],
  template: `
    <div class="stat" [class.ap-card]="!flat" [class.flat]="flat" [attr.data-tone]="tone || null">
      <span class="chip" aria-hidden="true">
        <ap-icon *ngIf="iconName" [name]="iconName" [size]="18"></ap-icon>
        <span *ngIf="!iconName && icon">{{ icon }}</span>
      </span>
      <div class="col">
        <div class="value">{{ value }}</div>
        <div class="label">{{ label }}</div>
        <div class="hint ap-subtle" *ngIf="hint">{{ hint }}</div>
      </div>
    </div>`,
  styles: [`
    .stat { padding:16px 20px; display:flex; align-items:center; gap:14px; position:relative;
      transition:border-color var(--t-med), background var(--t-med); }
    .chip { width:38px; height:38px; flex:0 0 38px; border-radius:11px; display:grid; place-items:center;
      background:var(--c-surface-2); border:1px solid var(--c-border); color:var(--c-text-muted); }
    .col { min-width:0; }
    .value { font-size:26px; font-weight:750; letter-spacing:-.02em; font-variant-numeric:tabular-nums;
      line-height:1.15; transition:color var(--t-med); }
    .label { font-size:13px; font-weight:600; color:var(--c-text-muted); line-height:1.35; }
    .hint { font-size:12px; line-height:1.35; }
    [data-tone=progress] .chip { background:var(--c-progress-soft); border-color:color-mix(in srgb, var(--c-progress) 25%, var(--c-border)); color:var(--c-progress); }
    [data-tone=progress] .value { color:var(--c-primary-strong); }
    [data-tone=done] .chip { background:var(--c-done-soft); color:var(--c-done); }
    [data-tone=done] .value { color:var(--c-done); }
    [data-tone=problem] .chip { background:var(--c-problem-soft); border-color:color-mix(in srgb, var(--c-problem) 30%, var(--c-border)); color:var(--c-problem); }
    [data-tone=problem] .value { color:var(--c-problem); }
    [data-tone=problem].flat { background:color-mix(in srgb, var(--c-problem-soft) 45%, transparent); }
    [data-tone=problem]:not(.flat) { border-color:color-mix(in srgb, var(--c-problem) 30%, var(--c-border)); }
  `],
})
export class StatComponent {
  @Input() label = '';
  @Input() value: string | number = '';
  @Input() hint = '';
  /** Legacy emoji/text glyph — prefer iconName (ap-icon set). */
  @Input() icon = '';
  @Input() iconName = '';
  @Input() tone = '';
  /** Chrome-less cell for use inside a shared console card. */
  @Input() flat = false;
}

/** Shimmer skeleton — perceived-speed placeholder for loading tables/cards. */
@Component({
  selector: 'ap-skeleton',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="sk-wrap ap-card" role="status" aria-label="Wird geladen">
      <div class="sk-row" *ngFor="let r of rowsArray">
        <span class="sk sk-dot"></span>
        <span class="sk sk-line" [style.width.%]="60 + (r % 3) * 12"></span>
        <span class="sk sk-pill"></span>
      </div>
    </div>`,
  styles: [`
    .sk-wrap { padding:8px 16px; }
    .sk-row { display:flex; align-items:center; gap:14px; padding:13px 0; border-bottom:1px solid var(--c-border); }
    .sk-row:last-child { border-bottom:none; }
    .sk { display:inline-block; border-radius:6px;
      background:linear-gradient(90deg, var(--c-surface-2) 25%, var(--c-border) 50%, var(--c-surface-2) 75%);
      background-size:200% 100%; animation:ap-shimmer 1.4s linear infinite; }
    .sk-dot { width:28px; height:28px; border-radius:9px; flex:0 0 28px; }
    .sk-line { height:12px; }
    .sk-pill { width:72px; height:20px; border-radius:999px; margin-left:auto; flex:0 0 72px; }
  `],
})
export class SkeletonComponent {
  @Input() set rows(v: number) { this.rowsArray = Array.from({ length: v }, (_, i) => i); }
  rowsArray = Array.from({ length: 4 }, (_, i) => i);
}

/** Empty / loading state placeholder. */
@Component({
  selector: 'ap-state',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="state ap-card">
      <div class="spinner ap-keep-motion" *ngIf="loading"></div>
      <div class="emoji" *ngIf="!loading">{{ icon || '📭' }}</div>
      <div class="title">{{ loading ? 'Wird geladen…' : title }}</div>
      <div class="msg ap-muted" *ngIf="!loading && message">{{ message }}</div>
    </div>`,
  styles: [`
    .state { padding:48px 24px; display:flex; flex-direction:column; align-items:center; gap:10px; text-align:center; }
    .emoji { font-size:34px; }
    .title { font-weight:600; }
    .msg { font-size:13px; max-width:320px; }
    .spinner { width:28px; height:28px; border-radius:50%; border:3px solid var(--c-border);
      border-top-color:var(--c-primary); animation:spin .7s linear infinite; }
    @keyframes spin { to { transform:rotate(360deg); } }
  `],
})
export class StateComponent {
  @Input() loading = false;
  @Input() title = 'Keine Daten';
  @Input() message = '';
  @Input() icon = '';
}

/**
 * Auto-dismissing success flash. Owns its whole lifecycle: fade-up enter,
 * timed fade-out exit (elements must never vanish without transition), then
 * `(done)` so the caller can clear its signal. Interruptible: a new message
 * restarts cleanly.
 *
 * When to reach for this vs `ap-toast-stack`: `ap-flash` is an *inline*,
 * single-message primitive meant to sit anchored directly under a specific
 * form/action (e.g. inside `ap-confirm`/`ap-modal` for a delete-flow error,
 * or under a save button) — content around it reflows to make room. Use
 * `ap-toast-stack` for anything that should surface globally regardless of
 * which route/dialog is open (list mutations, background sync results,
 * queued messages).
 */
@Component({
  selector: 'ap-flash',
  standalone: true,
  imports: [CommonModule],
  template: `<div class="flash" *ngIf="text()" [class.leaving]="leaving()" [attr.data-tone]="tone" role="status">{{ tone === 'problem' ? '⚠' : '✔' }} {{ text() }}</div>`,
  styles: [`
    .flash { background:var(--c-done-soft); color:var(--c-done); padding:10px 14px; border-radius:10px;
      font-weight:600; font-size:13px; margin-bottom:14px;
      animation:ap-fade-up var(--t-enter) both;
      transition:opacity var(--t-med), transform var(--t-med); }
    .flash[data-tone=problem] { background:var(--c-problem-soft); color:var(--c-problem); }
    .flash.leaving { opacity:0; transform:translateY(-4px); }
  `],
})
export class FlashComponent implements OnDestroy {
  text = signal('');
  leaving = signal(false);
  /** Visible time before the exit transition starts (ms). */
  @Input() duration = 3000;
  @Input() tone: 'done' | 'problem' = 'done';
  @Input() set message(v: string | null | undefined) {
    this.clearTimers();
    this.leaving.set(false);
    if (!v) { this.text.set(''); return; }
    this.text.set(v);
    this.timers.push(setTimeout(() => this.leaving.set(true), this.duration));
    this.timers.push(setTimeout(() => { this.text.set(''); this.done.emit(); }, this.duration + 200));
  }
  @Output() done = new EventEmitter<void>();
  private timers: ReturnType<typeof setTimeout>[] = [];
  private clearTimers() { this.timers.forEach(clearTimeout); this.timers = []; }
  ngOnDestroy() { this.clearTimers(); }
}

/**
 * Global stacked toast queue — reads `ToastService` and renders every
 * concurrent message as its own dismissible item, newest at the bottom.
 * Mounted once in `ShellComponent` (outside the routed content) so toasts
 * from any feature route through one queue instead of clobbering each other.
 * Visually a natural extension of `ap-flash` (same tone colors/glyphs), but
 * stacks instead of replacing.
 */
@Component({
  selector: 'ap-toast-stack',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="toast-stack" *ngIf="toastSvc.toasts().length">
      <div class="toast" *ngFor="let t of toastSvc.toasts()" [attr.data-tone]="t.tone"
           [class.leaving]="t.leaving"
           [attr.role]="t.tone === 'problem' ? 'alert' : 'status'">
        <span class="ic" aria-hidden="true">{{ t.tone === 'problem' ? '⚠' : '✔' }}</span>
        <span class="txt">{{ t.text }}</span>
        <button class="close" (click)="toastSvc.dismiss(t.id)" aria-label="Meldung schließen">✕</button>
      </div>
    </div>`,
  styles: [`
    .toast-stack { position:fixed; right:20px; bottom:20px; z-index:var(--z-toast);
      display:flex; flex-direction:column; gap:8px; max-width:min(360px, calc(100vw - 40px)); }
    .toast { display:flex; align-items:center; gap:10px; background:var(--c-done-soft); color:var(--c-done);
      padding:10px 10px 10px 14px; border-radius:10px; font-weight:600; font-size:13px;
      box-shadow:var(--sh-lg); animation:ap-fade-up var(--t-enter) both;
      transition:opacity var(--t-med), transform var(--t-med); }
    .toast[data-tone=problem] { background:var(--c-problem-soft); color:var(--c-problem); }
    .toast[data-tone=info] { background:var(--c-info-soft); color:var(--c-info); }
    .toast.leaving { opacity:0; transform:translateY(4px); }
    .txt { flex:1; min-width:0; }
    .close { flex:0 0 auto; border:none; background:transparent; cursor:pointer; color:inherit;
      opacity:.65; width:24px; height:24px; border-radius:7px; display:inline-grid; place-items:center;
      font-size:12px; line-height:1; transition:opacity var(--t-fast), background var(--t-fast); }
    .close:hover, .close:focus-visible { opacity:1; background:rgba(0,0,0,.06); }
    @media (max-width:560px) {
      .toast-stack { right:12px; left:12px; bottom:12px; max-width:none; }
    }
    @media (prefers-reduced-motion: reduce) {
      .toast { animation:none; }
    }
  `],
})
export class ToastStackComponent {
  constructor(public toastSvc: ToastService) {}
}

/**
 * Segmented stop progress — one segment per stop, colored by its status.
 * Glanceable tour state for the driver cockpit and dispatch surfaces
 * (a thin percent bar hides WHERE the problems are; segments show it).
 */
@Component({
  selector: 'ap-stop-progress',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="segs" role="progressbar" [attr.aria-valuenow]="doneCount"
         [attr.aria-valuemax]="stops.length"
         [attr.aria-label]="doneCount + ' von ' + stops.length + ' Stopps erledigt'">
      <i *ngFor="let s of stops" [attr.data-tone]="tone(s.status)"></i>
    </div>`,
  styles: [`
    .segs { display:flex; gap:3px; }
    i { flex:1 1 0; height:6px; border-radius:99px; background:var(--c-border-strong); min-width:6px;
      transition:background var(--t-med); }
    i[data-tone=done] { background:var(--c-done); }
    i[data-tone=problem] { background:var(--c-problem); }
    i[data-tone=skip] { background:var(--c-skip); }
    i[data-tone=progress] { background:var(--c-progress); animation:seg-pulse 2s ease-in-out infinite; }
    @keyframes seg-pulse { 0%,100% { opacity:1; } 50% { opacity:.45; } }
  `],
})
export class StopProgressComponent {
  @Input() stops: { status: string }[] = [];
  get doneCount() { return this.stops.filter(s => s.status === 'Done').length; }
  tone(s: string) { return ({ Done: 'done', Problem: 'problem', InProgress: 'progress', Skipped: 'skip' } as Record<string, string>)[s] ?? 'open'; }
}

/** Page header with title, optional subtitle and action slot. */
@Component({
  selector: 'ap-page-header',
  standalone: true,
  imports: [CommonModule],
  template: `
    <header class="ph">
      <div><h1>{{ title }}</h1><p class="ap-muted" *ngIf="subtitle">{{ subtitle }}</p></div>
      <div class="actions"><ng-content></ng-content></div>
    </header>`,
  styles: [`
    .ph { display:flex; align-items:flex-start; justify-content:space-between; gap:16px; margin-bottom:22px; flex-wrap:wrap; }
    h1 { font-size:24px; }
    p { margin:4px 0 0; font-size:14px; }
    .actions { display:flex; gap:10px; }
  `],
})
export class PageHeaderComponent {
  @Input() title = '';
  @Input() subtitle = '';
}
