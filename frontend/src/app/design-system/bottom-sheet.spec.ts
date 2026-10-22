import { TestBed } from '@angular/core/testing';
import { BottomSheetComponent } from './bottom-sheet';
import { MapUiStateService } from '../core/map/map-ui-state.service';
import { derivePrimaryAction } from '../features/driver/driver.component';
import { RouteDto, Stop } from '../core/models';

describe('ap-bottom-sheet (map-first interaction surface)', () => {
  function make(state: 'collapsed' | 'half' | 'expanded' = 'collapsed') {
    const fixture = TestBed.createComponent(BottomSheetComponent);
    fixture.componentInstance.state = state;
    fixture.detectChanges();
    return fixture;
  }

  it('cycles collapsed → half → expanded → collapsed via the grip', () => {
    const fixture = make();
    const c = fixture.componentInstance;
    const seen: string[] = [];
    c.stateChange.subscribe(s => seen.push(s));
    c.cycle(); c.cycle(); c.cycle();
    expect(seen).toEqual(['half', 'expanded', 'collapsed']);
  });

  it('ArrowUp expands and ArrowDown collapses (keyboard operability)', () => {
    const fixture = make('half');
    const c = fixture.componentInstance;
    c.onKey(new KeyboardEvent('keydown', { key: 'ArrowUp' }));
    expect(c.state).toBe('expanded');
    c.onKey(new KeyboardEvent('keydown', { key: 'ArrowDown' }));
    c.onKey(new KeyboardEvent('keydown', { key: 'ArrowDown' }));
    expect(c.state).toBe('collapsed');
  });

  it('exposes aria-expanded on the grip and reflects the state attribute', () => {
    const fixture = make('half');
    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelector('.grip')!.getAttribute('aria-expanded')).toBe('true');
    expect(el.querySelector('.sheet')!.getAttribute('data-state')).toBe('half');
  });

  it('Escape steps the sheet down one level (and stops at collapsed)', () => {
    const fixture = make('expanded');
    const c = fixture.componentInstance;
    c.onEscape();
    expect(c.state).toBe('half');
    c.onEscape();
    c.onEscape(); // already collapsed → stays
    expect(c.state).toBe('collapsed');
  });
});

describe('MapUiStateService (sheet → map padding coordination)', () => {
  it('derives map bottom padding from the sheet state', () => {
    const svc = new MapUiStateService();
    expect(svc.bottomPadding()).toBe(120);        // collapsed peek
    svc.setSheetState('half');
    expect(svc.bottomPadding()).toBe(220);
    svc.setSheetState('expanded');
    expect(svc.bottomPadding()).toBe(320);
  });
});

describe('derivePrimaryAction (one obvious next action)', () => {
  const route = (status: string) => ({ status } as RouteDto);
  const stop = (status: string) => ({ status } as Stop);

  it('Planned route → Route starten', () => {
    expect(derivePrimaryAction(route('Planned'), stop('Open'), 0, 5)!.kind).toBe('start-route');
  });
  it('InProgress + open stop → Stopp starten', () => {
    expect(derivePrimaryAction(route('InProgress'), stop('Open'), 1, 5)!.kind).toBe('start-stop');
  });
  it('InProgress + stop in progress → Stopp erledigt', () => {
    expect(derivePrimaryAction(route('InProgress'), stop('InProgress'), 1, 5)!.kind).toBe('complete-stop');
  });
  it('all stops done → Tour abschließen', () => {
    expect(derivePrimaryAction(route('InProgress'), null, 5, 5)!.kind).toBe('complete-route');
  });
  it('Completed route → no primary action', () => {
    expect(derivePrimaryAction(route('Completed'), null, 5, 5)).toBeNull();
  });
});
