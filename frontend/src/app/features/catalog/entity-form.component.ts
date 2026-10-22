import { Component, EventEmitter, Input, OnInit, Output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ApiService } from '../../core/api/api.service';
import { ModalComponent } from '../../design-system/modal';
import { FormConfig, FormField, formConfigs, wasteCategoryOptions } from './form-configs';
import { ListConfigKey } from './list-configs';

/**
 * Config-driven reactive create/edit form rendered in a modal. One component
 * serves all six catalogue entities — the field schema comes from `form-configs`.
 */
@Component({
  selector: 'ap-entity-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, ModalComponent],
  template: `
  <ap-modal [title]="titleText()" [subtitle]="cfg.singular" (close)="cancel.emit()">
    <form [formGroup]="form" class="grid" (ngSubmit)="submit()" id="entity-form">
      <ng-container *ngFor="let f of cfg.fields">
        <!-- checkbox -->
        <label *ngIf="f.type === 'checkbox'" class="check" [class.full]="true">
          <input type="checkbox" [formControlName]="f.key" />
          <span><strong>{{ f.label }}</strong><small class="ap-subtle" *ngIf="f.help">{{ f.help }}</small></span>
        </label>

        <!-- waste multi-select -->
        <div *ngIf="f.type === 'waste-multi'" class="field full">
          <label class="ap-label">{{ f.label }}<em *ngIf="f.required">*</em></label>
          <div class="chips">
            <button type="button" *ngFor="let o of wasteOptions" class="ap-chip"
                    [class.on]="isChecked(f.key, o.value)" [attr.aria-pressed]="isChecked(f.key, o.value)"
                    (click)="toggleWaste(f.key, o.value)">{{ o.label }}</button>
          </div>
          <small class="ap-subtle" *ngIf="f.help">{{ f.help }}</small>
          <small class="err" *ngIf="showError(f)">Bitte mindestens eine Abfallart wählen.</small>
        </div>

        <!-- standard fields -->
        <div *ngIf="isStandard(f)" class="field" [class.half]="f.half">
          <label class="ap-label" [for]="f.key">{{ f.label }}<em *ngIf="f.required">*</em></label>

          <select *ngIf="f.type === 'select'" class="ap-select" [id]="f.key" [formControlName]="f.key">
            <option value="" disabled>Bitte wählen…</option>
            <option *ngFor="let o of optionsFor(f)" [value]="o.value">{{ o.label }}</option>
          </select>

          <textarea *ngIf="f.type === 'textarea'" class="ap-input" [id]="f.key" rows="2"
                    [formControlName]="f.key" [placeholder]="f.placeholder || ''"></textarea>

          <input *ngIf="f.type === 'text'" class="ap-input" [id]="f.key" [formControlName]="f.key" [placeholder]="f.placeholder || ''" />
          <input *ngIf="f.type === 'number'" class="ap-input" type="number" step="any" [id]="f.key" [formControlName]="f.key" />
          <input *ngIf="f.type === 'date'" class="ap-input" type="date" [id]="f.key" [formControlName]="f.key" />

          <small class="ap-subtle" *ngIf="f.help">{{ f.help }}</small>
          <small class="err" *ngIf="showError(f)">Pflichtfeld</small>
        </div>
      </ng-container>

      <div class="apierr full" *ngIf="error()">{{ error() }}</div>
    </form>

    <ng-container footer>
      <button class="ap-btn ap-btn--ghost" type="button" (click)="cancel.emit()">Abbrechen</button>
      <button class="ap-btn ap-btn--primary" type="submit" form="entity-form" [disabled]="busy()">
        {{ busy() ? 'Speichern…' : (mode === 'edit' ? 'Änderungen speichern' : 'Anlegen') }}
      </button>
    </ng-container>
  </ap-modal>`,
  styles: [`
    .grid { display:grid; grid-template-columns:1fr 1fr; gap:16px; }
    .field { display:flex; flex-direction:column; grid-column:1 / -1; }
    .field.half { grid-column:auto; }
    .full { grid-column:1 / -1; }
    .ap-label em { color:var(--c-problem); margin-left:2px; font-style:normal; }
    .check { display:flex; gap:10px; align-items:flex-start; padding:12px 14px; border:1px solid var(--c-border); border-radius:var(--r-md); background:var(--c-surface-2); cursor:pointer; }
    .check input { margin-top:3px; width:16px; height:16px; }
    .check span { display:flex; flex-direction:column; } .check small { margin-top:2px; }
    .chips { display:flex; flex-wrap:wrap; gap:8px; margin:2px 0 2px; }
    .err { color:var(--c-problem); font-size:12px; margin-top:4px; font-weight:600; }
    small { margin-top:5px; }
    .apierr { background:var(--c-problem-soft); color:var(--c-problem); padding:10px 12px; border-radius:10px; font-size:13px; font-weight:600; }
    @media (max-width:560px){ .grid { grid-template-columns:1fr; } .field.half { grid-column:1 / -1; } }
  `],
})
export class EntityFormComponent implements OnInit {
  @Input({ required: true }) configKey!: ListConfigKey;
  @Input() mode: 'create' | 'edit' = 'create';
  @Input() row: any = null;
  @Output() saved = new EventEmitter<void>();
  @Output() cancel = new EventEmitter<void>();

