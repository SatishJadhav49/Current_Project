import { CommonModule } from '@angular/common';
import {
  Component,
  EventEmitter,
  inject,
  Input,
  OnInit,
  Output,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ToastService } from '../../../../services';
import { VehicleTrackingService } from '../../vehicle-tracking.service';
import {
  AuditorItem,
  ReworkUpsertPayload,
  STAGE,
  StageReworkUpsertPayload,
  VehicleDetails,
} from '../../vehicle-tracking.models';
import { AppConfig } from '../../../../config/app.config.service';

/** A single row in the Rework table — concern data + its rework state. */
interface ReworkRow {
  Defect_Entry_ID: number;
  Audit_Area_ID: number;
  Audit_Area_Name: string;
  Vehicle_Area_Name?: string;
  Part_Name: string;
  Defect_Name: string;
  Location_Name: string;
  Severity_Code: string;
  Attribution_Name: string;
  Defect_Type_Name: string;
  Remark?: string;

  /** Rework state — edited via the per-row Rework modal, not inline. */
  Rework_Done: boolean;
  Rework_By_Id: number;
  Rework_By_Name: string;
  /** What was actually done during rework — multi-line, entered in the modal. */
  Rework_Remark: string;
  Rework_Date?: string | null;

  /** Per-row saving spinner state. */
  saving?: boolean;
}

/** Local editable copy used inside the concern-level rework modal. */
interface ReworkModalDraft {
  Rework_Done: boolean;
  Rework_By_Name: string;
  Rework_Remark: string;
}

/** Stage-level failure row — one per NOK stage (DTC / ECOS / VMS). */
interface StageFailureRow {
  Audit_Area_ID: number;
  Audit_Area_Name: string;
  Auditor_Name: string;
  Log_Date: string | null;
  /** Files the AUDITOR attached when marking the stage Not OK (Track_Logs.Documents). */
  Stage_Documents: string[];
  Rework_Done: boolean;
  Rework_By: string;
  Rework_Remark: string;
  Rework_Documents: string[];
  Rework_Date: string | null;
}

/** Upload folder per status stage — must match the server-side whitelist. */
const STAGE_FOLDER_BY_AREA: Record<number, string> = {
  [STAGE.DTC]: 'DTCData',
  [STAGE.ECOS]: 'ECOSData',
  [STAGE.VMS]: 'VMSData',
};

/** Editable copy used inside the stage-rework modal (adds documents). */
interface StageReworkModalDraft {
  Rework_Done: boolean;
  Rework_By_Name: string;
  Rework_Remark: string;
  Documents: string[];
}

const STAGE_REWORK_UPLOAD_FOLDER = 'StageReworkData';
const STAGE_REWORK_MAX_FILES = 10;

@Component({
  selector: 'app-rework',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './rework.component.html',
})
export class RewokComponent implements OnInit {
  @Input() vehicle: VehicleDetails | null = null;
  @Input() locked = false;
  /** Kept for shell symmetry, never emitted — Rework writes no Track_Logs row. */
  @Output() stageSaved = new EventEmitter<{ auditAreaId: number; noDefect: boolean }>();

  readonly vehicleService = inject(VehicleTrackingService);
  readonly toast = inject(ToastService);
  readonly appConfig = inject(AppConfig);

  readonly STAGE_REWORK_UPLOAD_FOLDER = STAGE_REWORK_UPLOAD_FOLDER;
  readonly STAGE_REWORK_MAX_FILES = STAGE_REWORK_MAX_FILES;

  readonly AUDIT_AREA_ID = STAGE.REWORK;
  readonly STAGE_LABEL = 'Rework';

  /** Auditor master rows for Audit_Area_ID = 9. Powers the per-row Rework_By
   *  dropdown. There is intentionally NO single "supervisor" selection — rework
   *  may be done by multiple teams, attributed per concern. */
  auditors: AuditorItem[] = [];
  rows: ReworkRow[] = [];

  /** NOK stage failures (DTC / ECOS / VMS / Paint) that need stage-level rework. */
  stageFailures: StageFailureRow[] = [];

  /** Most recent Rework_Date across all rework rows — shown in the header
   *  next to VIN. There is no Track_Logs row for stage 9, so we derive this
   *  from the per-concern rework dates instead. */
  logDate: string | null = null;

  ngOnInit(): void {
    this.vehicleService.getAuditorsByArea(this.AUDIT_AREA_ID).subscribe({
      next: (list) => {
        this.auditors = list ?? [];
        this.loadReworkData();
      },
    });
  }

