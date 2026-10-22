import { Injectable, OnDestroy, signal } from '@angular/core';

export type ToastTone = 'done' | 'problem' | 'info';

export interface Toast {
  id: number;
  text: string;
  tone: ToastTone;
  duration: number;
  /** True once `dismiss()` has started the exit transition; the entry is removed after `EXIT_MS`. */
  leaving: boolean;
}

/** Exit-transition window before a leaving toast is actually removed from the array — mirrors `ap-flash`. */
const EXIT_MS = 200;

/**
 * Global operational toast queue. Unlike `ap-flash` (one message, owned by a
 * single component), this holds an array so concurrent messages from
 * different parts of the app — a save on one screen, a background sync
 * warning, an auth timeout — stack instead of clobbering each other.
 *
 * Mounted once via `ap-toast-stack` in `ShellComponent`, outside the routed
 * content, so toasts survive navigation and always render in one place.
 */
@Injectable({ providedIn: 'root' })
export class ToastService implements OnDestroy {
  toasts = signal<Toast[]>([]);
  private nextId = 1;
  private timers = new Map<number, ReturnType<typeof setTimeout>>();
  private exitTimers = new Map<number, ReturnType<typeof setTimeout>>();

  /** Queues a new toast. Never replaces an existing one — each call adds its own entry. */
  show(text: string, tone: ToastTone = 'done', duration = 4000): number {
    const id = this.nextId++;
    this.toasts.update(list => [...list, { id, text, tone, duration, leaving: false }]);
    const timer = setTimeout(() => this.dismiss(id), duration);
    this.timers.set(id, timer);
    return id;
  }

  /**
   * Manual or timer-triggered close — starts the fade-out for just this entry
   * (elements must never vanish without transition), then removes it after
   * `EXIT_MS`. No side effects on other queued toasts.
   */
  dismiss(id: number) {
    const timer = this.timers.get(id);
    if (timer) { clearTimeout(timer); this.timers.delete(id); }
    if (this.exitTimers.has(id)) return; // already leaving
    this.toasts.update(list => list.map(t => (t.id === id ? { ...t, leaving: true } : t)));
    const exitTimer = setTimeout(() => {
      this.exitTimers.delete(id);
      this.toasts.update(list => list.filter(t => t.id !== id));
    }, EXIT_MS);
    this.exitTimers.set(id, exitTimer);
  }

  ngOnDestroy() {
    this.timers.forEach(clearTimeout);
    this.timers.clear();
    this.exitTimers.forEach(clearTimeout);
    this.exitTimers.clear();
  }
}
