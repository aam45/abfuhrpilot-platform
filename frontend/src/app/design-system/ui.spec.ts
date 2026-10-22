import { TestBed } from '@angular/core/testing';
import { SkeletonComponent, StatComponent } from './ui';

describe('design system — new primitives', () => {
  it('ap-skeleton renders the requested number of shimmer rows with a loading role', () => {
    const fixture = TestBed.createComponent(SkeletonComponent);
    fixture.componentInstance.rows = 6;
    fixture.detectChanges();
    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelectorAll('.sk-row').length).toBe(6);
    expect(el.querySelector('[role="status"]')).toBeTruthy();
    expect(el.querySelector('[aria-label="Wird geladen"]')).toBeTruthy();
  });

  it('ap-stat applies the problem tone for critical tiles', () => {
    const fixture = TestBed.createComponent(StatComponent);
    fixture.componentInstance.label = 'Problemstellen';
    fixture.componentInstance.value = 3;
    fixture.componentInstance.tone = 'problem';
    fixture.detectChanges();
    const stat: HTMLElement = fixture.nativeElement.querySelector('.stat');
    expect(stat.getAttribute('data-tone')).toBe('problem');
    expect(fixture.nativeElement.textContent).toContain('Problemstellen');
    expect(fixture.nativeElement.textContent).toContain('3');
  });

  it('ap-stat stays neutral without a tone', () => {
    const fixture = TestBed.createComponent(StatComponent);
    fixture.componentInstance.label = 'Heute im Einsatz';
    fixture.componentInstance.value = 5;
    fixture.detectChanges();
    const stat: HTMLElement = fixture.nativeElement.querySelector('.stat');
    expect(stat.getAttribute('data-tone')).toBeNull();
  });
});
