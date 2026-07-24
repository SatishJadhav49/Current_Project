import { CommonModule } from '@angular/common';
import {
  Component,
  EventEmitter,
  inject,
  Input,
  OnInit,
  Output,
} from '@angular/core';
import { ToastService } from '../../../../services';
import { VehicleTrackingService } from '../../vehicle-tracking.service';
import {
  ConcernRow,
  STAGE,
  ShowerImportResult,
  ShowerSkippedRow,
  VehicleDetails,
} from '../../vehicle-tracking.models';

@Component({
  selector: 'app-shower-test',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './shower-test.component.html',
})
export class ShowerTestComponent implements OnInit {
  @Input() vehicle: VehicleDetails | null = null;
  @Input() locked = false;
  /** When true, Recheck has been completed for this vehicle and the shower
   *  data is frozen — the "Refresh from Booth" button is hidden. */
  @Input() recheckDone = false;
  @Output() stageSaved = new EventEmitter<{ auditAreaId: number; noDefect: boolean }>();

  private readonly vehicleService = inject(VehicleTrackingService);
  private readonly toast = inject(ToastService);

  readonly AUDIT_AREA_ID = STAGE.SHOWER_TEST;
  readonly STAGE_LABEL = 'Shower Test';

  /** Render state — drives which card the template shows.
   *  'rfd-locked-no-data' = vehicle is RFD-done AND no shower data was ever
   *  imported; we cannot re-fetch (server rejects), so we show a friendly note
   *  instead of the "not done" + Refresh card. */
  state: 'loading' | 'not-done' | 'rfd-locked-no-data' | 'no-defect' | 'with-defects' = 'loading';

  auditorName = '';
  auditDate: string | null = null;
  concerns: ConcernRow[] = [];
  skipped: ShowerSkippedRow[] = [];

  importing = false;

  ngOnInit(): void {
    if (!this.vehicle?.Vehicle_ID) return;
    this.loadExisting();
  }

  /** First open path: if a Track_Logs row already exists for stage 4, render
   *  the previously imported data and skip the external SP call. Otherwise
   *  trigger an import. */
  private loadExisting() {
    if (!this.vehicle?.Vehicle_ID) return;

    this.vehicleService
      .getStageData(this.vehicle.Vehicle_ID, this.AUDIT_AREA_ID)
      .subscribe({
        next: (data: any) => {
          const log = data?.TrackLog;
          if (!log) {
            // No data on file. If the vehicle is RFD-locked we cannot import
            // (the server rejects the call) — surface a friendly note instead
            // of triggering a doomed request.
            if (this.locked) {
              this.state = 'rfd-locked-no-data';
              return;
            }
            // Never imported — try fetching from the booth now.
            this.runImport();
            return;
          }
          this.applyExisting(log, data?.Concerns ?? []);
        },
        error: () => {
          if (this.locked) {
            this.state = 'rfd-locked-no-data';
            return;
          }
          this.runImport();
        },
      });
  }

  private applyExisting(log: any, rawConcerns: any[]) {
    this.auditorName = log.Auditor_Name ?? '';
    this.auditDate = log.Log_Date ?? null;
    const noDefect = !!log.Is_No_Defect;
    this.concerns = (rawConcerns || []) as ConcernRow[];
    this.state = noDefect ? 'no-defect' : 'with-defects';
  }

  /** Manual refresh button + auto-trigger on first open when no data exists. */
  runImport() {
    if (!this.vehicle?.Vehicle_ID || !this.vehicle.VIN_No) return;
    if (this.locked) {
      this.toast.showWarning('Locked', 'Vehicle is Ready For Dispatch — re-import blocked.');
      return;
    }

    this.importing = true;
    this.state = 'loading';

    this.vehicleService
      .importShower({
        Vehicle_ID: this.vehicle.Vehicle_ID,
        VIN_No: this.vehicle.VIN_No,
        Inserted_Host: localStorage.getItem('Hostname') ?? '',
        Inserted_User_ID: Number(localStorage.getItem('Employee_ID')) || undefined,
        Plant_Code: localStorage.getItem('Plant_Code') ?? '',
      })
      .subscribe({
        next: (result: ShowerImportResult) => {
          this.importing = false;
          if (!result || result.Found === false) {
            this.state = 'not-done';
            this.concerns = [];
            this.skipped = [];
            return;
          }

          this.auditorName = result.Auditor_Name ?? '';
          this.auditDate = result.Audit_Date ?? null;
          this.skipped = result.Skipped ?? [];

          if (result.Is_No_Defect) {
            this.state = 'no-defect';
            this.concerns = [];
          } else {
            // Reload the freshly inserted concerns so they include Defect_Entry_ID + master names.
            this.reloadConcerns();
            this.state = 'with-defects';
          }

          // Mark stage 4 green on the progress bar.
          this.stageSaved.emit({
            auditAreaId: this.AUDIT_AREA_ID,
            noDefect: result.Is_No_Defect,
          });

          const msg = result.Is_No_Defect
            ? 'Shower test completed — no defects.'
            : `Imported ${result.Imported} defect(s) from shower booth.`;
          this.toast.showSuccess('Shower imported', msg);

          if (this.skipped.length > 0) {
            this.toast.showWarning(
              'Some rows skipped',
              `${this.skipped.length} row(s) skipped — master entries missing.`,
            );
          }
        },
        error: () => {
          this.importing = false;
          this.state = 'not-done';
        },
      });
  }

  private reloadConcerns() {
    if (!this.vehicle?.Vehicle_ID) return;
    this.vehicleService
      .getStageData(this.vehicle.Vehicle_ID, this.AUDIT_AREA_ID)
      .subscribe({
        next: (data: any) => {
          this.concerns = (data?.Concerns ?? []) as ConcernRow[];
        },
      });
  }
}
