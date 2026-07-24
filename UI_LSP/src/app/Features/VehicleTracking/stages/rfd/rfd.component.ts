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
import { ConfirmationService, ToastService } from '../../../../services';
import { VehicleTrackingService } from '../../vehicle-tracking.service';
import {
  AuditorItem,
  STAGE,
  VehicleDetails,
} from '../../vehicle-tracking.models';

interface RfdStageCheck {
  Completed: boolean;
  AuditorName: string;
  LogDate: string | null;
}

interface RfdStatusCheck extends RfdStageCheck {
  IsOk: boolean | null;
  StageReworkDone?: boolean;
  StageReworkBy?: string | null;
  StageReworkRemark?: string | null;
  StageReworkDate?: string | null;
}

interface RfdReworkCheck {
  TotalConcerns: number;
  DoneCount: number;
  PendingCount: number;
}

interface RfdPendingConcern {
  Defect_Entry_ID: number;
  Audit_Area_Name: string;
  Vehicle_Area_Name: string;
  Part_Name: string;
  Defect_Name: string;
  Location_Name: string;
  Severity_Code: string;
  Attribution_Name: string;
  Defect_Type_Name: string;
  Remark: string | null;
}

interface RfdReworkedConcern extends RfdPendingConcern {
  Rework_By: string;
  Rework_Date: string | null;
}

interface RfdReadiness {
  Ready: boolean;
  RoadTest: RfdStageCheck;
  Underbody: RfdStageCheck;
  LspAudit: RfdStageCheck;
  Dtc: RfdStatusCheck;
  /** Informational — does NOT gate dispatch. */
  Ecos: RfdStatusCheck;
  /** Required — must be OK to gate dispatch. */
  Vms: RfdStatusCheck;
  /** Required — must be OK to gate dispatch. */
  Paint: RfdStatusCheck;
  Rework: RfdReworkCheck;
  PendingConcerns: RfdPendingConcern[];
  ReworkedConcerns: RfdReworkedConcern[];
}

const EMPTY_READINESS: RfdReadiness = {
  Ready: false,
  RoadTest: { Completed: false, AuditorName: '', LogDate: null },
  Underbody: { Completed: false, AuditorName: '', LogDate: null },
  LspAudit: { Completed: false, AuditorName: '', LogDate: null },
  Dtc: { Completed: false, AuditorName: '', LogDate: null, IsOk: null },
  Ecos: { Completed: false, AuditorName: '', LogDate: null, IsOk: null },
  Vms: { Completed: false, AuditorName: '', LogDate: null, IsOk: null },
  Paint: { Completed: false, AuditorName: '', LogDate: null, IsOk: null },
  Rework: { TotalConcerns: 0, DoneCount: 0, PendingCount: 0 },
  PendingConcerns: [],
  ReworkedConcerns: [],
};

@Component({
  selector: 'app-rfd',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './rfd.component.html',
})
export class RfdComponent implements OnInit {
  @Input() vehicle: VehicleDetails | null = null;
  /** True when this vehicle already has an RFD Track_Logs row. */
  @Input() locked = false;
  @Output() stageSaved = new EventEmitter<{ auditAreaId: number; noDefect: boolean }>();

  readonly vehicleService = inject(VehicleTrackingService);
  readonly toast = inject(ToastService);
  readonly confirmationService = inject(ConfirmationService);

  readonly AUDIT_AREA_ID = STAGE.PHYSICAL_RFD;

  auditors: AuditorItem[] = [];
  selectedAuditorId = 0;

  readiness: RfdReadiness = { ...EMPTY_READINESS };
  loading = false;
  saving = false;
  /** Whether the RFD stage itself has been saved already. */
  alreadyDispatched = false;

  /** Track_Logs.Log_Date for RFD — shown in the header next to VIN. */
  logDate: string | null = null;

  ngOnInit(): void {
    this.vehicleService.getAuditorsByArea(this.AUDIT_AREA_ID).subscribe({
      next: (list) => {
        this.auditors = list ?? [];
        this.loadOwnTrackLog();
        this.refreshReadiness();
      },
    });
  }

