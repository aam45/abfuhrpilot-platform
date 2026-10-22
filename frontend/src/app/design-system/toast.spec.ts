import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { ToastService } from './toast.service';
import { ToastStackComponent } from './ui';

describe('ToastService — stacked queue', () => {
  let svc: ToastService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    svc = TestBed.inject(ToastService);
  });

  it('queues multiple concurrent toasts instead of clobbering each other', () => {
    svc.show('Eintrag angelegt.');
    svc.show('Eintrag gelöscht.', 'problem');
    expect(svc.toasts().length).toBe(2);
    expect(svc.toasts()[0].text).toBe('Eintrag angelegt.');
    expect(svc.toasts()[1].text).toBe('Eintrag gelöscht.');
    expect(svc.toasts()[1].tone).toBe('problem');
  });

  it('auto-dismisses only the toast whose duration elapsed', fakeAsync(() => {
    svc.show('Kurz', 'done', 100);
    svc.show('Lang', 'done', 1000);
    expect(svc.toasts().length).toBe(2);
    // duration (100ms) elapses, starting the exit transition …
    tick(100);
    expect(svc.toasts().length).toBe(2);
    expect(svc.toasts().find(t => t.text === 'Kurz')?.leaving).toBe(true);
    // … then EXIT_MS (200ms) later the entry is actually removed from the array.
    tick(200);
    expect(svc.toasts().length).toBe(1);
    expect(svc.toasts()[0].text).toBe('Lang');
    tick(1000);
    expect(svc.toasts().length).toBe(0);
  }));

  it('dismiss() removes only the targeted entry after the exit transition', fakeAsync(() => {
    const first = svc.show('Eins', 'done', 5000);
    svc.show('Zwei', 'done', 5000);
    svc.dismiss(first);
    // Still present but marked leaving until the exit transition finishes —
    // elements must never vanish without a transition.
    expect(svc.toasts().length).toBe(2);
    expect(svc.toasts().find(t => t.id === first)?.leaving).toBe(true);
    tick(200);
    expect(svc.toasts().length).toBe(1);
    expect(svc.toasts()[0].text).toBe('Zwei');
  }));
});

describe('ap-toast-stack', () => {
  let svc: ToastService;

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [ToastStackComponent] });
    svc = TestBed.inject(ToastService);
  });

  it('renders every concurrent toast with its tone and dismiss control', () => {
    svc.show('Benutzer aktualisiert.');
    svc.show('Löschen fehlgeschlagen.', 'problem');
    const fixture = TestBed.createComponent(ToastStackComponent);
    fixture.detectChanges();
    const el: HTMLElement = fixture.nativeElement;
    const items = el.querySelectorAll('.toast');
    expect(items.length).toBe(2);
    expect(items[0].textContent).toContain('Benutzer aktualisiert.');
    expect(items[1].getAttribute('data-tone')).toBe('problem');
    expect(items[1].getAttribute('role')).toBe('alert');
    expect(items[0].getAttribute('role')).toBe('status');
    expect(items[0].querySelector('.close')).toBeTruthy();
  });

  it('renders nothing when the queue is empty', () => {
    const fixture = TestBed.createComponent(ToastStackComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.toast-stack')).toBeNull();
  });

  it('dismissing one toast via its close button removes only that entry', fakeAsync(() => {
    svc.show('Bleibt.');
    const removedId = svc.show('Wird geschlossen.');
    const fixture = TestBed.createComponent(ToastStackComponent);
    fixture.detectChanges();
    const closeButtons = fixture.nativeElement.querySelectorAll('.close');
    (closeButtons[1] as HTMLButtonElement).click();
    fixture.detectChanges();
    tick(200); // exit transition
    fixture.detectChanges();
    expect(svc.toasts().find((t: { id: number }) => t.id === removedId)).toBeUndefined();
    expect(svc.toasts().length).toBe(1);
    expect(fixture.nativeElement.querySelectorAll('.toast').length).toBe(1);
  }));

  it('marks a dismissed toast as leaving (fade-out) before it is removed from the DOM', fakeAsync(() => {
    svc.show('Wird geschlossen.');
    const fixture = TestBed.createComponent(ToastStackComponent);
    fixture.detectChanges();
    (fixture.nativeElement.querySelector('.close') as HTMLButtonElement).click();
    fixture.detectChanges();
    // Still rendered (elements must never vanish without a transition) but tagged for the fade-out.
    const item = fixture.nativeElement.querySelector('.toast');
    expect(item).toBeTruthy();
    expect(item.classList.contains('leaving')).toBe(true);
    tick(200);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.toast')).toBeNull();
  }));
});
