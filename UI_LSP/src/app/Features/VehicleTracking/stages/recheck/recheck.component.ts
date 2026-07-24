import { CommonModule } from '@angular/common';
import {
  Component,
  EventEmitter,
  inject,
  Input,
  OnInit,
  Output,
  ViewChild,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ToastService } from '../../../../services';
import { VehicleTrackingService } from '../../vehicle-tracking.service';
import {
  AuditorItem,
  ConcernRow,
  ModelItem,
  STAGE,
  StageSavePayload,
  StageStatusPayload,
  VehicleDefectEntryPayload,
  VehicleDetails,
} from '../../vehicle-tracking.models';
import { DefectEntryFormComponent } from '../../shared/defect-entry-form/defect-entry-form.component';
import { AppConfig } from '../../../../config/app.config.service';
import {
  AlertData,
  TeamsWebhookService,
} from '../../../../services/teamswebhook.service';

/** Folders that hold uploaded files for the status stages — must match the
 *  server-side whitelist used by Upload/Download endpoints. */
const FOLDER_BY_AREA: Record<number, string> = {
  [STAGE.DTC]: 'DTCData',
  [STAGE.ECOS]: 'ECOSData',
  [STAGE.VMS]: 'VMSData',
};

interface StatusStageView {
  Audit_Area_ID: number;
  Audit_Area_Name: string;
  Auditor_Name: string;
  /** true = OK, false = NOK, null = not saved yet */
  Is_No_Defect: boolean | null;
  Log_Date: string | null;
  documents: string[];
}

@Component({
  selector: 'app-recheck',
  standalone: true,
  imports: [CommonModule, FormsModule, DefectEntryFormComponent],
  templateUrl: './recheck.component.html',
})
export class RecheckComponent implements OnInit {
  @Input() vehicle: VehicleDetails | null = null;
  @Input() locked = false;
  @Output() stageSaved = new EventEmitter<{
    auditAreaId: number;
    noDefect: boolean;
  }>();

  @ViewChild(DefectEntryFormComponent) defectForm?: DefectEntryFormComponent;

  readonly vehicleService = inject(VehicleTrackingService);
  readonly toast = inject(ToastService);
  readonly appConfig = inject(AppConfig);
  readonly webhookService = inject(TeamsWebhookService);

  readonly AUDIT_AREA_ID = STAGE.RECHECK;
  readonly STAGE_LABEL = 'Recheck';

  auditors: AuditorItem[] = [];
  selectedAuditorId = 0;

  /** All concerns on the vehicle, across stages. */
  concerns: ConcernRow[] = [];
  /** DTC / ECOS / VMS status rows. */
  statusStages: StatusStageView[] = [];

  /** Modal state — open when a stage's files icon is tapped. */
  filesModalOpen = false;
  filesModalStage: StatusStageView | null = null;

  saving = false;

  /** Track_Logs.Log_Date for the Recheck stage — shown in the header next to VIN. */
  logDate: string | null = null;

  /** Model master — used to resolve the proper Model_Name for the Teams
   *  alert card (vehicle only carries Model_ID + raw Drona Model_Desc). */
  models: ModelItem[] = [];

  ngOnInit(): void {
    this.vehicleService.getAuditorsByArea(this.AUDIT_AREA_ID).subscribe({
      next: (list) => {
        this.auditors = list ?? [];
        this.loadRecheckData();
        this.loadOwnTrackLog();
      },
    });
    this.vehicleService.getModels().subscribe({
      next: (list) => (this.models = list ?? []),
    });
  }

  /** Recheck's own Track_Logs row (auditor saved on previous visit). */
  private loadOwnTrackLog() {
    if (!this.vehicle?.Vehicle_ID) return;
    this.vehicleService
      .getStageData(this.vehicle.Vehicle_ID, this.AUDIT_AREA_ID)
      .subscribe({
        next: (data: any) => {
          this.logDate = data?.TrackLog?.Log_Date ?? null;
          const savedName: string | undefined = data?.TrackLog?.Auditor_Name;
          if (!savedName) return;
          const match = this.auditors.find((a) => a.Auditor_Name === savedName);
          if (match) this.selectedAuditorId = match.Auditor_ID;
        },
      });
  }

