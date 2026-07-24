import { CommonModule } from '@angular/common';
import {
  AfterViewInit,
  Component,
  ElementRef,
  inject,
  OnInit,
  ViewChild,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ToastService } from '../../../services';
import { VehicleTrackingService } from '../vehicle-tracking.service';
import {
  AuditArea,
  StageStatus,
  StageView,
  VehicleDetails,
} from '../vehicle-tracking.models';
import { IdentifyVehicleComponent } from '../stages/identify-vehicle/identify-vehicle.component';
import { RoadTestComponent } from '../stages/road-test/road-test.component';
import { UnderbodyComponent } from '../stages/underbody/underbody.component';
import { LspAuditComponent } from '../stages/lsp-audit/lsp-audit.component';
import { DtcComponent } from '../stages/dtc/dtc.component';
import { EcosComponent } from '../stages/ecos/ecos.component';
import { VmsComponent } from '../stages/vms/vms.component';
import { RecheckComponent } from '../stages/recheck/recheck.component';
import { RewokComponent } from '../stages/rework/rework.component';
import { RfdComponent } from '../stages/rfd/rfd.component';
import { ShowerTestComponent } from '../stages/shower-test/shower-test.component';
import { PaintVerificationComponent } from '../stages/paint-verification/paint-verification.component';
import { STAGE } from '../vehicle-tracking.models';
import { AlertData, TeamsWebhookService } from '../../../services/teamswebhook.service';

const IDENTIFY_STAGE_ID = 0;

/** Stages whose unlock rules will land in a future task — kept hard-locked for now.
 *  All stages are currently reachable; the empty set keeps the existing structure
 *  so any future "lock until X" rule can be added without touching call sites. */
const LOCKED_FOR_NOW = new Set<number>([]);

/** Stages that are always optional — sourced from an external system or not
 *  required to gate downstream stages. They never count as "skipped" and will
 *  be excluded from any future "all middle stages complete" checks. */
const OPTIONAL_STAGES = new Set<number>([
  STAGE.SHOWER_TEST,
]);

@Component({
  selector: 'app-vehicle-tracking-home',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    IdentifyVehicleComponent,
    RoadTestComponent,
    UnderbodyComponent,
    LspAuditComponent,
    DtcComponent,
    EcosComponent,
    VmsComponent,
    RecheckComponent,
    RewokComponent,
    RfdComponent,
    ShowerTestComponent,
    PaintVerificationComponent,
  ],
  templateUrl: './vehicle-tracking-home.component.html',
})
export class VehicleTrackingHomeComponent implements OnInit, AfterViewInit {
  readonly vehicleService = inject(VehicleTrackingService);
  readonly toast = inject(ToastService);
  readonly webhookService = inject(TeamsWebhookService);

  @ViewChild('vinInput') vinInput!: ElementRef<HTMLInputElement>;

  // VIN + vehicle live in the shell — every stage receives them via @Input
  vinNo = '';
  vehicle: VehicleDetails | null = null;
  vehicleLoaded = false;
  loadingVin = false;

  stages: StageView[] = [];
  currentStageId: number = IDENTIFY_STAGE_ID;
  /** True once a Physical RFD Track_Logs row exists for this vehicle.
   *  When set, every write endpoint server-side rejects with 400 Locked
   *  and the UI hides/disables every write control. */
  rfdDone = false;

  /** True once a Recheck Track_Logs row exists for this vehicle. Used by the
   *  Shower stage to suppress the "Refresh from Booth" button — once recheck
   *  has been performed, the shower data is treated as frozen. */
  recheckDone = false;

  readonly IDENTIFY_STAGE_ID = IDENTIFY_STAGE_ID;
  readonly STAGE = STAGE;
  plantCode = localStorage.getItem('Plant_Code') ?? '';

  ngOnInit(): void {
    this.loadStages();
  }

  ngAfterViewInit(): void {
    setTimeout(() => this.vinInput?.nativeElement?.focus(), 0);
  }

  private loadStages() {
    this.vehicleService.getAuditAreas().subscribe({
      next: (areas: AuditArea[]) => {
        const dbStages: StageView[] = (areas ?? []).map((a) => ({
          Audit_Area_ID: a.Audit_Area_ID,
          Audit_Area_Name: a.Audit_Area_Name,
          status: 'pending' as StageStatus,
        }));
        this.stages = [
          {
            Audit_Area_ID: IDENTIFY_STAGE_ID,
            Audit_Area_Name: 'Vehicle',
            status: 'active',
          },
          ...dbStages,
        ];
      },
    });
  }

  // ──────────────── VIN handling ────────────────

  onVinInput() {
    const v = (this.vinNo || '').trim();
    if (v.length === 8 || v.length === 17) this.fetchVin();
  }

