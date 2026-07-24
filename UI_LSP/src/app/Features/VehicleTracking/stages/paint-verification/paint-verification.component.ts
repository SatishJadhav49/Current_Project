import { CommonModule } from '@angular/common';
import {
  Component,
  ElementRef,
  EventEmitter,
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
  AreaImageWithMappings,
  AuditorItem,
  ConcernRow,
  STAGE,
  StageSavePayload,
  VehicleAreaItem,
  VehicleDefectEntryPayload,
  VehicleDetails,
} from '../../vehicle-tracking.models';
import { DefectEntryFormComponent } from '../../shared/defect-entry-form/defect-entry-form.component';
import { AppConfig } from '../../../../config/app.config.service';

/** Server-side whitelisted folder where Paint Verification uploads land. */
const UPLOAD_FOLDER = 'PaintData';
const MAX_FILES = 10;

/** A parsed rectangular hotspot of the active image. */
interface HotspotRect {
  Mapping_ID: number;
  Part_ID: number;
  Part_Name: string;
  x: number;
  y: number;
  w: number;
  h: number;
}

@Component({
  selector: 'app-paint-verification',
  standalone: true,
  imports: [CommonModule, FormsModule, DefectEntryFormComponent],
  templateUrl: './paint-verification.component.html',
})
export class PaintVerificationComponent implements OnInit {
  @Input() vehicle: VehicleDetails | null = null;
  @Input() locked = false;
  @Output() stageSaved = new EventEmitter<{ auditAreaId: number; noDefect: boolean }>();

  @ViewChild(DefectEntryFormComponent) defectForm?: DefectEntryFormComponent;
  @ViewChild('auditImg') auditImg?: ElementRef<HTMLImageElement>;

  readonly vehicleService = inject(VehicleTrackingService);
  readonly toast = inject(ToastService);
  readonly confirmationService = inject(ConfirmationService);
  readonly appConfig = inject(AppConfig);

  readonly AUDIT_AREA_ID = STAGE.PAINT_VERIFICATION;
  readonly STAGE_LABEL = 'Paint Verification';
  readonly MAX_FILES = MAX_FILES;

  // ─── Standard defect-entry state ─────────────────────────────────────────
  auditors: AuditorItem[] = [];
  selectedAuditorId = 0;
  concerns: ConcernRow[] = [];
  saving = false;
  savingNoDefect = false;
  logDate: string | null = null;

  // ─── Attachments (kept from the OK/NOK-era component) ───────────────────
  documents: string[] = [];
  uploading = false;

  // ─── Image audit panel ───────────────────────────────────────────────────
  vehicleAreas: VehicleAreaItem[] = [];
  selectedAreaId = 0;
  /** Images (with hotspots) of the selected area. */
  areaImages: AreaImageWithMappings[] = [];
  activeImageIndex = 0;
  loadingImages = false;

  /** Hotspots of the ACTIVE image, parsed once per image switch. */
  hotspots: HotspotRect[] = [];

  /** The last image tap — attached to the next added concern, then cleared. */
  pendingClick: { areaImageId: number; x: number; y: number } | null = null;

  /** Part filter for the defect form: null = all parts. Set when a tap hits
   *  multiple overlapping hotspots (auditor picks from the shortlist). */
  partFilterIds: number[] | null = null;

  /** Human-readable result of the last tap — shown as a hint strip. */
  clickHint = '';

  ngOnInit(): void {
    this.vehicleService.getAuditorsByArea(this.AUDIT_AREA_ID).subscribe({
      next: (list) => {
        this.auditors = list ?? [];
        this.loadExistingStageData();
      },
    });
    this.vehicleService.getVehicleAreas().subscribe({
      next: (list) => (this.vehicleAreas = list ?? []),
    });
  }

  // ─── Load previously saved state ─────────────────────────────────────────

