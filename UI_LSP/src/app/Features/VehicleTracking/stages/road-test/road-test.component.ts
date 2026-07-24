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
  STAGE,
  StageSavePayload,
  VehicleDefectEntryPayload,
  VehicleDetails,
} from '../../vehicle-tracking.models';
import { DefectEntryFormComponent } from '../../shared/defect-entry-form/defect-entry-form.component';

@Component({
  selector: 'app-road-test',
  standalone: true,
  imports: [CommonModule, FormsModule, DefectEntryFormComponent],
  templateUrl: './road-test.component.html',
})
export class RoadTestComponent implements OnInit {
  @Input() vehicle: VehicleDetails | null = null;
  @Input() locked = false;
  @Output() stageSaved = new EventEmitter<{ auditAreaId: number; noDefect: boolean }>();

  @ViewChild(DefectEntryFormComponent) defectForm?: DefectEntryFormComponent;

  readonly vehicleService = inject(VehicleTrackingService);
  readonly toast = inject(ToastService);

  readonly AUDIT_AREA_ID = STAGE.ROAD_TEST;
  readonly STAGE_LABEL = 'Road Test';

  auditors: AuditorItem[] = [];
  selectedAuditorId = 0;
  concerns: ConcernRow[] = [];

  /** Two-way bound to the KM inputs. Stored as numbers — null when blank. */
  startKm: number | null = null;
  endKm: number | null = null;
  savingKm = false;

  saving = false;
  savingNoDefect = false;

  /** Track_Logs.Log_Date for this stage — shown in the header next to VIN.
   *  Set on first load + refreshed locally on every successful save. */
  logDate: string | null = null;