  private loadReworkData() {
    if (!this.vehicle?.Vehicle_ID) return;

    this.vehicleService.getReworkData(this.vehicle.Vehicle_ID).subscribe({
      next: (data: any) => {
        const list: any[] = data?.Concerns ?? [];
        this.rows = list.map((c) => {
          const reworkByName: string = c.Rework_By || '';
          const matchedAuditor = reworkByName
            ? this.auditors.find((a) => a.Auditor_Name === reworkByName)
            : undefined;
          return {
            Defect_Entry_ID: Number(c.Defect_Entry_ID),
            Audit_Area_ID: Number(c.Audit_Area_ID),
            Audit_Area_Name: c.Audit_Area_Name,
            Vehicle_Area_Name: c.Vehicle_Area_Name || undefined,
            Part_Name: c.Part_Name,
            Defect_Name: c.Defect_Name,
            Location_Name: c.Location_Name,
            Severity_Code: c.Severity_Code || '',
            Attribution_Name: c.Attribution_Name || '',
            Defect_Type_Name: c.Defect_Type_Name || '',
            Remark: c.Remark || undefined,
            Rework_Done: c.Rework_Done === true,
            Rework_By_Id: matchedAuditor?.Auditor_ID ?? 0,
            Rework_By_Name: reworkByName,
            Rework_Remark: c.Rework_Remark || '',
            Rework_Date: c.Rework_Date ?? null,
          } as ReworkRow;
        });
        // Stage-level failures — NOK status stages that need rework.
        const failures: any[] = data?.StageFailures ?? [];
        this.stageFailures = failures.map((f) => ({
          Audit_Area_ID: Number(f.Audit_Area_ID),
          Audit_Area_Name: f.Audit_Area_Name,
          Auditor_Name: f.Auditor_Name || '',
          Log_Date: f.Log_Date ?? null,
          Stage_Documents: this.parseDocs(f.Stage_Documents),
          Rework_Done: f.Stage_Rework_Done === true,
          Rework_By: f.Stage_Rework_By || '',
          Rework_Remark: f.Stage_Rework_Remark || '',
          Rework_Documents: this.parseDocs(f.Stage_Rework_Documents),
          Rework_Date: f.Stage_Rework_Date ?? null,
        }));

        this.logDate = this.computeLatestReworkDate();
      },
    });
  }

  /** Parse a JSON-encoded array of file names as stored on the server. */
  private parseDocs(raw: unknown): string[] {
    if (typeof raw !== 'string' || raw.trim().length === 0) return [];
    try {
      const parsed = JSON.parse(raw);
      return Array.isArray(parsed) ? parsed.filter((x) => typeof x === 'string') : [];
    } catch {
      return [];
    }
  }

  private computeLatestReworkDate(): string | null {
    let latest = 0;
    for (const r of this.rows) {
      if (!r.Rework_Date) continue;
      const t = new Date(r.Rework_Date).getTime();
      if (!Number.isNaN(t) && t > latest) latest = t;
    }
    return latest > 0 ? new Date(latest).toISOString() : null;
  }

  // ─── Modal-driven save ─────────────────────────────────────────────────

  /** Row currently being edited in the modal (null = modal closed). */
  activeRow: ReworkRow | null = null;
  /** Working copy of the row's rework fields — mutated by the modal. */
  draft: ReworkModalDraft = { Rework_Done: false, Rework_By_Name: '', Rework_Remark: '' };
  savingModal = false;

  openReworkModal(row: ReworkRow) {
    if (this.locked) return;
    this.activeRow = row;
    this.draft = {
      Rework_Done: row.Rework_Done,
      Rework_By_Name: row.Rework_By_Name,
      Rework_Remark: row.Rework_Remark,
    };
  }

  closeModal() {
    this.activeRow = null;
    this.savingModal = false;
  }

  /** Save requires: Done ticked AND Rework_By present AND remark not empty.
   *  The remark documents WHAT was actually done — it's the auditable trail. */
  canSaveModal(): boolean {
    return (
      !this.savingModal &&
      this.draft.Rework_Done === true &&
      this.draft.Rework_By_Name.trim().length > 0 &&
      this.draft.Rework_Remark.trim().length > 0
    );
  }