  fetchVin() {
    const v = this.vinNo.trim();
    if (v.length !== 8 && v.length !== 17) {
      this.toast.showWarning('Validation', 'VIN must be 8 or 17 characters.');
      return;
    }
    this.loadingVin = true;
    this.vehicleService.GetVinDetails(v).subscribe({
      next: (res) => {
        this.loadingVin = false;
        if (!res) {
          this.toast.showWarning('Not found', 'VIN not found in Drona.');
          return;
        }
        this.vehicle = { ...new VehicleDetails(), ...res };
        this.vehicleLoaded = true;
        this.applyStageStateFromVehicle();
      },
      error: () => (this.loadingVin = false),
    });
  }

  clearVin() {
    this.vinNo = '';
    this.vehicle = null;
    this.vehicleLoaded = false;
    this.rfdDone = false;
    this.recheckDone = false;
    this.currentStageId = IDENTIFY_STAGE_ID;
    this.stages = this.stages.map((s, i) => ({
      ...s,
      status: i === 0 ? 'active' : 'pending',
    }));
    setTimeout(() => this.vinInput?.nativeElement?.focus(), 0);
  }

  /** When vehicle already exists in DB, mark Stage 0 complete and fetch the saved
   *  stage logs so the progress bar reflects every stage that has been completed.
   *  When new, only Stage 0 is active. */
  private applyStageStateFromVehicle() {
    if (!this.vehicle) return;
    const alreadySaved = (this.vehicle.Vehicle_ID ?? 0) > 0;

    if (alreadySaved) {
      this.loadStageLogs();
    } else {
      this.stages = this.stages.map((s, i) => ({
        ...s,
        status: i === 0 ? 'active' : 'pending',
      }));
      this.currentStageId = IDENTIFY_STAGE_ID;
    }
  }

  /** Fetch all stage logs for the current vehicle and reflect them in the progress bar. */
  private loadStageLogs() {
    if (!this.vehicle?.Vehicle_ID) return;

    this.vehicleService.getStageLogsByVehicle(this.vehicle.Vehicle_ID).subscribe({
      next: (logs: any) => {
        const completedAreaIds = new Set<number>(
          (logs ?? []).map((l: any) => Number(l.Audit_Area_ID)),
        );
        // RFD Track_Logs row presence = whole vehicle is locked
        this.rfdDone = completedAreaIds.has(STAGE.PHYSICAL_RFD);
        // Recheck Track_Logs row presence = shower data is now frozen
        this.recheckDone = completedAreaIds.has(STAGE.RECHECK);

        const newStages: StageView[] = this.stages.map((s) => {
          if (s.Audit_Area_ID === IDENTIFY_STAGE_ID) {
            return { ...s, status: 'completed' };
          }
          if (completedAreaIds.has(s.Audit_Area_ID)) {
            return { ...s, status: 'completed' };
          }
          return { ...s, status: 'pending' };
        });

        // Auto-navigation rules:
        //  • If RFD is done, jump straight to RFD (the operator should land
        //    on the final state, not partway back through the flow).
        //  • Otherwise pick the first pending stage that ISN'T Shower Test —
        //    Shower is fully optional and driven by the external booth, so we
        //    never auto-land there.
        //  • If everything mandatory is complete, land on the last stage.
        if (this.rfdDone) {
          this.currentStageId = STAGE.PHYSICAL_RFD;
        } else {
          const firstPending = newStages.find(
            (s) => s.status === 'pending' && s.Audit_Area_ID !== STAGE.SHOWER_TEST,
          );
          if (firstPending) {
            firstPending.status = 'active';
            this.currentStageId = firstPending.Audit_Area_ID;
          } else {
            this.currentStageId = newStages[newStages.length - 1].Audit_Area_ID;
          }
        }

        this.stages = newStages;
      },
    });
  }

  // ──────────────── Stage interactions ────────────────

  onStageClick(stage: StageView) {
    if (!this.vehicleLoaded) {
      this.toast.showWarning('Scan VIN first', 'Please enter a VIN to begin.');
      return;
    }
    if (stage.Audit_Area_ID === this.currentStageId) return;

    if (!this.isAccessible(stage)) {
      // Tell the operator exactly why this stage is locked.
      if (stage.Audit_Area_ID === STAGE.ROAD_TEST) {
        this.toast.showWarning('Locked', 'Save Vehicle details first.');
      } else if (LOCKED_FOR_NOW.has(stage.Audit_Area_ID)) {
        this.toast.showWarning('Locked', 'This stage will unlock later.');
      } else {
        this.toast.showWarning('Locked', 'Save Road Test first.');
      }
      return;
    }
    this.currentStageId = stage.Audit_Area_ID;
  }

  /** Gating rules:
   *   • Vehicle (0)            — always accessible once a VIN is loaded
   *   • Road Test (1)          — requires Vehicle completed
   *   • All other middle stages — require Road Test completed (any order)
   *   • Recheck/Rework/RFD     — locked for now (will be wired later) */
  isAccessible(stage: StageView): boolean {
    if (!this.vehicleLoaded) return false;
    if (LOCKED_FOR_NOW.has(stage.Audit_Area_ID)) return false;

    if (stage.Audit_Area_ID === IDENTIFY_STAGE_ID) return true;

    const vehicleStage = this.stages.find((s) => s.Audit_Area_ID === IDENTIFY_STAGE_ID);
    const vehicleDone = vehicleStage?.status === 'completed';

    if (stage.Audit_Area_ID === STAGE.ROAD_TEST) return vehicleDone;

    const rtStage = this.stages.find((s) => s.Audit_Area_ID === STAGE.ROAD_TEST);
    const rtDone = rtStage?.status === 'completed';
    return vehicleDone && rtDone;
  }

