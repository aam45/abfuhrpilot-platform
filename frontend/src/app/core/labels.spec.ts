import { statusTone, routeStatusLabel, stopStatusLabel, wasteCategoryLabel, wasteColorVar } from './labels';

describe('labels', () => {
  it('maps operational statuses to the right visual tone', () => {
    expect(statusTone('Open')).toBe('open');
    expect(statusTone('InProgress')).toBe('progress');
    expect(statusTone('Done')).toBe('done');
    expect(statusTone('Skipped')).toBe('skip');
    expect(statusTone('Problem')).toBe('problem');
    expect(statusTone('Unknown')).toBe('open'); // safe fallback
  });

  it('maps waste-fraction names and categories to their rail color token', () => {
    expect(wasteColorVar('Kunststoff')).toBe('var(--w-kunststoff)');
    expect(wasteColorVar('Plastic')).toBe('var(--w-kunststoff)');
    expect(wasteColorVar('Biomüll')).toBe('var(--w-bio)');
    expect(wasteColorVar('Sonderabfall')).toBe('var(--w-sonder)');
    expect(wasteColorVar('Hausmüll')).toBe('var(--w-hausmuell)');
    expect(wasteColorVar(undefined)).toBe('var(--w-hausmuell)'); // safe fallback
  });

  it('provides German labels for core enums', () => {
    expect(routeStatusLabel.InProgress).toBe('In Arbeit');
    expect(stopStatusLabel.Done).toBe('Erledigt');
    expect(wasteCategoryLabel.HouseholdWaste).toBe('Hausmüll');
    expect(wasteCategoryLabel.Plastic).toBe('Kunststoff');
  });
});