  saveModal() {
    if (!this.activeRow) return;
    if (!this.canSaveModal()) {
      if (!this.draft.Rework_Done) {
        this.toast.showWarning('Mark Done', 'Tick the "Rework Done" checkbox.');
      } else if (this.draft.Rework_By_Name.trim().length === 0) {
        this.toast.showWarning('Rework By required', 'Enter or pick a name.');
      } else if (this.draft.Rework_Remark.trim().length === 0) {
        this.toast.showWarning('Remark required', 'Describe what rework was done.');
      }
      return;
    }

    const row = this.activeRow;
    const draft = this.draft;
    this.savingModal = true;

    const payload: ReworkUpsertPayload = {
      Defect_Entry_ID: row.Defect_Entry_ID,
      Rework_Done: draft.Rework_Done,
      Rework_By: draft.Rework_By_Name.trim(),
      Remark: draft.Rework_Remark.trim(),
      Inserted_Host: localStorage.getItem('Hostname') ?? '',
      Inserted_User_ID: Number(localStorage.getItem('Employee_ID')) || undefined,
      Plant_Code: localStorage.getItem('Plant_Code') ?? '',
    };

    this.vehicleService.upsertRework(payload).subscribe({
      next: () => {
        // Commit the draft into the row so the summary shows the new values.
        row.Rework_Done = draft.Rework_Done;
        row.Rework_By_Name = draft.Rework_By_Name.trim();
        row.Rework_Remark = draft.Rework_Remark.trim();
        row.Rework_Date = new Date().toISOString();
        this.logDate = row.Rework_Date;

        this.savingModal = false;
        this.toast.showSuccess(
          'Rework saved',
          `${row.Part_Name} — by ${row.Rework_By_Name}.`,
        );
        this.closeModal();
      },
      error: () => (this.savingModal = false),
    });
  }

  /** First non-empty line of the remark — shown compactly in the row summary. */
  remarkFirstLine(remark: string | undefined | null): string {
    if (!remark) return '';
    const line = remark.split(/\r?\n/).find((l) => l.trim().length > 0) ?? '';
    return line.length > 80 ? line.slice(0, 77) + '…' : line;
  }

  // ─── Stage-level rework modal (DTC / ECOS / VMS / Paint) ─────────────

  activeStageFailure: StageFailureRow | null = null;
  stageDraft: StageReworkModalDraft = {
    Rework_Done: false,
    Rework_By_Name: '',
    Rework_Remark: '',
    Documents: [],
  };
  savingStageModal = false;
  uploadingStageFiles = false;

  openStageReworkModal(row: StageFailureRow) {
    if (this.locked) return;
    this.activeStageFailure = row;
    this.stageDraft = {
      Rework_Done: row.Rework_Done,
      Rework_By_Name: row.Rework_By,
      Rework_Remark: row.Rework_Remark,
      Documents: [...row.Rework_Documents],
    };
  }

  closeStageModal() {
    this.activeStageFailure = null;
    this.savingStageModal = false;
    this.uploadingStageFiles = false;
  }

  canSaveStageModal(): boolean {
    return (
      !this.savingStageModal &&
      !this.uploadingStageFiles &&
      this.stageDraft.Rework_Done === true &&
      this.stageDraft.Rework_By_Name.trim().length > 0 &&
      this.stageDraft.Rework_Remark.trim().length > 0
    );
  }

  saveStageModal() {
    if (!this.activeStageFailure || !this.vehicle?.Vehicle_ID) return;
    if (!this.canSaveStageModal()) {
      if (!this.stageDraft.Rework_Done) {
        this.toast.showWarning('Mark Done', 'Tick the "Rework Done" checkbox.');
      } else if (this.stageDraft.Rework_By_Name.trim().length === 0) {
        this.toast.showWarning('Rework By required', 'Enter or pick a name.');
      } else if (this.stageDraft.Rework_Remark.trim().length === 0) {
        this.toast.showWarning('Remark required', 'Describe what rework was done.');
      }
      return;
    }

    const row = this.activeStageFailure;
    const draft = this.stageDraft;
    this.savingStageModal = true;

    const payload: StageReworkUpsertPayload = {
      Vehicle_ID: this.vehicle.Vehicle_ID,
      Audit_Area_ID: row.Audit_Area_ID,
      Rework_Done: draft.Rework_Done,
      Rework_By: draft.Rework_By_Name.trim(),
      Remark: draft.Rework_Remark.trim(),
      Documents: draft.Documents,
      Inserted_Host: localStorage.getItem('Hostname') ?? '',
      Inserted_User_ID: Number(localStorage.getItem('Employee_ID')) || undefined,
      Plant_Code: localStorage.getItem('Plant_Code') ?? '',
    };

    this.vehicleService.upsertStageRework(payload).subscribe({
      next: () => {
        row.Rework_Done = draft.Rework_Done;
        row.Rework_By = draft.Rework_By_Name.trim();
        row.Rework_Remark = draft.Rework_Remark.trim();
        row.Rework_Documents = [...draft.Documents];
        row.Rework_Date = new Date().toISOString();
        this.savingStageModal = false;
        this.toast.showSuccess(
          'Stage rework saved',
          `${row.Audit_Area_Name} resolved by ${row.Rework_By}.`,
        );
        this.closeStageModal();
      },
      error: () => (this.savingStageModal = false),
    });
  }

  // ─── File upload handlers for the stage-rework modal ───────────────────

