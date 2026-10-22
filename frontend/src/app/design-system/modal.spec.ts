import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { ConfirmDialogComponent, ModalComponent } from './modal';

describe('ap-modal (Tab-cycle focus trap)', () => {
  function make() {
    const fixture = TestBed.createComponent(ConfirmDialogComponent);
    fixture.detectChanges();
    const modal = fixture.debugElement.query(By.directive(ModalComponent)).componentInstance as ModalComponent;
    const el: HTMLElement = fixture.nativeElement;
    // DOM order inside the dialog: header close button ("x") → Abbrechen → Löschen.
    const closeBtn = el.querySelector('.head .x') as HTMLButtonElement;
    const cancelBtn = el.querySelector('.foot .ap-btn--ghost') as HTMLButtonElement;
    const confirmBtn = el.querySelector('.foot .ap-btn--danger') as HTMLButtonElement;
    return { fixture, modal, closeBtn, cancelBtn, confirmBtn };
  }

  // Dispatch a real KeyboardEvent on `document`, exercising the actual
  // `document:keydown` HostListener binding end-to-end (not a direct method
  // call) — this is what would have caught the Shift+Tab modifier-matching
  // bug that a bare `keydown.tab` Angular pseudo-event binding misses.
  function pressTab(shiftKey: boolean) {
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Tab', shiftKey, bubbles: true, cancelable: true }));
  }

  it('wraps Tab from the last focusable element back to the first', () => {
    const { confirmBtn, closeBtn } = make();
    confirmBtn.focus();
    expect(document.activeElement).toBe(confirmBtn);
    pressTab(false);
    expect(document.activeElement).toBe(closeBtn);
  });

  it('wraps Shift+Tab from the first focusable element back to the last', () => {
    const { confirmBtn, closeBtn } = make();
    closeBtn.focus();
    expect(document.activeElement).toBe(closeBtn);
    pressTab(true);
    expect(document.activeElement).toBe(confirmBtn);
  });

  it('Shift+Tab from the initial dialog-container focus wraps to the last focusable element (does not escape the modal)', () => {
    const { confirmBtn } = make();
    // ngAfterViewInit already focused the dialog container itself.
    pressTab(true);
    expect(document.activeElement).toBe(confirmBtn);
  });
});
