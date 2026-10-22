import { AfterViewInit, Component, ElementRef, EventEmitter, HostListener, Input, OnDestroy, Output, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';

let modalUid = 0;

/**
 * Reusable modal/dialog shell. Renders a centered card over a dimmed backdrop.
 * Content is projected; the footer uses a named slot. Closes on backdrop click
 * or Escape. Kept presentational — callers own the form logic.
 * Focus moves into the dialog on open and returns to the opener on close;
 * the card keeps `transform-origin: center` (modals are viewport-anchored,
 * not trigger-anchored).
 * Tab/Shift+Tab is trapped inside the dialog while it is open (keyboard and
 * screen-reader users must not be able to tab into the page behind it).
 */
@Component({
  selector: 'ap-modal',
  standalone: true,
  imports: [CommonModule],
  template: `
  <div class="bg" (click)="onBackdrop()">
    <div #dialog class="modal ap-card" role="dialog" aria-modal="true" tabindex="-1"
         [attr.aria-labelledby]="titleId" (click)="$event.stopPropagation()">
      <header class="head">
        <div><h3 [id]="titleId">{{ title }}</h3><p class="ap-muted" *ngIf="subtitle">{{ subtitle }}</p></div>
        <button class="x" type="button" (click)="close.emit()" aria-label="Schließen">✕</button>
      </header>
      <div class="body"><ng-content></ng-content></div>
      <footer class="foot"><ng-content select="[footer]"></ng-content></footer>
    </div>
  </div>`,
  styles: [`
    .bg { position:fixed; inset:0; background:rgba(16,24,40,.45); display:grid; place-items:center; z-index:50; padding:20px;
      animation:fade var(--t-med) both; }
    .modal { width:min(640px, 100%); max-height:90vh; display:flex; flex-direction:column;
      animation:pop var(--t-enter) both; outline:none; }
    .head { display:flex; align-items:flex-start; justify-content:space-between; gap:12px; padding:20px 22px 12px; border-bottom:1px solid var(--c-border); }
    .head h3 { font-size:18px; } .head p { margin:3px 0 0; font-size:13px; }
    .x { border:none; background:transparent; font-size:16px; cursor:pointer; color:var(--c-text-muted); padding:4px 8px; border-radius:8px;
      transition:background var(--t-fast), color var(--t-fast), transform var(--t-fast); }
    .x:active { transform:scale(.94); }
    @media (hover:hover) and (pointer:fine) { .x:hover { background:var(--c-surface-2); color:var(--c-text); } }
    .body { padding:20px 22px; overflow:auto; }
    .foot { padding:14px 22px 18px; border-top:1px solid var(--c-border); display:flex; justify-content:flex-end; gap:10px; }
    @keyframes fade { from { opacity:0; } }
    @keyframes pop { from { transform:translateY(8px) scale(.97); opacity:0; } }
  `],
})
export class ModalComponent implements AfterViewInit, OnDestroy {
  @Input() title = '';
  @Input() subtitle = '';
  @Input() closeOnBackdrop = true;
  @Output() close = new EventEmitter<void>();
  @ViewChild('dialog') dialog?: ElementRef<HTMLElement>;

  readonly titleId = `ap-modal-title-${modalUid++}`;
  private opener: HTMLElement | null = null;

  ngAfterViewInit() {
    this.opener = document.activeElement as HTMLElement | null;
    this.dialog?.nativeElement.focus();
  }

  ngOnDestroy() { this.opener?.focus?.(); }

  onBackdrop() { if (this.closeOnBackdrop) this.close.emit(); }
  @HostListener('document:keydown.escape') onEsc() { this.close.emit(); }

  /** Focusable elements inside the dialog, in DOM order. */
  private focusableElements(): HTMLElement[] {
    const root = this.dialog?.nativeElement;
    if (!root) return [];
    const selector = [
      'a[href]', 'button', 'input', 'select', 'textarea',
      '[tabindex]',
    ].join(',');
    return Array.from(root.querySelectorAll<HTMLElement>(selector)).filter(el =>
      !el.hasAttribute('disabled') &&
      el.getAttribute('aria-hidden') !== 'true' &&
      el.getAttribute('tabindex') !== '-1'
    );
  }

  /**
   * Traps Tab/Shift+Tab focus inside the dialog while it is open.
   * Bound to the raw `keydown` event (not Angular's `keydown.tab` pseudo-event):
   * Angular's key-event matching requires an exact modifier match, so a bare
   * `keydown.tab` binding never receives Shift+Tab — only plain Tab. Checking
   * `event.key`/`event.shiftKey` manually catches both.
   */
  @HostListener('document:keydown', ['$event'])
  onTab(event: KeyboardEvent) {
    if (event.key !== 'Tab') return;
    const root = this.dialog?.nativeElement;
    if (!root) return;
    const focusable = this.focusableElements();
    // No focusable content in the body/footer — keep focus on the dialog itself.
    if (focusable.length === 0) {
      event.preventDefault();
      root.focus();
      return;
    }
    const first = focusable[0];
    const last = focusable[focusable.length - 1];
    const active = document.activeElement;
    // Treat focus on the dialog container itself (the initial-focus target) as
    // "at the start" so Shift+Tab from there wraps to the last item instead of
    // escaping into the page behind the modal.
    if (event.shiftKey) {
      if (active === first || active === root || !root.contains(active)) {
        event.preventDefault();
        last.focus();
      }
    } else {
      if (active === last || !root.contains(active)) {
        event.preventDefault();
        first.focus();
      }
    }
  }
}

/**
 * Confirmation dialog — replaces native confirm() for destructive actions.
 * Deliberate by design: the danger action sits right, ghost cancel left,
 * Escape/backdrop cancel. Callers own the async work via `busy`.
 */
@Component({
  selector: 'ap-confirm',
  standalone: true,
  imports: [CommonModule, ModalComponent],
  template: `
  <ap-modal [title]="title" [subtitle]="subtitle" (close)="cancel.emit()">
    <p class="msg">{{ message }}</p>
    <ng-container footer>
      <button class="ap-btn ap-btn--ghost" type="button" (click)="cancel.emit()" [disabled]="busy">Abbrechen</button>
      <button class="ap-btn ap-btn--danger" type="button" (click)="confirm.emit()" [disabled]="busy">
        {{ busy ? 'Bitte warten…' : confirmLabel }}
      </button>
    </ng-container>
  </ap-modal>`,
  styles: [`.msg { margin:0; font-size:14px; line-height:1.55; color:var(--c-text-muted); }`],
})
export class ConfirmDialogComponent {
  @Input() title = 'Wirklich löschen?';
  @Input() subtitle = '';
  @Input() message = 'Diese Aktion kann nicht rückgängig gemacht werden.';
  @Input() confirmLabel = 'Löschen';
  @Input() busy = false;
  @Output() confirm = new EventEmitter<void>();
  @Output() cancel = new EventEmitter<void>();
}