  private loadRecheckData() {
    if (!this.vehicle?.Vehicle_ID) return;

    this.vehicleService.getRecheckData(this.vehicle.Vehicle_ID).subscribe({
      next: (data: any) => {
        // Concerns
        const rows: any[] = data?.Concerns ?? [];
        this.concerns = rows.map((c) => ({
          Defect_Entry_ID: Number(c.Defect_Entry_ID),
          Audit_Area_ID: Number(c.Audit_Area_ID),
          Audit_Area_Name: c.Audit_Area_Name,
          Vehicle_Area_ID:
            c.Vehicle_Area_ID != null ? Number(c.Vehicle_Area_ID) : undefined,
          Vehicle_Area_Name: c.Vehicle_Area_Name || undefined,
          Part_ID: Number(c.Part_ID),
          Part_Name: c.Part_Name,
          Defect_ID: Number(c.Defect_ID),
          Defect_Name: c.Defect_Name,
          Location_ID: Number(c.Location_ID),
          Location_Name: c.Location_Name,
          Severity_ID: c.Severity_ID != null ? Number(c.Severity_ID) : 0,
          Severity_Code: c.Severity_Code || '',
          Attribution_ID:
            c.Attribution_ID != null ? Number(c.Attribution_ID) : 0,
          Attribution_Name: c.Attribution_Name || '',
          Defect_Type_ID:
            c.Defect_Type_ID != null ? Number(c.Defect_Type_ID) : 0,
          Defect_Type_Name: c.Defect_Type_Name || '',
          Remark: c.Remark || undefined,
          Rework_Done: c.Rework_Done === true,
        }));

        // Status stages
        const stages: any[] = data?.StatusStages ?? [];
        this.statusStages = stages.map((s) => ({
          Audit_Area_ID: Number(s.Audit_Area_ID),
          Audit_Area_Name: s.Audit_Area_Name,
          Auditor_Name: s.Auditor_Name || '',
          Is_No_Defect:
            typeof s.Is_No_Defect === 'boolean' ? s.Is_No_Defect : null,
          Log_Date: s.Log_Date ?? null,
          documents: this.parseDocs(s.Documents),
        }));
      },
    });
  }

  private parseDocs(raw: unknown): string[] {
    if (typeof raw !== 'string' || raw.trim().length === 0) return [];
    try {
      const parsed = JSON.parse(raw);
      return Array.isArray(parsed)
        ? parsed.filter((x) => typeof x === 'string')
        : [raw];
    } catch {
      return [raw];
    }
  }

  // ─── Auditor handling ──────────────────────────────────────────────────