  onStageFilesChosen(evt: Event) {
    const input = evt.target as HTMLInputElement;
    const files = input.files;
    if (!files || files.length === 0) return;
    this.uploadStageFiles(Array.from(files));
    input.value = '';
  }

  private async uploadStageFiles(files: File[]) {
    const remaining = STAGE_REWORK_MAX_FILES - this.stageDraft.Documents.length;
    if (remaining <= 0) {
      this.toast.showWarning('Limit reached', `Max ${STAGE_REWORK_MAX_FILES} files.`);
      return;
    }
    const toUpload = files.slice(0, remaining);
    this.uploadingStageFiles = true;
    try {
      for (const file of toUpload) {
        try {
          const savedName: any = await this.vehicleService
            .uploadStageFile(file, STAGE_REWORK_UPLOAD_FOLDER)
            .toPromise();
          const name = typeof savedName === 'string' ? savedName : savedName?.datalist;
          if (typeof name === 'string' && name.length > 0) {
            this.stageDraft.Documents = [...this.stageDraft.Documents, name];
          }
        } catch {
          this.toast.showError('Upload failed', file.name);
        }
      }
    } finally {
      this.uploadingStageFiles = false;
    }
  }

  removeStageDoc(index: number) {
    const name = this.stageDraft.Documents[index];
    if (!name) return;
    this.vehicleService.deleteStageFile(STAGE_REWORK_UPLOAD_FOLDER, name).subscribe({
      next: () => {
        this.stageDraft.Documents = this.stageDraft.Documents.filter((_, i) => i !== index);
      },
    });
  }

  displayNameOf(saved: string): string {
    const idx = saved.indexOf('_');
    return idx >= 0 && idx < saved.length - 1 ? saved.substring(idx + 1) : saved;
  }

  downloadUrlOf(saved: string): string {
    const base = (this.appConfig.baseApiPath || '').replace(/\/$/, '');
    return `${base}/MM_Vehicle_Tracking/DownloadStageFile?folder=${encodeURIComponent(
      STAGE_REWORK_UPLOAD_FOLDER,
    )}&fileName=${encodeURIComponent(saved)}`;
  }

  /** Download URL for a file the auditor attached to the FAILED STAGE itself
   *  (DTC/ECOS/VMS) — the folder differs per stage. */
  stageDocUrl(auditAreaId: number, saved: string): string {
    const folder = STAGE_FOLDER_BY_AREA[auditAreaId] ?? 'StageData';
    const base = (this.appConfig.baseApiPath || '').replace(/\/$/, '');
    return `${base}/MM_Vehicle_Tracking/DownloadStageFile?folder=${encodeURIComponent(
      folder,
    )}&fileName=${encodeURIComponent(saved)}`;
  }

  isImage(saved: string): boolean {
    return /\.(jpe?g|png|gif|bmp|webp)$/i.test(saved);
  }

  // ─── Files modal (same pattern as Recheck's Stage Status table) ─────────

  filesModalOpen = false;
  filesModalTitle = '';
  filesModalFiles: string[] = [];
  /** Resolves the download URL per file — bound when the modal is opened so
   *  one modal serves both stage-auditor files and rework files. */
  private filesModalUrlFn: (f: string) => string = () => '';

  /** Files the auditor attached when marking the stage Not OK. */
  openStageFilesModal(f: StageFailureRow) {
    this.filesModalTitle = `${f.Audit_Area_Name} — Auditor Files`;
    this.filesModalFiles = f.Stage_Documents;
    this.filesModalUrlFn = (doc) => this.stageDocUrl(f.Audit_Area_ID, doc);
    this.filesModalOpen = true;
  }

  /** Files attached with the stage rework entry. */
  openReworkFilesModal(f: StageFailureRow) {
    this.filesModalTitle = `${f.Audit_Area_Name} — Rework Files`;
    this.filesModalFiles = f.Rework_Documents;
    this.filesModalUrlFn = (doc) => this.downloadUrlOf(doc);
    this.filesModalOpen = true;
  }

  closeFilesModal() {
    this.filesModalOpen = false;
  }

  filesModalUrl(f: string): string {
    return this.filesModalUrlFn(f);
  }

  /** Header icon for each failing stage — matches the progress-bar emojis. */
  stageEmoji(auditAreaId: number): string {
    switch (auditAreaId) {
      case STAGE.DTC: return '💻';
      case STAGE.ECOS: return '⚡';
      case STAGE.VMS: return '🖥️';
      case STAGE.PAINT_VERIFICATION: return '🎨';
      default: return '⚠️';
    }
  }

  // ─── Counters for the header chip ─────────────────────────────────────

  get doneCount(): number {
    return this.rows.filter((r) => r.Rework_Done).length;
  }

  get pendingCount(): number {
    return this.rows.length - this.doneCount;
  }
}