  /** Pre-select the operator if RFD was saved on a previous visit. */
  private loadOwnTrackLog() {
    if (!this.vehicle?.Vehicle_ID) return;
    this.vehicleService
      .getStageData(this.vehicle.Vehicle_ID, this.AUDIT_AREA_ID)
      .subscribe({
        next: (data: any) => {
          if (data?.TrackLog) {
            this.alreadyDispatched = true;
            this.logDate = data.TrackLog.Log_Date ?? null;
            const savedName: string | undefined = data.TrackLog.Auditor_Name;
            if (savedName) {
              const match = this.auditors.find((a) => a.Auditor_Name === savedName);
              if (match) this.selectedAuditorId = match.Auditor_ID;
            }
          }
        },
      });
  }

  refreshReadiness() {
    if (!this.vehicle?.Vehicle_ID) return;
    this.loading = true;
    this.vehicleService.getRfdReadiness(this.vehicle.Vehicle_ID).subscribe({
      next: (data: any) => {
        this.loading = false;
        this.readiness = { ...EMPTY_READINESS, ...(data ?? {}) };
      },
      error: () => (this.loading = false),
    });
  }

  // ─── Operator auto-save (matches every other stage) ────────────────────

  get selectedOperatorName(): string {
    return (
      this.auditors.find((a) => a.Auditor_ID === this.selectedAuditorId)?.Auditor_Name ?? ''
    );
  }

  onOperatorChange() {
    const name = this.selectedOperatorName;
    if (!name || !this.vehicle?.Vehicle_ID) return;

    this.vehicleService
      .updateAuditor(this.vehicle.Vehicle_ID, this.AUDIT_AREA_ID, name)
      .subscribe({
        next: (updated: any) => {
          if (updated === true || updated?.datalist === true) {
            this.toast.showSuccess('Operator updated', name);
          }
        },
      });
  }

  // ─── Final RFD ────────────────────────────────────────────────────────

  canDispatch(): boolean {
    return (
      !this.locked &&
      this.readiness.Ready &&
      !!this.selectedAuditorId &&
      !!this.vehicle?.Vehicle_ID &&
      !this.saving
    );
  }

  dispatch() {
    if (this.locked) return;
    if (!this.canDispatch()) {
      if (!this.selectedAuditorId) {
        this.toast.showWarning('Operator required', 'Select an RFD operator first.');
      } else if (!this.readiness.Ready) {
        this.toast.showWarning('Not ready', 'Resolve all pending checks first.');
      }
      return;
    }

    this.confirmationService
      .confirm(
        'Physical RFD Done',
        `Mark vehicle ${this.vehicle?.VIN_No} as Ready For Dispatch?`,
        'Confirm',
        'Cancel',
        'warning',
      )
      .then((confirmed) => {
        if (!confirmed) return;
        this.saving = true;
        this.vehicleService
          .saveRfd({
            Vehicle_ID: this.vehicle!.Vehicle_ID,
            Operator_Name: this.selectedOperatorName,
            Inserted_Host: localStorage.getItem('Hostname') ?? '',
            Inserted_User_ID: Number(localStorage.getItem('Employee_ID')) || undefined,
            Plant_Code: localStorage.getItem('Plant_Code') ?? '',
          })
          .subscribe({
            next: () => {
              this.saving = false;
              this.alreadyDispatched = true;
              // Flip the local lock immediately — every guard on this view
              // already inspects `this.locked`, so the UI freezes without
              // waiting for the shell's loadStageLogs round-trip.
              this.locked = true;
              this.logDate = new Date().toISOString();
              this.toast.showSuccess(
                'Ready For Dispatch',
                `Vehicle dispatched by ${this.selectedOperatorName}.`,
              );
              this.stageSaved.emit({ auditAreaId: this.AUDIT_AREA_ID, noDefect: true });
              // Future: trigger barcode generation + send to printer here.
            },
            error: () => (this.saving = false),
          });
      });
  }

  // ─── Display helpers ───────────────────────────────────────────────────

  dtcLabel(): string {
    return this.statusLabel(this.readiness.Dtc);
  }

  dtcOk(): boolean {
    return this.readiness.Dtc.IsOk === true;
  }

  /** Generic label for OK/NOK-style stages — used by DTC, ECOS, VMS. */
  statusLabel(s: RfdStatusCheck): string {
    if (s.IsOk === true) return 'OK';
    if (s.IsOk === false) return 'Not OK';
    return 'not saved';
  }

  /** True when the stage was NOK originally but has been reworked and marked done. */
  reworkedDone(s: RfdStatusCheck): boolean {
    return s.IsOk === false && s.StageReworkDone === true;
  }
}
