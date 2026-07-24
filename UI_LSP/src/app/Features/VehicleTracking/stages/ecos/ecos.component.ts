import { CommonModule } from '@angular/common';
import {
  Component,
  ElementRef,
  EventEmitter,
  HostListener,
  inject,
  Input,
  OnInit,
  Output,
  ViewChild,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ConfirmationService, ToastService } from '../../../../services';
import { VehicleTrackingService } from '../../vehicle-tracking.service';
import {
  AuditorItem,
  STAGE,
  StageStatusPayload,
  VehicleDetails,
} from '../../vehicle-tracking.models';
import { AppConfig } from '../../../../config/app.config.service';

/** Server-side whitelisted folder where ECOS uploads land. */
const UPLOAD_FOLDER = 'ECOSData';
/** Hard limit per PPT: max 10 images per stage. */
const MAX_FILES = 10;

@Component({
  selector: 'app-ecos',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './ecos.component.html',
})
export class EcosComponent implements OnInit {
  @Input() vehicle: VehicleDetails | null = null;
  @Input() locked = false;
  @Output() stageSaved = new EventEmitter<{ auditAreaId: number; noDefect: boolean }>();

  @ViewChild('fileInput') fileInput?: ElementRef<HTMLInputElement>;

  readonly vehicleService = inject(VehicleTrackingService);
  readonly toast = inject(ToastService);
  readonly confirmationService = inject(ConfirmationService);
  readonly appConfig = inject(AppConfig);

  readonly AUDIT_AREA_ID = STAGE.ECOS;
  readonly STAGE_LABEL = 'ECOS';
  readonly UPLOAD_FOLDER = UPLOAD_FOLDER;
  readonly MAX_FILES = MAX_FILES;

  auditors: AuditorItem[] = [];
  selectedAuditorId = 0;
  status: 'OK' | 'NOK' | null = null;
  documents: string[] = [];

  saving = false;
  uploading = false;
  isDragging = false;

  /** Track_Logs.Log_Date for this stage — shown in the header next to VIN. */
  logDate: string | null = null;

  ngOnInit(): void {
    this.vehicleService.getAuditorsByArea(this.AUDIT_AREA_ID).subscribe({
      next: (list) => {
        this.auditors = list ?? [];
        this.loadExistingStageData();
      },
    });
  }

  private loadExistingStageData() {
    if (!this.vehicle?.Vehicle_ID) return;

    this.vehicleService
      .getStageData(this.vehicle.Vehicle_ID, this.AUDIT_AREA_ID)
      .subscribe({
        next: (data: any) => {
          const log = data?.TrackLog;
          if (!log) return;

          this.logDate = log.Log_Date ?? null;

          const savedName: string | undefined = log.Auditor_Name;
          if (savedName) {
            const match = this.auditors.find((a) => a.Auditor_Name === savedName);
            if (match) this.selectedAuditorId = match.Auditor_ID;
          }

          if (typeof log.Is_No_Defect === 'boolean') {
            this.status = log.Is_No_Defect ? 'OK' : 'NOK';
          }

          const docsRaw = log.Documents;
          if (typeof docsRaw === 'string' && docsRaw.trim().length > 0) {
            try {
              const parsed = JSON.parse(docsRaw);
              if (Array.isArray(parsed)) {
                this.documents = parsed.filter((x) => typeof x === 'string');
              }
            } catch {
              this.documents = [docsRaw];
            }
          }
        },
      });
  }

  get selectedAuditorName(): string {
    return (
      this.auditors.find((a) => a.Auditor_ID === this.selectedAuditorId)?.Auditor_Name ?? ''
    );
  }

  onAuditorChange() {
    if (this.locked) return;
    const name = this.selectedAuditorName;
    if (!name || !this.vehicle?.Vehicle_ID) return;

    this.vehicleService
      .updateAuditor(this.vehicle.Vehicle_ID, this.AUDIT_AREA_ID, name)
      .subscribe({
        next: (updated: any) => {
          if (updated === true || updated?.datalist === true) {
            this.toast.showSuccess('Auditor updated', name);
          }
        },
      });
  }

  pickStatus(value: 'OK' | 'NOK') {
    if (this.locked) return;
    this.status = value;
  }

  // ─── File uploads (picker + drag-drop + paste) ─────────────────────────

  onFilesChosen(evt: Event) {
    const input = evt.target as HTMLInputElement;
    const files = input.files;
    if (!files || files.length === 0) return;
    this.handleIncomingFiles(Array.from(files));
    input.value = '';
  }

  private handleIncomingFiles(files: File[]) {
    if (this.locked) return;
    if (!files.length) return;

    const remaining = MAX_FILES - this.documents.length;
    if (remaining <= 0) {
      this.toast.showWarning('Limit reached', `Max ${MAX_FILES} files allowed.`);
      return;
    }
    const toUpload = files.slice(0, remaining);
    if (files.length > toUpload.length) {
      this.toast.showWarning(
        'Limit reached',
        `Only the first ${toUpload.length} file(s) will be uploaded.`,
      );
    }
    this.uploadSequentially(toUpload);
  }

