import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ApiService } from '../../core/api/api.service';
import { Driver, Vehicle, WasteType } from '../../core/models';
import { wasteCategoryLabel } from '../../core/labels';
import { PageHeaderComponent } from '../../design-system/ui';

@Component({
  selector: 'ap-route-create',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, PageHeaderComponent],
  template: `
  <div class="ap-page-enter">
  <a routerLink="/routen" class="back">← Zurück zu Routen</a>
  <ap-page-header title="Neue Route" subtitle="Tour anlegen und Fahrzeug/Fahrer zuweisen"></ap-page-header>

  <form class="ap-card form" (ngSubmit)="submit()">
    <div class="row">
      <div class="ap-field"><label class="ap-label">Routennummer</label>
        <input class="ap-input" [(ngModel)]="m.routeNumber" name="rn" required placeholder="R-002" /></div>
      <div class="ap-field"><label class="ap-label">Bezeichnung</label>
        <input class="ap-input" [(ngModel)]="m.name" name="name" required placeholder="Hausmüll Nordstadt" /></div>
    </div>
    <div class="row">
      <div class="ap-field"><label class="ap-label">Datum</label>
        <input class="ap-input" type="date" [(ngModel)]="m.date" name="date" required /></div>
      <div class="ap-field"><label class="ap-label">Abfallart</label>
        <select class="ap-select" [(ngModel)]="m.wasteTypeId" name="wt" required>
          <option value="" disabled>Bitte wählen…</option>
          <option *ngFor="let w of wasteTypes()" [value]="w.id">{{ w.name }} ({{ wl[w.category] }})</option>
        </select></div>
    </div>
    <div class="row">
      <div class="ap-field"><label class="ap-label">Fahrzeug</label>
        <select class="ap-select" [(ngModel)]="m.vehicleId" name="veh">
          <option value="">— später zuweisen —</option>
          <option *ngFor="let v of vehicles()" [value]="v.id">{{ v.name }} · {{ v.licensePlate }}</option>
        </select>
        <small class="hint ap-subtle">Das Fahrzeug muss für die Abfallart freigegeben sein.</small></div>
      <div class="ap-field"><label class="ap-label">Fahrer</label>
        <select class="ap-select" [(ngModel)]="m.driverId" name="drv">
          <option value="">— später zuweisen —</option>
          <option *ngFor="let d of drivers()" [value]="d.id">{{ d.fullName }}</option>
        </select></div>
    </div>

    <div class="err" *ngIf="error()">{{ error() }}</div>
    <div class="actions">
      <a routerLink="/routen" class="ap-btn ap-btn--ghost">Abbrechen</a>
      <button class="ap-btn ap-btn--primary" type="submit" [disabled]="busy()">{{ busy() ? 'Speichern…' : 'Route anlegen' }}</button>
    </div>
  </form>
  </div>`,
  styles: [`
    .back { display:inline-block; margin-bottom:14px; color:var(--c-text-muted); font-weight:600; font-size:13px; }
    .form { padding:24px; max-width:760px; }
    .row { display:grid; grid-template-columns:1fr 1fr; gap:18px; }
    .hint { display:block; margin-top:6px; }
    .err { background:var(--c-problem-soft); color:var(--c-problem); padding:10px 12px; border-radius:10px; font-size:13px; margin-bottom:14px; font-weight:600; }
    .actions { display:flex; justify-content:flex-end; gap:10px; margin-top:6px; }
    @media (max-width:640px){ .row { grid-template-columns:1fr; } }
  `],
})
export class RouteCreateComponent implements OnInit {
  wasteTypes = signal<WasteType[]>([]);
  vehicles = signal<Vehicle[]>([]);
  drivers = signal<Driver[]>([]);
  busy = signal(false);
  error = signal('');
  wl = wasteCategoryLabel;
  m: any = { routeNumber: '', name: '', date: new Date().toISOString().slice(0, 10), wasteTypeId: '', vehicleId: '', driverId: '' };

  constructor(private api: ApiService, private router: Router) {}
  ngOnInit() {
    this.api.wasteTypes({ pageSize: 100 }).subscribe(p => this.wasteTypes.set(p.items));
    this.api.vehicles({ pageSize: 100 }).subscribe(p => this.vehicles.set(p.items));
    this.api.drivers({ pageSize: 100 }).subscribe(p => this.drivers.set(p.items));
  }
  submit() {
    this.busy.set(true); this.error.set('');
    const body = { ...this.m, vehicleId: this.m.vehicleId || null, driverId: this.m.driverId || null };
    this.api.createRoute(body).subscribe({
      next: r => this.router.navigate(['/routen', r.id]),
      error: e => { this.busy.set(false); this.error.set(e?.error?.detail || 'Route konnte nicht angelegt werden.'); },
    });
  }
}