  cfg!: FormConfig;
  form!: FormGroup;
  busy = signal(false);
  error = signal('');
  submitted = signal(false);
  wasteOptions = wasteCategoryOptions;
  private dynamicOptions: Record<string, { value: string; label: string }[]> = {};

  constructor(private fb: FormBuilder, private api: ApiService) {}

  ngOnInit() {
    this.cfg = formConfigs[this.configKey];
    const initial = this.mode === 'edit' && this.row ? this.cfg.toForm(this.row) : {};
    const controls: Record<string, any> = {};
    for (const f of this.cfg.fields) {
      const val = initial[f.key] ?? this.defaultFor(f);
      const validators = f.required && f.type !== 'waste-multi' ? [Validators.required] : [];
      controls[f.key] = [val, validators];
    }
    this.form = this.fb.group(controls);
    this.loadDynamicOptions();
  }

  private defaultFor(f: FormField): any {
    if (f.type === 'checkbox') return f.key === 'isActive';
    if (f.type === 'waste-multi') return [];
    return '';
  }

  private loadDynamicOptions() {
    const needsWasteTypes = this.cfg.fields.some(f => f.optionsSource === 'wasteTypes');
    if (needsWasteTypes) {
      this.api.wasteTypes({ pageSize: 200 }).subscribe(p =>
        this.dynamicOptions['wasteTypes'] = p.items.map(w => ({ value: w.id, label: w.name })));
    }
  }

  titleText() { return this.mode === 'edit' ? `${this.cfg.singular} bearbeiten` : `Neu: ${this.cfg.singular}`; }
  isStandard(f: FormField) { return f.type !== 'checkbox' && f.type !== 'waste-multi'; }
  optionsFor(f: FormField) { return f.optionsSource ? (this.dynamicOptions[f.optionsSource] ?? []) : (f.options ?? []); }

  isChecked(key: string, value: string): boolean { return (this.form.get(key)!.value as string[]).includes(value); }
  toggleWaste(key: string, value: string) {
    const ctrl = this.form.get(key)!;
    const arr = [...(ctrl.value as string[])];
    const i = arr.indexOf(value);
    i >= 0 ? arr.splice(i, 1) : arr.push(value);
    ctrl.setValue(arr); ctrl.markAsDirty();
  }

  showError(f: FormField): boolean {
    if (!this.submitted()) return false;
    if (f.type === 'waste-multi') return !!f.required && (this.form.get(f.key)!.value as string[]).length === 0;
    return !!f.required && this.form.get(f.key)!.invalid;
  }

  submit() {
    this.submitted.set(true);
    // waste-multi required rule (not covered by Validators.required on arrays)
    const missingWaste = this.cfg.fields.some(f =>
      f.type === 'waste-multi' && f.required && (this.form.get(f.key)!.value as string[]).length === 0);
    if (this.form.invalid || missingWaste) { this.form.markAllAsTouched(); return; }

    this.busy.set(true); this.error.set('');
    const body = this.cfg.buildBody(this.form.getRawValue());
    const op = this.mode === 'edit'
      ? this.cfg.update(this.api, this.row.id, body)
      : this.cfg.create(this.api, body);
    op.subscribe({
      next: () => { this.busy.set(false); this.saved.emit(); },
      error: (e) => { this.busy.set(false); this.error.set(e?.error?.detail || 'Speichern fehlgeschlagen. Bitte Eingaben prüfen.'); },
    });
  }
}