  private async uploadSequentially(files: File[]) {
    this.uploading = true;
    try {
      for (const file of files) {
        try {
          const savedName: any = await this.vehicleService
            .uploadStageFile(file, UPLOAD_FOLDER)
            .toPromise();
          if (typeof savedName === 'string' && savedName.length > 0) {
            this.documents = [...this.documents, savedName];
          } else if (savedName?.datalist && typeof savedName.datalist === 'string') {
            this.documents = [...this.documents, savedName.datalist];
          }
        } catch {
          this.toast.showError('Upload failed', file.name);
        }
      }
      if (this.documents.length > 0) {
        this.toast.showSuccess('Uploaded', `${this.documents.length} file(s) ready.`);
      }
    } finally {
      this.uploading = false;
    }
  }

  onDragOver(evt: DragEvent) {
    evt.preventDefault();
    evt.stopPropagation();
    if (!this.uploading) this.isDragging = true;
  }

  onDragLeave(evt: DragEvent) {
    evt.preventDefault();
    evt.stopPropagation();
    this.isDragging = false;
  }

  onDrop(evt: DragEvent) {
    evt.preventDefault();
    evt.stopPropagation();
    this.isDragging = false;
    const files = evt.dataTransfer?.files;
    if (!files || files.length === 0) return;
    this.handleIncomingFiles(Array.from(files));
  }

  @HostListener('paste', ['$event'])
  onPaste(evt: ClipboardEvent) {
    const items = evt.clipboardData?.items;
    if (!items || items.length === 0) return;

    const files: File[] = [];
    for (let i = 0; i < items.length; i++) {
      const item = items[i];
      if (item.kind === 'file') {
        const f = item.getAsFile();
        if (f) files.push(f);
      }
    }
    if (files.length === 0) return;
    evt.preventDefault();
    this.handleIncomingFiles(files);
  }

  removeFile(index: number) {
    if (this.locked) return;
    const name = this.documents[index];
    if (!name) return;

    this.confirmationService
      .confirmDelete(
        'Delete File',
        'Are you sure you want to remove this file? It will be deleted from the server.',
        this.displayNameOf(name),
        'Delete',
        'Cancel',
      )
      .then((confirmed) => {
        if (!confirmed) return;

        this.vehicleService.deleteStageFile(UPLOAD_FOLDER, name).subscribe({
          next: () => {
            this.documents = this.documents.filter((_, i) => i !== index);
            this.toast.showSuccess('Removed', this.displayNameOf(name));
          },
        });
      });
  }

  displayNameOf(savedName: string): string {
    const idx = savedName.indexOf('_');
    return idx >= 0 && idx < savedName.length - 1 ? savedName.substring(idx + 1) : savedName;
  }

  downloadUrlOf(savedName: string): string {
    const base = (this.appConfig.baseApiPath || '').replace(/\/$/, '');
    return `${base}/MM_Vehicle_Tracking/DownloadStageFile?folder=${encodeURIComponent(
      UPLOAD_FOLDER,
    )}&fileName=${encodeURIComponent(savedName)}`;
  }

  isImage(savedName: string): boolean {
    return /\.(jpe?g|png|gif|bmp|webp)$/i.test(savedName);
  }

  // ─── Save ──────────────────────────────────────────────────────────────

  canSave(): boolean {
    return (
      !this.locked &&
      !!this.selectedAuditorId &&
      this.status !== null &&
      !!this.vehicle?.Vehicle_ID &&
      !this.saving &&
      !this.uploading
    );
  }

  save() {
    if (this.locked) return;
    if (!this.canSave()) {
      if (!this.selectedAuditorId) {
        this.toast.showWarning('Auditor required', 'Please select an auditor first.');
      } else if (this.status === null) {
        this.toast.showWarning('Status required', 'Please choose OK or Not OK.');
      }
      return;
    }
    this.saving = true;
    const payload: StageStatusPayload = {
      Vehicle_ID: this.vehicle!.Vehicle_ID,
      Audit_Area_ID: this.AUDIT_AREA_ID,
      Auditor_Name: this.selectedAuditorName,
      Is_No_Defect: this.status === 'OK',
      Documents: this.documents,
      Inserted_Host: localStorage.getItem('Hostname') ?? '',
      Inserted_User_ID: Number(localStorage.getItem('Employee_ID')) || undefined,
      Plant_Code: localStorage.getItem('Plant_Code') ?? '',
    };

    this.vehicleService.saveStageStatus(payload).subscribe({
      next: () => {
        this.saving = false;
        this.logDate = new Date().toISOString();
        this.toast.showSuccess(
          'Saved',
          `${this.STAGE_LABEL} marked as ${this.status} by ${this.selectedAuditorName}.`,
        );
        this.stageSaved.emit({
          auditAreaId: this.AUDIT_AREA_ID,
          noDefect: this.status === 'OK',
        });
      },
      error: () => (this.saving = false),
    });
  }
}
