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
  selector: 'app-underbody',
  standalone: true,
  imports: [CommonModule, FormsModule, DefectEntryFormComponent],
  templateUrl: './underbody.component.html',
})
export class UnderbodyComponent implements OnInit {
  @Input() vehicle: VehicleDetails | null = null;
  @Input() locked = false;
  @Output() stageSaved = new EventEmitter<{ auditAreaId: number; noDefect: boolean }>();

  @ViewChild(DefectEntryFormComponent) defectForm?: DefectEntryFormComponent;

  readonly vehicleService = inject(VehicleTrackingService);
  readonly toast = inject(ToastService);

  readonly AUDIT_AREA_ID = STAGE.UNDERBODY;
  readonly STAGE_LABEL = 'Underbody';

  auditors: AuditorItem[] = [];
  selectedAuditorId = 0;
  concerns: ConcernRow[] = [];

  saving = false;
  savingNoDefect = false;

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
          const savedName: string | undefined = data?.TrackLog?.Auditor_Name;
          if (savedName) {
            const match = this.auditors.find((a) => a.Auditor_Name === savedName);
            if (match) this.selectedAuditorId = match.Auditor_ID;
          }

          this.logDate = data?.TrackLog?.Log_Date ?? null;

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

  onAuditorChange() {
    if (this.locked) return;
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

        this.defectForm?.resetAfterAdd();

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
        this.concerns = this.concerns.map((c) =>
          c.Defect_Entry_ID === evt.defectEntryId
            ? { ...evt.row, Defect_Entry_ID: evt.defectEntryId }
            : c,
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