  ngOnInit(): void {
    // Load auditors first; once we have them, fetch any previously-saved
    // stage data so we can pre-select the auditor + rehydrate the concerns table.
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
          // Pre-select auditor by matching the saved name against the loaded master list.
          const savedName: string | undefined = data?.TrackLog?.Auditor_Name;
          if (savedName) {
            const match = this.auditors.find((a) => a.Auditor_Name === savedName);
            if (match) this.selectedAuditorId = match.Auditor_ID;
          }

          // Header stamp — when this stage was last saved.
          this.logDate = data?.TrackLog?.Log_Date ?? null;

          // Prefill the KM inputs from the saved Track_Logs row.
          const sk = data?.TrackLog?.Start_KM;
          const ek = data?.TrackLog?.End_KM;
          this.startKm = sk != null ? Number(sk) : null;
          this.endKm = ek != null ? Number(ek) : null;

          // Populate concerns table from the joined API result.
          const rows: any[] = data?.Concerns ?? [];
          this.concerns = rows.map((c) => ({
            Defect_Entry_ID: Number(c.Defect_Entry_ID),
            Vehicle_Area_ID: c.Vehicle_Area_ID != null ? Number(c.Vehicle_Area_ID) : undefined,
            Vehicle_Area_Name: c.Vehicle_Area_Name || undefined,
            Part_ID: Number(c.Part_ID),
            Part_Name: c.Part_Name,
            Defect_ID: Number(c.Defect_ID),
            Defect_Name: c.Defect_Name,
            Location_ID: Number(c.Location_ID),
            Location_Name: c.Location_Name,
            Severity_ID: c.Severity_ID != null ? Number(c.Severity_ID) : 0,
            Severity_Code: c.Severity_Code || '',
            Attribution_ID: c.Attribution_ID != null ? Number(c.Attribution_ID) : 0,
            Attribution_Name: c.Attribution_Name || '',
            Defect_Type_ID: c.Defect_Type_ID != null ? Number(c.Defect_Type_ID) : 0,
            Defect_Type_Name: c.Defect_Type_Name || '',
            Remark: c.Remark || undefined,
            Rework_Done: c.Rework_Done === true,
          }));
        },
      });
  }

  get selectedAuditorName(): string {
    return (
      this.auditors.find((a) => a.Auditor_ID === this.selectedAuditorId)?.Auditor_Name ?? ''
    );
  }

  /** Fires when the auditor dropdown changes. If a Track_Logs row already
   *  exists for this (Vehicle, Stage), we update Auditor_Name in place. If
   *  no row exists yet, the API no-ops; the auditor will be saved when the
   *  first concern is added or No Defect is tapped. */
  /** True when End_KM is missing, Start_KM is missing, or End_KM >= Start_KM.
   *  Drives the input's error styling and gates KM autosave. */
  get isEndKmValid(): boolean {
    if (this.startKm == null || this.endKm == null) return true;
    return Number(this.endKm) >= Number(this.startKm);
  }

  /** Total kilometres driven during the road test. Returns null when either
   *  endpoint is missing or the values are invalid — keeps the UI honest. */
  get totalDriveKm(): number | null {
    if (this.startKm == null || this.endKm == null) return null;
    const total = Number(this.endKm) - Number(this.startKm);
    return total >= 0 ? total : null;
  }

  /** Autosaves on blur from the KM inputs. Skipped when End_KM < Start_KM
   *  so we don't persist invalid trip data. */
  onKmBlur() {
    if (this.locked || !this.vehicle?.Vehicle_ID || this.savingKm) return;

    if (!this.isEndKmValid) {
      this.toast.showWarning(
        'Invalid KM',
        'End KM cannot be less than Start KM.',
      );
      return;
    }

    this.savingKm = true;
    this.vehicleService
      .updateRoadTestKm(this.vehicle.Vehicle_ID, this.startKm, this.endKm)
      .subscribe({
        next: () => {
          this.savingKm = false;
          this.logDate = new Date().toISOString();
        },
        error: () => (this.savingKm = false),
      });
  }

  onAuditorChange() {
    if (this.locked) return;
    const name = this.selectedAuditorName;
    if (!name || !this.vehicle?.Vehicle_ID) return;

    this.vehicleService
      .updateAuditor(this.vehicle.Vehicle_ID, this.AUDIT_AREA_ID, name)
      .subscribe({
        next: (updated: any) => {
          // updated is true|false from the API's datalist
          if (updated === true || updated?.datalist === true) {
            this.toast.showSuccess('Auditor updated', name);
          }
        },
      });
  }

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

  private baseStagePayload(): Omit<StageSavePayload, 'Concerns' | 'Is_No_Defect'> {
    return {
      Vehicle_ID: this.vehicle?.Vehicle_ID ?? 0,
      Audit_Area_ID: this.AUDIT_AREA_ID,
      Auditor_Name: this.selectedAuditorName,
      Documents: null,
      Inserted_Host: localStorage.getItem('Hostname') ?? '',
      Inserted_User_ID: Number(localStorage.getItem('Employee_ID')) || undefined,
      Plant_Code: localStorage.getItem('Plant_Code') ?? '',
    };
  }

  private guardSaveable(): boolean {
    if (this.locked) return false;
    if (!this.selectedAuditorId) {
      this.toast.showWarning('Auditor required', 'Please select an auditor first.');
      return false;
    }
    if (!this.vehicle?.Vehicle_ID) {
      this.toast.showWarning('Vehicle missing', 'Vehicle is not saved yet.');
      return false;
    }
    return true;
  }

  // ─── Direct per-row save ───────────────────────────────────────────────

  onAddRequest(row: ConcernRow) {
    if (!this.guardSaveable()) return;

    this.saving = true;
    const payload: StageSavePayload = {
      ...this.baseStagePayload(),
      Is_No_Defect: false,
      Concerns: [this.toPayload(row)],
    };

    this.vehicleService.addConcern(payload).subscribe({
      next: (res: any) => {
        this.saving = false;
        const datalist = res?.datalist ?? res;
        const newId = datalist?.Defect_Entry_ID ?? datalist?.defect_Entry_ID;
        const saved: ConcernRow = { ...row, Defect_Entry_ID: newId };
        this.concerns = [...this.concerns, saved];
        this.logDate = new Date().toISOString();
        this.toast.showSuccess('Saved', `Concern #${this.concerns.length} added.`);

        // Only clear the picker after a successful save.
        this.defectForm?.resetAfterAdd();

        // First successful add marks the stage as completed in the shell.
        if (this.concerns.length === 1) {
          this.stageSaved.emit({ auditAreaId: this.AUDIT_AREA_ID, noDefect: false });
        }
      },
      error: () => (this.saving = false),
    });
  }

  onRemoveRequest(evt: { index: number; row: ConcernRow }) {
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

  onUpdateRequest(evt: { defectEntryId: number; row: ConcernRow }) {
    if (!this.guardSaveable()) return;

    this.saving = true;
    const payload: StageSavePayload = {
      ...this.baseStagePayload(),
      Is_No_Defect: false,
      Concerns: [this.toPayload(evt.row)],
    };

    this.vehicleService.updateConcern(evt.defectEntryId, payload).subscribe({
      next: () => {
        this.saving = false;
        // Splice the updated row into the existing list (keep order).
        this.concerns = this.concerns.map((c) =>
          c.Defect_Entry_ID === evt.defectEntryId ? { ...evt.row, Defect_Entry_ID: evt.defectEntryId } : c,
        );
        this.logDate = new Date().toISOString();
        this.toast.showSuccess('Updated', 'Concern updated.');
        this.defectForm?.resetAfterUpdate();
      },
      error: () => (this.saving = false),
    });
  }

  // ─── No Defect path ────────────────────────────────────────────────────

  onNoDefect() {
    if (!this.guardSaveable()) return;
    this.savingNoDefect = true;
    const payload: StageSavePayload = {
      ...this.baseStagePayload(),
      Is_No_Defect: true,
      Concerns: [],
    };
    this.vehicleService.saveNoDefect(payload).subscribe({
      next: () => {
        this.savingNoDefect = false;
        this.logDate = new Date().toISOString();
        this.toast.showSuccess(
          'No Defect Recorded',
          `${this.STAGE_LABEL} marked as No Defect by ${this.selectedAuditorName}.`,
        );
        this.stageSaved.emit({ auditAreaId: this.AUDIT_AREA_ID, noDefect: true });
      },
      error: () => (this.savingNoDefect = false),
    });
  }
}
