import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { EntityFormComponent } from './entity-form.component';
import { API_BASE } from '../../core/config';

describe('EntityFormComponent (catalogue create/edit)', () => {
  let http: HttpTestingController;

  function make(configKey: any, mode: 'create' | 'edit' = 'create', row: any = null) {
    const fixture = TestBed.createComponent(EntityFormComponent);
    fixture.componentInstance.configKey = configKey;
    fixture.componentInstance.mode = mode;
    fixture.componentInstance.row = row;
    fixture.detectChanges(); // ngOnInit builds the reactive form
    return fixture;
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [EntityFormComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  it('builds a form from the config with sensible defaults', () => {
    const c = make('wasteTypes').componentInstance;
    expect(c.form.contains('name')).toBeTrue();
    expect(c.form.contains('category')).toBeTrue();
    expect(c.form.get('isActive')!.value).toBeTrue(); // isActive defaults on
  });

  it('blocks submit and sends no request when required fields are empty', () => {
    const c = make('wasteTypes').componentInstance;
    c.submit();
    expect(c.form.invalid).toBeTrue();
    http.expectNone(`${API_BASE}/waste-types`);
  });

  it('posts the mapped payload when the form is valid', () => {
    const fixture = make('wasteTypes');
    const c = fixture.componentInstance;
    spyOn(c.saved, 'emit');
    c.form.patchValue({ name: 'Metall', code: 'ME', category: 'Metal' });

    c.submit();

    const req = http.expectOne(`${API_BASE}/waste-types`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.name).toBe('Metall');
    expect(req.request.body.defaultDestinationId).toBeNull(); // added by buildBody
    req.flush({ id: 'w1' });
    expect(c.saved.emit).toHaveBeenCalled();
  });

  it('sends a PUT to the entity id in edit mode', () => {
    const fixture = make('drivers', 'edit', { id: 'd1', fullName: 'Frank', phoneNumber: null, licenseNumber: null, isActive: true });
    const c = fixture.componentInstance;
    c.form.patchValue({ fullName: 'Frank Neu' });
    c.submit();
    const req = http.expectOne(`${API_BASE}/drivers/d1`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body.fullName).toBe('Frank Neu');
    req.flush({ id: 'd1' });
  });
});