  /** A stage was "skipped" if it isn't completed but the operator already
   *  saved a later stage — surface this as an orange '!' on the dot.
   *  Optional stages (e.g. Shower Test from an external system) are never
   *  considered skipped. */
  isSkipped(stage: StageView): boolean {
    if (stage.status === 'completed') return false;
    if (OPTIONAL_STAGES.has(stage.Audit_Area_ID)) return false;

    const idx = this.stages.findIndex((s) => s.Audit_Area_ID === stage.Audit_Area_ID);
    if (idx < 0) return false;
    for (let i = idx + 1; i < this.stages.length; i++) {
      if (this.stages[i].status === 'completed') return true;
    }
    return false;
  }

  /** Optional stages get a softer visual treatment (dashed border) so the
   *  operator sees that they're not theirs to act on. */
  isOptional(stage: StageView): boolean {
    return OPTIONAL_STAGES.has(stage.Audit_Area_ID);
  }

  /** Stage 0 saved — mark complete, activate next stage. No auto-navigation. */
  onVehicleSaved(vehicle: VehicleDetails) {
    this.vehicle = vehicle;
    this.markStageComplete(IDENTIFY_STAGE_ID);
  }

  /** Generic stage-saved handler — used by every stage except Identify. */
  onStageSaved(auditAreaId: number) {
    this.markStageComplete(auditAreaId);
    // Recheck just saved — freeze shower data immediately (don't wait for a
    // page reload before the Refresh button disappears).
    if (auditAreaId === STAGE.RECHECK) {
      this.recheckDone = true;
    }
    // Physical RFD just saved — flip the whole-vehicle lock immediately so
    // every other stage component receives [locked]="true" on its next change
    // detection. Without this, the operator could still edit other stages
    // until the next loadStageLogs round-trip.
    if (auditAreaId === STAGE.PHYSICAL_RFD) {
      this.rfdDone = true;
    }
  }

  private markStageComplete(auditAreaId: number) {
    const idx = this.stages.findIndex((s) => s.Audit_Area_ID === auditAreaId);
    if (idx < 0) return;
    this.stages[idx].status = 'completed';
    if (this.stages.length > idx + 1 && this.stages[idx + 1].status === 'pending') {
      this.stages[idx + 1].status = 'active';
    }
  }

  // ──────────────── Progress bar styling ────────────────

  stageDotCls(stage: StageView): string {
    if (stage.Audit_Area_ID === this.currentStageId)
      return 'bg-blue-600 text-white border-blue-600';
    if (stage.status === 'completed')
      return 'bg-green-500 text-white border-green-500';
    if (this.isSkipped(stage))
      return 'bg-orange-500 text-white border-orange-500';
    if (!this.isAccessible(stage))
      return 'bg-white text-gray-300 border-gray-200';
    return 'bg-white text-gray-400 border-gray-300';
  }

  stageLabelCls(stage: StageView): string {
    if (stage.Audit_Area_ID === this.currentStageId)
      return 'text-blue-700 font-semibold';
    if (stage.status === 'completed') return 'text-green-700 font-medium';
    if (this.isSkipped(stage)) return 'text-orange-700 font-medium';
    if (!this.isAccessible(stage)) return 'text-gray-400';
    return 'text-gray-500';
  }

  connectorCls(i: number): string {
    const stage = this.stages[i];
    return stage?.status === 'completed' ? 'bg-green-500' : 'bg-gray-300';
  }

  /** Emoji icon shown inside the progress-bar dot for pending/active stages.
   *  Completed stages still show ✓ and skipped stages still show ! — the icon
   *  only replaces the numeric index. Keyed on Audit_Area_ID so renaming the
   *  master label can't break the mapping. */
  stageIcon(stage: StageView): string {
    switch (stage.Audit_Area_ID) {
      case IDENTIFY_STAGE_ID: return '🚗';
      case STAGE.ROAD_TEST:   return '🛣️';
      case STAGE.UNDERBODY:   return '🔩';
      case STAGE.DTC:         return '💻';
      case STAGE.SHOWER_TEST: return '🚿';
      case STAGE.ECOS:        return '⚡';
      case STAGE.LSP_AUDIT:   return '📋';
      case STAGE.VMS:         return '🖥️';
      case STAGE.RECHECK:     return '🔁';
      case STAGE.PAINT_VERIFICATION: return '🎨';
      case STAGE.REWORK:      return '🛠️';
      case STAGE.PHYSICAL_RFD: return '🚚';
      default: return '•';
    }
  }
}
