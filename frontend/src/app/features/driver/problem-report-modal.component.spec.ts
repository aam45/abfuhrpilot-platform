import { TestBed } from '@angular/core/testing';
import { PhotoEntry, ProblemReportModalComponent } from './problem-report-modal.component';
import { ProblemType, Stop } from '../../core/models';

const LABELS: Record<string, string> = {
  BlockedByCar: 'Durch Auto blockiert',
  BinNotAvailable: 'Tonne nicht bereitgestellt',
  Other: 'Sonstiges',
};

const PROBLEM_TYPES: ProblemType[] = ['BlockedByCar', 'BinNotAvailable', 'Other'];

function stop(overrides: Partial<Stop> = {}): Stop {
  return {
    id: 's1', routeId: 'r1', sequenceNumber: 1, address: 'Hauptstraße 1',
    latitude: null, longitude: null, wasteTypeId: 'w1', pickupType: 'RegularBinPickup', status: 'Open',
    ...overrides,
  } as Stop;
}

describe('ProblemReportModalComponent', () => {
  function mount() {
    const fixture = TestBed.createComponent(ProblemReportModalComponent);
    const component = fixture.componentInstance;
    component.stop = stop();
    component.problemTypes = PROBLEM_TYPES;
    component.labels = LABELS;
    fixture.detectChanges();
    return { fixture, component };
  }

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [ProblemReportModalComponent] });
  });

  it('renders the stop address', () => {
    const { fixture } = mount();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Hauptstraße 1');
  });

  it('renders one <option> per problemType, labelled via the labels map', () => {
    const { fixture } = mount();
    const options: HTMLOptionElement[] = Array.from(fixture.nativeElement.querySelectorAll('option'));
    expect(options.length).toBe(PROBLEM_TYPES.length);
    expect(options.map(o => o.textContent?.trim())).toEqual(['Durch Auto blockiert', 'Tonne nicht bereitgestellt', 'Sonstiges']);
  });

  it('clicking the backdrop emits closed', () => {
    const { fixture, component } = mount();
    let closed = false;
    component.closed.subscribe(() => (closed = true));
    const bg: HTMLElement = fixture.nativeElement.querySelector('.modal-bg');
    bg.click();
    expect(closed).toBe(true);
  });

  it('clicking inside the modal does not emit closed', () => {
    const { fixture, component } = mount();
    let closed = false;
    component.closed.subscribe(() => (closed = true));
    const modal: HTMLElement = fixture.nativeElement.querySelector('.modal');
    modal.click();
    expect(closed).toBe(false);
  });

  it('"Abbrechen" emits closed', () => {
    const { fixture, component } = mount();
    let closed = false;
    component.closed.subscribe(() => (closed = true));
    const buttons: HTMLButtonElement[] = Array.from(fixture.nativeElement.querySelectorAll('.modal-actions button'));
    const cancel = buttons.find(b => b.textContent?.includes('Abbrechen'))!;
    cancel.click();
    expect(closed).toBe(true);
  });

  it('submit button is disabled when busy', () => {
    const { fixture, component } = mount();
    component.busy = true;
    fixture.detectChanges();
    const submitBtn: HTMLButtonElement = fixture.nativeElement.querySelector('.ap-btn--danger');
    expect(submitBtn.disabled).toBe(true);
  });

  it('submit button is disabled and shows "Foto lädt…" while uploading', () => {
    const { fixture, component } = mount();
    component.uploading = true;
    fixture.detectChanges();
    const submitBtn: HTMLButtonElement = fixture.nativeElement.querySelector('.ap-btn--danger');
    expect(submitBtn.disabled).toBe(true);
    expect(submitBtn.textContent).toContain('Foto lädt…');
  });

  it('submit emits the currently selected problemType and note', () => {
    const { fixture, component } = mount();
    let emitted: { type: ProblemType; note: string } | undefined;
    component.submitted.subscribe(v => (emitted = v));
    component.problemType = 'BinNotAvailable';
    component.problemNote = 'Zufahrt versperrt';
    fixture.detectChanges();
    const submitBtn: HTMLButtonElement = fixture.nativeElement.querySelector('.ap-btn--danger');
    submitBtn.click();
    expect(emitted).toEqual({ type: 'BinNotAvailable', note: 'Zufahrt versperrt' });
  });

  it('renders a thumbnail per photo and emits photoRemoved with the correct index', () => {
    const photos: PhotoEntry[] = [
      { name: 'a.jpg', objectUrl: 'blob:a', uploading: false },
      { name: 'b.jpg', objectUrl: 'blob:b', uploading: false },
    ];
    const { fixture, component } = mount();
    component.photos = photos;
    fixture.detectChanges();

    const thumbs: HTMLElement[] = Array.from(fixture.nativeElement.querySelectorAll('.thumb'));
    expect(thumbs.length).toBe(2);
    expect(thumbs[0].querySelector('img')?.getAttribute('src')).toBe('blob:a');
    expect(thumbs[1].querySelector('img')?.getAttribute('src')).toBe('blob:b');

    let removedIndex: number | undefined;
    component.photoRemoved.subscribe(i => (removedIndex = i));
    (thumbs[1].querySelector('.thumb-x') as HTMLButtonElement).click();
    expect(removedIndex).toBe(1);
  });

  it('shows the spinner overlay for an uploading photo and the error overlay for a failed photo', () => {
    const photos: PhotoEntry[] = [
      { name: 'a.jpg', objectUrl: 'blob:a', uploading: true },
      { name: 'b.jpg', objectUrl: 'blob:b', uploading: false, error: true },
    ];
    const { fixture, component } = mount();
    component.photos = photos;
    fixture.detectChanges();

    const thumbs: HTMLElement[] = Array.from(fixture.nativeElement.querySelectorAll('.thumb'));
    expect(thumbs[0].querySelector('.thumb-over')).toBeTruthy();
    expect(thumbs[0].querySelector('.thumb-err')).toBeFalsy();
    expect(thumbs[1].querySelector('.thumb-err')).toBeTruthy();
    expect(thumbs[1].querySelector('.thumb-over')).toBeFalsy();
  });

  it('emits photoSelected with the raw file-input change event', () => {
    const { fixture, component } = mount();
    let emitted: Event | undefined;
    component.photoSelected.subscribe(ev => (emitted = ev));
    const input: HTMLInputElement = fixture.nativeElement.querySelector('input[type="file"]');
    input.dispatchEvent(new Event('change'));
    expect(emitted).toBeTruthy();
    expect(emitted?.type).toBe('change');
  });

  it('ngOnChanges resets problemType and clears problemNote when a new stop input arrives', () => {
    const { component } = mount();
    component.problemType = 'Other';
    component.problemNote = 'irgendein Text';

    component.stop = stop({ id: 's2', address: 'Nebenstraße 2' });
    component.ngOnChanges({
      stop: { currentValue: component.stop, previousValue: null, firstChange: false, isFirstChange: () => false },
    });

    expect(component.problemType).toBe('BlockedByCar');
    expect(component.problemNote).toBe('');
  });
});
