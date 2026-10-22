import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SheetState } from '../core/map/map-ui-state.service';

/**
 * Bottom sheet — the map-first interaction surface. Three positions
 * (collapsed / half / expanded) with a keyboard-operable grip:
 * Enter/Space cycles upward, ArrowUp expands, ArrowDown collapses.
 * The grip bar carries the tour's waste-fraction color (`accent` input) —
 * AbfuhrPilot's signature rail traveling into the map world.
 * Mobile-first (docked full-width); ≥1000px it becomes a floating side panel.
 * Motion uses the global tokens and collapses under prefers-reduced-motion.
 */
@Component({
  selector: 'ap-bottom-sheet',
  standalone: true,
  imports: [CommonModule],
  template: `
  <section class="sheet" [attr.data-state]="state" [style.--accent]="accent"
           (keydown.escape)="onEscape()">
    <button type="button" class="grip" (click)="cycle()" (keydown)="onKey($event)"
            [attr.aria-expanded]="state !== 'collapsed'"
            [attr.aria-label]="state === 'expanded' ? 'Bereich verkleinern' : 'Bereich vergrößern'">
      <span class="grip-bar" aria-hidden="true"></span>
    </button>
    <div class="peek"><ng-content select="[sheet-peek]"></ng-content></div>
    <div class="body" [class.body-open]="state !== 'collapsed'">
      <ng-content></ng-content>
    </div>
    <div class="more" *ngIf="state === 'expanded'">
      <ng-content select="[sheet-expanded]"></ng-content>
    </div>
  </section>`,
  styles: [`
    .sheet { position:absolute; left:0; right:0; bottom:0; z-index:20;
      background:var(--c-surface); border-radius:18px 18px 0 0;
      box-shadow:0 -10px 34px rgba(19,26,22,.18);
      display:flex; flex-direction:column; overflow:hidden;
      /* max-height (layout) is a deliberate trade-off: transform-based snapping
         would leave sheet content unreachable below the fold. Content is light,
         duration frequency-tuned (drivers cycle this constantly). */
      transition:max-height var(--t-med);
      padding-bottom:env(safe-area-inset-bottom, 0px);
      max-height:34vh; }
    .sheet[data-state=half] { max-height:58vh; }
    .sheet[data-state=expanded] { max-height:88vh; }

    .grip { border:none; background:transparent; padding:10px 0 6px; cursor:pointer;
      display:flex; justify-content:center; width:100%; flex:0 0 auto; }
    .grip-bar { width:44px; height:5px; border-radius:99px;
      background:var(--accent, var(--c-border-strong)); opacity:.9;
      transition:transform var(--t-fast); }
    .grip:active .grip-bar { transform:scaleX(.82); }
    .grip:focus-visible { outline-offset:-2px; }

    .peek { flex:0 0 auto; padding:0 18px 14px; }
    .body { flex:0 1 auto; overflow-y:auto; padding:0 18px;
      opacity:0; max-height:0; transition:opacity var(--t-med), max-height var(--t-med); }
    .body.body-open { opacity:1; max-height:100%; padding-bottom:14px; }
    .more { flex:1 1 auto; overflow-y:auto; padding:0 18px 18px;
      border-top:1px solid var(--c-border); animation:ap-fade-up var(--t-enter) both; }

    /* Desktop: floating side panel instead of a docked sheet */
    @media (min-width: 1000px) {
      .sheet { left:auto; right:20px; bottom:20px; width:400px;
        border-radius:18px; box-shadow:var(--sh-lg);
        max-height:42vh; }
      .sheet[data-state=half] { max-height:62vh; }
      .sheet[data-state=expanded] { max-height:calc(100% - 40px); }
    }
  `],
})
export class BottomSheetComponent {
  @Input() state: SheetState = 'collapsed';
  /** Waste-fraction accent for the grip bar (CSS color/var). */
  @Input() accent = '';
  @Output() stateChange = new EventEmitter<SheetState>();

  private static readonly ORDER: SheetState[] = ['collapsed', 'half', 'expanded'];

  cycle() {
    const i = BottomSheetComponent.ORDER.indexOf(this.state);
    this.set(BottomSheetComponent.ORDER[(i + 1) % BottomSheetComponent.ORDER.length]);
  }

  onKey(ev: KeyboardEvent) {
    if (ev.key === 'ArrowUp') { ev.preventDefault(); this.step(+1); }
    else if (ev.key === 'ArrowDown') { ev.preventDefault(); this.step(-1); }
  }

  /** Escape anywhere in the sheet steps it down one level. */
  onEscape() { this.step(-1); }

  private step(dir: 1 | -1) {
    const i = BottomSheetComponent.ORDER.indexOf(this.state);
    const next = BottomSheetComponent.ORDER[Math.min(2, Math.max(0, i + dir))];
    if (next !== this.state) this.set(next);
  }

  private set(next: SheetState) {
    this.state = next;
    this.stateChange.emit(next);
  }
}

/**
 * Floating panel — a card that floats over the map with strong elevation.
 * Position via `spot`: top (full-width strip), top-left, top-right.
 */
@Component({
  selector: 'ap-floating-panel',
  standalone: true,
  imports: [CommonModule],
  template: `<div class="fp" [attr.data-spot]="spot"><ng-content></ng-content></div>`,
  styles: [`
    .fp { position:absolute; z-index:15; background:rgba(255,255,255,.96);
      backdrop-filter:blur(6px); border:1px solid var(--c-border);
      border-radius:14px; box-shadow:var(--sh-lg); }
    [data-spot=top] { top:12px; left:12px; right:12px; }
    [data-spot=top-left] { top:12px; left:12px; }
    [data-spot=top-right] { top:12px; right:12px; }
  `],
})
export class FloatingPanelComponent {
  @Input() spot: 'top' | 'top-left' | 'top-right' = 'top';
}