  private loadExistingStageData() {
    if (!this.vehicle?.Vehicle_ID) return;

    this.vehicleService
      .getStageData(this.vehicle.Vehicle_ID, this.AUDIT_AREA_ID)
      .subscribe({
        next: (data: any) => {
          const log = data?.TrackLog;
          if (log) {
            this.logDate = log.Log_Date ?? null;
            const savedName: string | undefined = log.Auditor_Name;
            if (savedName) {
              const match = this.auditors.find((a) => a.Auditor_Name === savedName);
              if (match) this.selectedAuditorId = match.Auditor_ID;
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
          }

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
            Area_Image_ID: c.Area_Image_ID != null ? Number(c.Area_Image_ID) : null,
            Image_X: c.Image_X != null ? Number(c.Image_X) : null,
            Image_Y: c.Image_Y != null ? Number(c.Image_Y) : null,
          }));
        },
      });
  }

  // ─── Auditor ─────────────────────────────────────────────────────────────

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

  // ─── Image audit panel ───────────────────────────────────────────────────

  onAreaChange() {
    this.areaImages = [];
    this.activeImageIndex = 0;
    this.hotspots = [];
    this.clearClickState();
    if (this.selectedAreaId <= 0) return;

    // Images are model-specific — a Thar front ≠ a 3XO front.
    const modelId = this.vehicle?.Model_ID ?? 0;
    if (modelId <= 0) {
      this.toast.showWarning(
        'Model missing',
        'Vehicle has no model set — save the vehicle details first to load images.',
      );
      return;
    }

    this.loadingImages = true;
    this.vehicleService.getAreaImagesByArea(this.selectedAreaId, modelId).subscribe({
      next: (list: any[]) => {
        this.loadingImages = false;
        this.areaImages = (list ?? []) as AreaImageWithMappings[];
        this.activeImageIndex = 0;
        this.parseActiveHotspots();
      },
      error: () => (this.loadingImages = false),
    });

    // Preselect the picked area in the defect form.
    const area = this.vehicleAreas.find((a) => a.Vehicle_Area_ID === this.selectedAreaId);
    if (area) {
      this.defectForm?.presetSelection({
        Vehicle_Area_ID: area.Vehicle_Area_ID,
        Vehicle_Area_Name: area.Vehicle_Area_Name,
      });
    }
  }

  get activeImage(): AreaImageWithMappings | null {
    return this.areaImages[this.activeImageIndex] ?? null;
  }

  switchImage(index: number) {
    this.activeImageIndex = index;
    this.parseActiveHotspots();
    this.clearClickState();
  }

  private parseActiveHotspots() {
    this.hotspots = [];
    const img = this.activeImage;
    if (!img) return;
    for (const m of img.Mappings ?? []) {
      try {
        const shape = JSON.parse(m.Shape);
        if (shape?.type !== 'rect') continue;
        this.hotspots.push({
          Mapping_ID: m.Mapping_ID,
          Part_ID: m.Part_ID,
          Part_Name: m.Part_Name,
          x: Number(shape.x) || 0,
          y: Number(shape.y) || 0,
          w: Number(shape.w) || 0,
          h: Number(shape.h) || 0,
        });
      } catch {
        // Malformed shape JSON — skip the hotspot.
      }
    }
  }

  private clearClickState() {
    this.pendingClick = null;
    this.partFilterIds = null;
    this.clickHint = '';
  }

  /** Auditor tapped the image: record the point, hit-test hotspots, and
   *  preset/filter the part accordingly. */
  onImageClick(evt: MouseEvent) {
    if (this.locked) return;
    const img = this.auditImg?.nativeElement;
    const active = this.activeImage;
    if (!img || !active) return;

    const rect = img.getBoundingClientRect();
    if (rect.width === 0 || rect.height === 0) return;
    const x = Math.min(1, Math.max(0, (evt.clientX - rect.left) / rect.width));
    const y = Math.min(1, Math.max(0, (evt.clientY - rect.top) / rect.height));

    this.pendingClick = { areaImageId: active.Area_Image_ID, x, y };

    // Hit-test: all hotspots containing the point, smallest area first.
    const hits = this.hotspots
      .filter((h) => x >= h.x && x <= h.x + h.w && y >= h.y && y <= h.y + h.h)
      .sort((a, b) => a.w * a.h - b.w * b.h);

    if (hits.length === 1 || (hits.length > 1 && this.uniquePartCount(hits) === 1)) {
      // Exactly one part at this spot — auto-select it (still editable).
      const hit = hits[0];
      this.partFilterIds = null;
      this.defectForm?.presetSelection({ Part_ID: hit.Part_ID, Part_Name: hit.Part_Name });
      this.clickHint = `Part auto-selected: ${hit.Part_Name}`;
    } else if (hits.length > 1) {
      // Multiple overlapping parts — restrict the picker to the shortlist.
      const ids = [...new Set(hits.map((h) => h.Part_ID))];
      this.partFilterIds = ids;
      this.defectForm?.presetSelection({ Part_ID: 0, Part_Name: '' });
      this.clickHint = `${ids.length} parts at this spot — pick one from the filtered list below.`;
    } else {
      // No mapped part here — fall back to the full part list.
      this.partFilterIds = null;
      this.clickHint = 'No mapped part at this spot — showing all parts.';
    }
  }

  /** How many distinct Part_IDs are in the hit list. */
  private uniquePartCount(hits: HotspotRect[]): number {
    return new Set(hits.map((h) => h.Part_ID)).size;
  }

  clearPartFilter() {
    this.partFilterIds = null;
    this.clickHint = '';
  }

  imageUrl(fileName: string): string {
    const base = (this.appConfig.baseApiPath || '').replace(/\/$/, '');
    return `${base}/MM_Area_Image_Master/GetImage?fileName=${encodeURIComponent(fileName)}`;
  }

  /** Defect pins to draw on the ACTIVE image — saved concerns clicked there. */
  get activePins(): ConcernRow[] {
    const active = this.activeImage;
    if (!active) return [];
    return this.concerns.filter(
      (c) =>
        c.Area_Image_ID === active.Area_Image_ID &&
        c.Image_X != null &&
        c.Image_Y != null,
    );
  }

  // ─── Defect entry (same pattern as Static Audit) ─────────────────────────

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
      // Attach the image tap (if any) — cleared after a successful add.
      Area_Image_ID: this.pendingClick?.areaImageId ?? null,
      Image_X: this.pendingClick != null ? +this.pendingClick.x.toFixed(4) : null,
      Image_Y: this.pendingClick != null ? +this.pendingClick.y.toFixed(4) : null,
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

  onAddRequest(row: ConcernRow) {
    if (!this.guardSaveable()) return;

    this.saving = true;
    const entry = this.toPayload(row);
    const payload: StageSavePayload = {
      ...this.baseStagePayload(),
      Is_No_Defect: false,
      Concerns: [entry],
    };

    this.vehicleService.addConcern(payload).subscribe({
      next: (res: any) => {
        this.saving = false;
        const datalist = res?.datalist ?? res;
        const newId = datalist?.Defect_Entry_ID ?? datalist?.defect_Entry_ID;
        const saved: ConcernRow = {
          ...row,
          Defect_Entry_ID: newId,
          Area_Image_ID: entry.Area_Image_ID,
          Image_X: entry.Image_X,
          Image_Y: entry.Image_Y,
        };
        this.concerns = [...this.concerns, saved];
        this.logDate = new Date().toISOString();
        this.toast.showSuccess('Saved', `Concern #${this.concerns.length} added.`);

        this.defectForm?.resetAfterAdd();
        this.clearClickState();

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
            ? { ...c, ...evt.row, Defect_Entry_ID: evt.defectEntryId }
            : c,
        );
        this.logDate = new Date().toISOString();
        this.toast.showSuccess('Updated', 'Concern updated.');
        this.defectForm?.resetAfterUpdate();
        this.clearClickState();
      },
      error: () => (this.saving = false),
    });
  }

  // ─── No Defect path ──────────────────────────────────────────────────────

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

  // ─── Attachments (auto-saved via UpdateStageDocuments) ───────────────────

  onFilesChosen(evt: Event) {
    const input = evt.target as HTMLInputElement;
    const files = input.files;
    if (!files || files.length === 0) return;
    this.uploadFiles(Array.from(files));
    input.value = '';
  }

  private async uploadFiles(files: File[]) {
    if (this.locked) return;
    const remaining = MAX_FILES - this.documents.length;
    if (remaining <= 0) {
      this.toast.showWarning('Limit reached', `Max ${MAX_FILES} files allowed.`);
      return;
    }
    const toUpload = files.slice(0, remaining);
    this.uploading = true;
    try {
      for (const file of toUpload) {
        try {
          const savedName: any = await this.vehicleService
            .uploadStageFile(file, UPLOAD_FOLDER)
            .toPromise();
          const name = typeof savedName === 'string' ? savedName : savedName?.datalist;
          if (typeof name === 'string' && name.length > 0) {
            this.documents = [...this.documents, name];
          }
        } catch {
          this.toast.showError('Upload failed', file.name);
        }
      }
      this.persistDocuments();
    } finally {
      this.uploading = false;
    }
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
            this.persistDocuments();
            this.toast.showSuccess('Removed', this.displayNameOf(name));
          },
        });
      });
  }

  /** Write the current Documents list to the Track_Logs row. */
  private persistDocuments() {
    if (!this.vehicle?.Vehicle_ID) return;
    this.vehicleService
      .updateStageDocuments({
        Vehicle_ID: this.vehicle.Vehicle_ID,
        Audit_Area_ID: this.AUDIT_AREA_ID,
        Documents: this.documents,
        Inserted_Host: localStorage.getItem('Hostname') ?? '',
        Inserted_User_ID: Number(localStorage.getItem('Employee_ID')) || undefined,
        Plant_Code: localStorage.getItem('Plant_Code') ?? '',
      })
      .subscribe();
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
}