  get selectedAuditorName(): string {
    return (
      this.auditors.find((a) => a.Auditor_ID === this.selectedAuditorId)
        ?.Auditor_Name ?? ''
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

  // ─── Edit / delete on a concern — re-uses the shared defect-entry-form ─

  private toPayload(c: ConcernRow): VehicleDefectEntryPayload {
    return {
      Vehicle_Area_ID: c.Vehicle_Area_ID ?? null,
      Part_ID: c.Part_ID,
      Defect_ID: c.Defect_ID,
      Location_ID: c.Location_ID,
      Severity_ID: c.Severity_ID,
      Attribution_ID: c.Attribution_ID,
      Defect_Type_ID: c.Defect_Type_ID,
      Remark: c.Remark ?? null,
    };
  }

  onUpdateRequest(evt: { defectEntryId: number; row: ConcernRow }) {
    if (this.locked) return;
    if (!this.vehicle?.Vehicle_ID) return;

    // Use the ORIGINAL stage's Audit_Area_ID — the edit belongs to it, not Recheck.
    const original = this.concerns.find(
      (c) => c.Defect_Entry_ID === evt.defectEntryId,
    );
    const auditAreaId = original?.Audit_Area_ID ?? this.AUDIT_AREA_ID;

    this.saving = true;
    const payload: StageSavePayload = {
      Vehicle_ID: this.vehicle.Vehicle_ID,
      Audit_Area_ID: auditAreaId,
      Auditor_Name:
        this.selectedAuditorName || (original?.Audit_Area_Name ?? 'Recheck'),
      Is_No_Defect: false,
      Documents: null,
      Inserted_Host: localStorage.getItem('Hostname') ?? '',
      Inserted_User_ID:
        Number(localStorage.getItem('Employee_ID')) || undefined,
      Plant_Code: localStorage.getItem('Plant_Code') ?? '',
      Concerns: [this.toPayload(evt.row)],
    };

    this.vehicleService.updateConcern(evt.defectEntryId, payload).subscribe({
      next: () => {
        this.saving = false;
        this.concerns = this.concerns.map((c) =>
          c.Defect_Entry_ID === evt.defectEntryId
            ? {
                ...c,
                ...evt.row,
                Defect_Entry_ID: evt.defectEntryId,
                Audit_Area_ID: c.Audit_Area_ID,
                Audit_Area_Name: c.Audit_Area_Name,
              }
            : c,
        );
        this.toast.showSuccess('Updated', 'Concern updated.');
        this.defectForm?.resetAfterUpdate();
      },
      error: () => (this.saving = false),
    });
  }

  onRemoveRequest(evt: { index: number; row: ConcernRow }) {
    if (this.locked) return;
    if (!evt.row.Defect_Entry_ID) {
      this.concerns = this.concerns.filter((_, i) => i !== evt.index);
      return;
    }
    this.vehicleService.deleteConcern(evt.row.Defect_Entry_ID).subscribe({
      next: () => {
        this.concerns = this.concerns.filter((_, i) => i !== evt.index);
        this.toast.showSuccess('Removed', 'Concern deleted.');
      },
    });
  }

  // ─── Status-stage files modal ──────────────────────────────────────────

  openFilesModal(stage: StatusStageView) {
    this.filesModalStage = stage;
    this.filesModalOpen = true;
  }

  closeFilesModal() {
    this.filesModalOpen = false;
    this.filesModalStage = null;
  }

  folderFor(auditAreaId: number): string {
    return FOLDER_BY_AREA[auditAreaId] ?? 'StageData';
  }

  displayNameOf(savedName: string): string {
    const idx = savedName.indexOf('_');
    return idx >= 0 && idx < savedName.length - 1
      ? savedName.substring(idx + 1)
      : savedName;
  }

  downloadUrlOf(auditAreaId: number, savedName: string): string {
    const base = (this.appConfig.baseApiPath || '').replace(/\/$/, '');
    return `${base}/MM_Vehicle_Tracking/DownloadStageFile?folder=${encodeURIComponent(
      this.folderFor(auditAreaId),
    )}&fileName=${encodeURIComponent(savedName)}`;
  }

  isImage(savedName: string): boolean {
    return /\.(jpe?g|png|gif|bmp|webp)$/i.test(savedName);
  }

  statusLabel(s: StatusStageView): string {
    if (s.Is_No_Defect === true) return 'OK';
    if (s.Is_No_Defect === false) return 'Not OK';
    return 'Pending';
  }

  statusCls(s: StatusStageView): string {
    if (s.Is_No_Defect === true) return 'bg-emerald-100 text-emerald-800';
    if (s.Is_No_Defect === false) return 'bg-red-100 text-red-800';
    return 'bg-gray-100 text-gray-600';
  }

  // ─── Save Recheck (Track_Logs only, Is_No_Defect = 1) ──────────────────

  canSave(): boolean {
    return (
      !this.locked &&
      !!this.selectedAuditorId &&
      !!this.vehicle?.Vehicle_ID &&
      !this.saving
    );
  }

  save() {
    if (this.locked) return;
    if (!this.canSave()) {
      this.toast.showWarning(
        'Auditor required',
        'Please select a Recheck auditor.',
      );
      return;
    }
    this.saving = true;
    const payload: StageStatusPayload = {
      Vehicle_ID: this.vehicle!.Vehicle_ID,
      Audit_Area_ID: this.AUDIT_AREA_ID,
      Auditor_Name: this.selectedAuditorName,
      Is_No_Defect: true, // Recheck itself doesn't carry defects of its own
      Documents: [],
      Inserted_Host: localStorage.getItem('Hostname') ?? '',
      Inserted_User_ID:
        Number(localStorage.getItem('Employee_ID')) || undefined,
      Plant_Code: localStorage.getItem('Plant_Code') ?? '',
    };

    this.vehicleService.saveStageStatus(payload).subscribe({
      next: () => {
        this.saving = false;
        this.logDate = new Date().toISOString();
        this.toast.showSuccess(
          'Recheck saved',
          `Verified by ${this.selectedAuditorName}.`,
        );
        this.stageSaved.emit({
          auditAreaId: this.AUDIT_AREA_ID,
          noDefect: true,
        });
        this.triggerAlert();
      },
      error: () => (this.saving = false),
    });
  }

  triggerAlert() {
    this.concerns.forEach((a) => {
      if (a.Severity_Code == 'V1+' || a.Severity_Code == 'V1') {
        const alertData: AlertData = {
          vinNo: this.vehicle?.VIN_No || 'Unknown VIN',
          model: this.modelName(),
          variant: this.vehicle?.Model_Desc || '—',
          severity: a.Severity_Code as 'V1+' | 'V1',
          concern: this.buildConcernText(a),
        };
        this.webhookService.sendAlert(alertData).subscribe({
          next: () => {
            console.log('Alert sent successfully.');
          },
          error: (err) => {
            console.error('Error sending alert:', err);
          },
        });
      }
    });
  }

  /** Proper model name from the Model master; falls back to the raw Drona
   *  description when the master has no match. */
  private modelName(): string {
    const match = this.models.find(
      (m) => m.Model_ID === this.vehicle?.Model_ID,
    );
    return match?.Model_Name || this.vehicle?.Model_Desc || 'Unknown Model';
  }

  /** Human-readable concern sentence for the Teams card, e.g.:
   *  "Leackage observed on Door (LH) in the Front Side area — reported
   *   during Static Audit. Remark: testing" */
  private buildConcernText(c: ConcernRow): string {
    if (!c.Defect_Name && !c.Part_Name) return 'Concern details not available.';

    let text = `${c.Defect_Name || 'Defect'} observed on ${c.Part_Name || 'a part'}`;
    if (c.Location_Name) text += ` (${c.Location_Name})`;
    if (c.Vehicle_Area_Name) text += ` in the ${c.Vehicle_Area_Name} area`;
    if (c.Audit_Area_Name) text += ` — reported during ${c.Audit_Area_Name}`;
    text += '.';
    if (c.Remark) text += ` Remark: ${c.Remark}`;
    return text;
  }
}
