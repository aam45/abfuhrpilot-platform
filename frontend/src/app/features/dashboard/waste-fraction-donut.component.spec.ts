import { TestBed } from '@angular/core/testing';
import { WasteFractionDonutComponent } from './waste-fraction-donut.component';
import { wasteColorVar } from '../../core/labels';

describe('WasteFractionDonutComponent — Leitstand fraction distribution', () => {
  function mount(routes: ReadonlyArray<{ wasteTypeName?: string | null }>) {
    TestBed.configureTestingModule({ imports: [WasteFractionDonutComponent] });
    const fixture = TestBed.createComponent(WasteFractionDonutComponent);
    fixture.componentRef.setInput('routes', routes);
    fixture.detectChanges();
    return fixture;
  }

  const byName = (n: number) => ({ wasteTypeName: n === 0 ? 'Hausmüll' : n === 1 ? 'Papier' : 'Biomüll' });

  it('groups routes by wasteTypeName with correct counts, largest fraction first', () => {
    const fixture = mount([
      { wasteTypeName: 'Papier' },
      { wasteTypeName: 'Hausmüll' },
      { wasteTypeName: 'Hausmüll' },
      { wasteTypeName: 'Biomüll' },
      { wasteTypeName: 'Hausmüll' },
      { wasteTypeName: 'Papier' },
    ]);
    const slices = fixture.componentInstance.slices();
    expect(slices.map(s => s.name)).toEqual(['Hausmüll', 'Papier', 'Biomüll']);
    expect(slices.map(s => s.count)).toEqual([3, 2, 1]);
    expect(fixture.componentInstance.total()).toBe(6);
  });

  it('assigns every slice its color via wasteColorVar — the same mapping as the table dots', () => {
    const fixture = mount([{ wasteTypeName: 'Papier' }, { wasteTypeName: 'Biomüll' }]);
    for (const s of fixture.componentInstance.slices()) {
      expect(s.color).toBe(wasteColorVar(s.name));
    }
    // And the rendered SVG arcs carry exactly those colors.
    const strokes = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll('svg circle'),
    ).map(c => c.getAttribute('stroke'));
    // Equal counts → alphabetical tiebreak, so Biomüll leads.
    expect(strokes).toEqual([wasteColorVar('Biomüll'), wasteColorVar('Papier')]);
    expect(strokes).toEqual(fixture.componentInstance.slices().map(s => s.color));
  });

  it('legend states the explicit route count per fraction in German (counts, not percentages)', () => {
    const fixture = mount([
      { wasteTypeName: 'Hausmüll' },
      { wasteTypeName: 'Hausmüll' },
      { wasteTypeName: 'Papier' },
    ]);
    const items = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('.legend li'))
      .map(li => ({
        name: (li.querySelector('.name')?.textContent ?? '').trim(),
        count: (li.querySelector('.count')?.textContent ?? '').replace(/\s+/g, ' ').trim(),
      }));
    expect(items[0]).toEqual({ name: 'Hausmüll', count: '2 Touren' });
    expect(items[1]).toEqual({ name: 'Papier', count: '1 Tour' });
    expect((fixture.nativeElement as HTMLElement).textContent).not.toContain('%');
  });

  it('header shows the honest total of routes shown', () => {
    const fixture = mount([byName(0), byName(1), byName(2)]);
    const head = (fixture.nativeElement as HTMLElement).querySelector('.fd-head');
    expect((head!.textContent ?? '').replace(/\s+/g, ' ')).toContain('3 Touren');
  });

  it('empty input renders nothing at all — no fake gray donut, no empty card', () => {
    const fixture = mount([]);
    expect((fixture.nativeElement as HTMLElement).querySelector('.fd')).toBeNull();
    expect((fixture.nativeElement as HTMLElement).querySelector('svg')).toBeNull();
  });

  it('single fraction renders one full-circumference arc without a gap', () => {
    const fixture = mount([{ wasteTypeName: 'Hausmüll' }, { wasteTypeName: 'Hausmüll' }]);
    const arcs = fixture.componentInstance.arcs();
    expect(arcs.length).toBe(1);
    expect(arcs[0].dasharray).toBe('100.000 0.000');
  });

  it('multi-fraction arcs sum (arc + gap) back to the full circumference', () => {
    const fixture = mount([
      { wasteTypeName: 'Hausmüll' }, { wasteTypeName: 'Hausmüll' },
      { wasteTypeName: 'Papier' }, { wasteTypeName: 'Biomüll' },
    ]);
    const arcs = fixture.componentInstance.arcs();
    const gap = 1; // one hairline unit per slice
    const sum = arcs.reduce((acc, a) => acc + parseFloat(a.dasharray.split(' ')[0]) + gap, 0);
    expect(sum).toBeCloseTo(100, 3);
  });

  it('routes without a wasteTypeName group honestly under "Ohne Abfallart"', () => {
    const fixture = mount([{ wasteTypeName: null }, { wasteTypeName: '' }, { wasteTypeName: 'Papier' }]);
    const slices = fixture.componentInstance.slices();
    expect(slices.map(s => s.name)).toEqual(['Ohne Abfallart', 'Papier']);
    expect(slices[0].count).toBe(2);
  });

  it('"Ohne Abfallart" gets a neutral color — never wasteColorVar\'s Hausmüll fallback', () => {
    const fixture = mount([{ wasteTypeName: null }, { wasteTypeName: 'Hausmüll' }]);
    const slices = fixture.componentInstance.slices();
    const ohne = slices.find(s => s.name === 'Ohne Abfallart')!;
    expect(ohne.color).toBe('var(--c-border-strong)');
    // The impersonation guard itself: unnamed ≠ Hausmüll's color.
    expect(ohne.color).not.toBe(wasteColorVar('Hausmüll'));
    expect(ohne.color).not.toBe(wasteColorVar(undefined));
    // Real fractions on the same donut keep the shared mapping.
    expect(slices.find(s => s.name === 'Hausmüll')!.color).toBe(wasteColorVar('Hausmüll'));
    // And the rendered arc + legend dot carry the neutral color, not a fraction color.
    const el = fixture.nativeElement as HTMLElement;
    const strokes = Array.from(el.querySelectorAll('svg circle')).map(c => c.getAttribute('stroke'));
    expect(strokes).toContain('var(--c-border-strong)');
    expect(strokes.filter(s => s === wasteColorVar('Hausmüll')).length).toBe(1);
    const dots = Array.from(el.querySelectorAll('.legend .dot'))
      .map(d => (d as HTMLElement).style.background);
    expect(dots).toContain('var(--c-border-strong)');
  });

  it('pins the gap-centered dashoffset formula (25 − start − gap/2) for multi-fraction arcs', () => {
    // Counts 2/1/1 → pcts 50/25/25, gap 1. Largest first: Hausmüll(50), then
    // alphabetical tiebreak Biomüll(25), Papier(25).
    const fixture = mount([
      { wasteTypeName: 'Hausmüll' }, { wasteTypeName: 'Hausmüll' },
      { wasteTypeName: 'Biomüll' }, { wasteTypeName: 'Papier' },
    ]);
    const arcs = fixture.componentInstance.arcs();
    // start = 0, 50, 75 → offsets 24.5, −25.5, −50.5. A later "simplification"
    // that drops the gap/2 term or the running start would drift gaps off-center
    // while arc lengths (pinned elsewhere) still pass.
    expect(arcs.map(a => a.dashoffset)).toEqual(['24.500', '-25.500', '-50.500']);
    expect(arcs.map(a => a.dasharray.split(' ')[0])).toEqual(['49.000', '24.000', '24.000']);
  });

  it('single-fraction arc starts at 12 o\'clock exactly (offset 25, no gap correction)', () => {
    const fixture = mount([{ wasteTypeName: 'Hausmüll' }]);
    const arcs = fixture.componentInstance.arcs();
    expect(arcs.length).toBe(1);
    expect(arcs[0].dashoffset).toBe('25.000');
  });

  it('the donut SVG is decorative (aria-hidden) — the legend list carries the accessible content', () => {
    const fixture = mount([{ wasteTypeName: 'Hausmüll' }]);
    const svg = (fixture.nativeElement as HTMLElement).querySelector('svg');
    expect(svg!.getAttribute('aria-hidden')).toBe('true');
    const legend = (fixture.nativeElement as HTMLElement).querySelector('ul.legend');
    expect(legend).not.toBeNull();
    expect(legend!.getAttribute('aria-label')).toBe('Touren je Abfallart');
  });
});
