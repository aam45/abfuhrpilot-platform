import { Component, Input } from '@angular/core';

/**
 * Bespoke stroke icon set — one geometric micro-family for all UI chrome
 * (replaces OS-dependent emoji glyphs). 24×24 viewBox, 1.8px stroke,
 * round caps/joins, currentColor. Each glyph is a single <path> `d` string,
 * so no innerHTML/sanitizer is needed and tree-shaking stays trivial.
 * Decorative by default (aria-hidden) — pair with visible text or aria-label
 * on the interactive parent.
 */
export const AP_ICONS: Record<string, string> = {
  grid: 'M4 4h6.5v6.5H4zM13.5 4H20v6.5h-6.5zM4 13.5h6.5V20H4zM13.5 13.5H20V20h-6.5z',
  route: 'M5 19m-2 0a2 2 0 1 0 4 0a2 2 0 1 0-4 0M19 5m-2 0a2 2 0 1 0 4 0a2 2 0 1 0-4 0M6.8 17.2C14 17.2 10 6.8 17.2 6.8',
  clipboard: 'M9 3.5h6V7H9zM15 5h2.5A1.5 1.5 0 0 1 19 6.5v12a1.5 1.5 0 0 1-1.5 1.5h-11A1.5 1.5 0 0 1 5 18.5v-12A1.5 1.5 0 0 1 6.5 5H9M9 11.5h6M9 15h6',
  truck: 'M2.5 6.5H14v9H2.5zM14 9.5h3.6l2.9 3.2v2.8H14M7 17.2m-1.8 0a1.8 1.8 0 1 0 3.6 0a1.8 1.8 0 1 0-3.6 0M17 17.2m-1.8 0a1.8 1.8 0 1 0 3.6 0a1.8 1.8 0 1 0-3.6 0',
  package: 'M4 8l8-4 8 4v8l-8 4-8-4zM4 8l8 4m0 0l8-4m-8 4v8',
  person: 'M12 8m-3.5 0a3.5 3.5 0 1 0 7 0a3.5 3.5 0 1 0-7 0M5 20c0-3.6 3.1-5.5 7-5.5s7 1.9 7 5.5',
  users: 'M9 8.5m-3 0a3 3 0 1 0 6 0a3 3 0 1 0-6 0M3.5 19.5c0-3 2.4-4.8 5.5-4.8s5.5 1.8 5.5 4.8M15.5 5.7a3 3 0 0 1 0 5.6M16.8 14.9c2.3.5 3.7 2 3.7 4.6',
  bin: 'M5 7h14M9.5 7V4.5h5V7M7 7l.9 13h8.2L17 7M10 10.5l.3 6M14 10.5l-.3 6',
  depot: 'M3.5 20V8.5l5.5 3v-3l5.5 3V5.5H20V20H3.5z',
  signout: 'M9.5 4H6.5A1.5 1.5 0 0 0 5 5.5v13A1.5 1.5 0 0 0 6.5 20h3M15 8l4 4-4 4M19 12H10',
  menu: 'M4 7h16M4 12h16M4 17h16',
  search: 'M11 11m-6 0a6 6 0 1 0 12 0a6 6 0 1 0-12 0M15.4 15.4L20 20',
  warning: 'M12 4L2.5 20h19zM12 10.5v4M12 17.4v.01',
  wheel: 'M12 12m-8 0a8 8 0 1 0 16 0a8 8 0 1 0-16 0M12 12m-2.5 0a2.5 2.5 0 1 0 5 0a2.5 2.5 0 1 0-5 0M12 4v5.5M4.6 14.9l5.1-1.7M19.4 14.9l-5.1-1.7',
  check: 'M4.5 12.5l5 5L19.5 7',
};

@Component({
  selector: 'ap-icon',
  standalone: true,
  template: `
    <svg [attr.width]="size" [attr.height]="size" viewBox="0 0 24 24" fill="none"
         stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"
         aria-hidden="true"><path [attr.d]="d"></path></svg>`,
  styles: [`:host { display:inline-flex; flex:0 0 auto; line-height:0; }`],
})
export class IconComponent {
  d = '';
  @Input() size = 18;
  @Input({ required: true }) set name(v: string) { this.d = AP_ICONS[v] ?? ''; }
}
