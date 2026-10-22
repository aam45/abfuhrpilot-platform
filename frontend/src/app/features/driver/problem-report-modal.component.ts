import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ProblemType, Stop } from '../../core/models';

export interface PhotoEntry { id?: string; name: string; objectUrl: string; uploading: boolean; error?: boolean; }

/**
 * Problem-report modal, extracted from ap-driver so the photo-evidence UI
 * (thumbnails, upload states) carries its own style budget — same
 * dumb-presentational-component pattern as ap-next-maneuver-card.
 */
@Component({
  selector: 'ap-problem-report-modal',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
  <div class="modal-bg" (click)="close()">
    <div class="modal ap-card" role="dialog" aria-modal="true" (click)="$event.stopPropagation()">
      <h3>Problem melden</h3>
      <p class="ap-muted">{{ stop?.address }}</p>
      <div class="ap-field">
        <label class="ap-label">Problemart</label>
        <select class="ap-select" [(ngModel)]="problemType">
          <option *ngFor="let p of problemTypes" [value]="p">{{ labels[p] }}</option>
        </select>
      </div>
      <div class="ap-field">
        <label class="ap-label">Anmerkung (optional)</label>
        <input class="ap-input" [(ngModel)]="problemNote" placeholder="z. B. Baustelle, Zufahrt versperrt" />
      </div>
      <div class="ap-field">
        <label class="ap-label">Fotos (optional)</label>
        <div class="photos">
          <div class="thumb" *ngFor="let p of photos; let i = index">
            <img [src]="p.objectUrl" alt="Foto" />
            <div class="thumb-over" *ngIf="p.uploading"><div class="mini-spin ap-keep-motion"></div></div>
            <div class="thumb-err" *ngIf="p.error">!</div>
            <button type="button" class="thumb-x" (click)="photoRemoved.emit(i)" aria-label="Foto entfernen">✕</button>
          </div>
          <label class="add-photo">
            <input type="file" accept="image/jpeg,image/png,image/webp" (change)="photoSelected.emit($event)" hidden />
            📷<span>Foto hinzufügen</span>
          </label>
        </div>
        <small class="ap-subtle">JPEG/PNG/WebP, max. 8 MB. Foto ist optional.</small>
      </div>
      <div class="modal-actions">
        <button class="ap-btn ap-btn--ghost" (click)="close()">Abbrechen</button>
        <button class="ap-btn ap-btn--danger" (click)="submit()" [disabled]="busy || uploading">
          {{ uploading ? 'Foto lädt…' : 'Problem melden' }}
        </button>
      </div>
    </div>
  </div>`,
  styles: [`
    .modal-bg { position:fixed; inset:0; background:rgba(16,24,40,.45); display:grid; place-items:center; z-index:50; padding:20px; }
    .modal { padding:24px; width:min(440px, 100%); max-height:90vh; overflow:auto; }
    .modal-actions { display:flex; justify-content:flex-end; gap:10px; margin-top:8px; }
    .modal-actions .ap-btn { min-height:56px; }
    .photos { display:flex; flex-wrap:wrap; gap:10px; }
    .thumb { position:relative; width:76px; height:76px; border-radius:10px; overflow:hidden; border:1px solid var(--c-border); }
    .thumb img { width:100%; height:100%; object-fit:cover; display:block; }
    .thumb-over { position:absolute; inset:0; background:rgba(255,255,255,.6); display:grid; place-items:center; }
    .thumb-err { position:absolute; inset:0; background:var(--c-problem-soft); color:var(--c-problem); display:grid; place-items:center; font-weight:800; }
    .thumb-x { position:absolute; top:2px; right:2px; width:20px; height:20px; border:none; border-radius:50%; background:rgba(0,0,0,.6); color:#fff; cursor:pointer; font-size:11px; line-height:1; }
    .mini-spin { width:20px; height:20px; border-radius:50%; border:3px solid var(--c-border); border-top-color:var(--c-primary); animation:spin .8s linear infinite; }
    .add-photo { width:76px; height:76px; border:1.5px dashed var(--c-border-strong); border-radius:10px; display:flex; flex-direction:column; align-items:center; justify-content:center; gap:2px; cursor:pointer; font-size:20px; color:var(--c-text-muted); }
    .add-photo span { font-size:10px; font-weight:600; }
    .add-photo:hover { background:var(--c-surface-2); }
    @keyframes spin { to { transform:rotate(360deg); } }
  `],
})
export class ProblemReportModalComponent implements OnChanges {
  @Input() stop: Stop | null = null;
  @Input() problemTypes: ProblemType[] = [];
  @Input() labels: Record<string, string> = {};
  @Input() photos: PhotoEntry[] = [];
  @Input() busy = false;
  @Input() uploading = false;

  @Output() closed = new EventEmitter<void>();
  @Output() submitted = new EventEmitter<{ type: ProblemType; note: string }>();
  @Output() photoSelected = new EventEmitter<Event>();
  @Output() photoRemoved = new EventEmitter<number>();

  problemType: ProblemType = 'BlockedByCar';
  problemNote = '';

  ngOnChanges(changes: SimpleChanges) {
    if (changes['stop'] && this.stop) {
      this.problemType = 'BlockedByCar';
      this.problemNote = '';
    }
  }

  close() { this.closed.emit(); }
  submit() { this.submitted.emit({ type: this.problemType, note: this.problemNote }); }
}
