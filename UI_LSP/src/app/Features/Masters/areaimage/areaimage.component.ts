import { CommonModule } from '@angular/common';
import { Component, ElementRef, inject, OnInit, ViewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ToastService } from '../../../services/toast.service';
import { ConfirmationService } from '../../../services/confirmation.service';
import { MasterService } from '../master.service';
import { VehicleTrackingService } from '../../VehicleTracking/vehicle-tracking.service';
import { ModelItem, PartItem, VehicleAreaItem } from '../../VehicleTracking/vehicle-tracking.models';
import { AppConfig } from '../../../config/app.config.service';

/** One image row in the master list. */
interface AreaImageRow {
  Area_Image_ID: number;
  Vehicle_Area_ID: number;
  Vehicle_Area_Name: string;
  Model_ID: number;
  Model_Name: string;
  Image_Name: string;
  Image_FileName: string;
  SORTORDER: number;
  Mapping_Count: number;
}

/** A part hotspot. Shape is parsed from/serialized to the stored JSON. */
interface MappingRow {
  Mapping_ID: number;
  Area_Image_ID: number;
  Part_ID: number;
  Part_Name: string;
  /** Normalized 0..1 rect. */
  x: number;
  y: number;
  w: number;
  h: number;
}

@Component({
  selector: 'app-areaimage-master',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './areaimage.component.html',
})
export class AreaImageMasterComponent implements OnInit {
  readonly toastService = inject(ToastService);
  readonly confirmationService = inject(ConfirmationService);
  readonly masterService = inject(MasterService);
  readonly vehicleService = inject(VehicleTrackingService);
  readonly appConfig = inject(AppConfig);

  @ViewChild('imgEl') imgEl?: ElementRef<HTMLImageElement>;

  // ─── View switching ──────────────────────────────────────────────────────
  /** 'list' = master table + create/edit form. 'mapper' = hotspot editor. */
  view: 'list' | 'mapper' = 'list';

  // ─── Masters ─────────────────────────────────────────────────────────────
  vehicleAreas: VehicleAreaItem[] = [];
  models: ModelItem[] = [];
  parts: PartItem[] = [];

  // ─── List state ──────────────────────────────────────────────────────────
  images: AreaImageRow[] = [];
  loading = false;

  // ─── Create/Edit form ────────────────────────────────────────────────────
  showForm = false;
  editId = 0;
  formAreaId = 0;
  formModelId = 0;
  formName = '';
  formSortOrder = 0;
  formFile: File | null = null;
  saving = false;

  // ─── Mapper state ────────────────────────────────────────────────────────
  activeImage: AreaImageRow | null = null;
  mappings: MappingRow[] = [];
  selectedPartId = 0;
  hoveredMappingId = 0;
  savingMapping = false;

  /** In-progress drag rectangle (normalized coords). Null = not drawing. */
  drawRect: { x: number; y: number; w: number; h: number } | null = null;
  private dragStart: { x: number; y: number } | null = null;

  ngOnInit(): void {
    this.vehicleService.getVehicleAreas().subscribe({
      next: (list) => (this.vehicleAreas = list ?? []),
    });
    this.vehicleService.getModels().subscribe({
      next: (list) => (this.models = list ?? []),
    });
    this.vehicleService.getParts().subscribe({
      next: (list) => (this.parts = list ?? []),
    });
    this.loadImages();
  }

  // ─── List + form ─────────────────────────────────────────────────────────

  loadImages() {
    this.loading = true;
    this.masterService.getAllAreaImages().subscribe({
      next: (list: any[]) => {
        this.loading = false;
        this.images = (list ?? []).map((r) => ({
          Area_Image_ID: Number(r.Area_Image_ID),
          Vehicle_Area_ID: Number(r.Vehicle_Area_ID),
          Vehicle_Area_Name: r.Vehicle_Area_Name,
          Model_ID: Number(r.Model_ID) || 0,
          Model_Name: r.Model_Name || '',
          Image_Name: r.Image_Name,
          Image_FileName: r.Image_FileName,
          SORTORDER: Number(r.SORTORDER) || 0,
          Mapping_Count: Number(r.Mapping_Count) || 0,
        }));
      },
      error: () => (this.loading = false),
    });
  }

  openCreate() {
    this.showForm = true;
    this.editId = 0;
    this.formAreaId = 0;
    this.formModelId = 0;
    this.formName = '';
    this.formSortOrder = 0;
    this.formFile = null;
  }

  openEdit(row: AreaImageRow) {
    this.showForm = true;
    this.editId = row.Area_Image_ID;
    this.formAreaId = row.Vehicle_Area_ID;
    this.formModelId = row.Model_ID;
    this.formName = row.Image_Name;
    this.formSortOrder = row.SORTORDER;
    this.formFile = null; // optional replacement
  }

  cancelForm() {
    this.showForm = false;
    this.editId = 0;
  }

  onFileChosen(evt: Event) {
    const input = evt.target as HTMLInputElement;
    this.formFile = input.files?.[0] ?? null;
  }

  canSaveForm(): boolean {
    return (
      !this.saving &&
      this.formAreaId > 0 &&
      this.formModelId > 0 &&
      this.formName.trim().length > 0 &&
      (this.editId > 0 || this.formFile != null)
    );
  }

  saveForm() {
    if (!this.canSaveForm()) {
      if (this.formAreaId <= 0) {
        this.toastService.showWarning('Area required', 'Select a vehicle area.');
      } else if (this.formModelId <= 0) {
        this.toastService.showWarning('Model required', 'Select the vehicle model this image belongs to.');
      } else if (!this.formName.trim()) {
        this.toastService.showWarning('Name required', 'Enter an image name (e.g., Front View).');
      } else if (this.editId === 0 && !this.formFile) {
        this.toastService.showWarning('Image required', 'Choose an image file.');
      }
      return;
    }

    this.saving = true;
    const form = new FormData();
    form.append('Area_Image_ID', String(this.editId));
    form.append('Vehicle_Area_ID', String(this.formAreaId));
    form.append('Model_ID', String(this.formModelId));
    form.append('Image_Name', this.formName.trim());
    form.append('SORTORDER', String(this.formSortOrder || 0));
    form.append('Inserted_Host', localStorage.getItem('Hostname') ?? '');
    form.append('Inserted_User_ID', localStorage.getItem('Employee_ID') ?? '');
    form.append('Plant_Code', localStorage.getItem('Plant_Code') ?? '');
    if (this.formFile) form.append('File', this.formFile);

    this.masterService.saveAreaImage(form).subscribe({
      next: () => {
        this.saving = false;
        this.toastService.showSuccess(
          this.editId > 0 ? 'Updated' : 'Saved',
          this.formName.trim(),
        );
        this.cancelForm();
        this.loadImages();
      },
      error: () => (this.saving = false),
    });
  }

  deleteImage(row: AreaImageRow) {
    this.confirmationService
      .confirmDelete(
        'Delete Area Image',
        'The image and its part mappings will no longer be offered on the Paint Verification screen. Historical defects that reference it are kept.',
        row.Image_Name,
        'Delete',
        'Cancel',
      )
      .then((confirmed) => {
        if (!confirmed) return;
        this.masterService.deleteAreaImage(row.Area_Image_ID).subscribe({
          next: () => {
            this.toastService.showSuccess('Deleted', row.Image_Name);
            this.loadImages();
          },
        });
      });
  }

  imageUrl(fileName: string): string {
    const base = (this.appConfig.baseApiPath || '').replace(/\/$/, '');
    return `${base}/MM_Area_Image_Master/GetImage?fileName=${encodeURIComponent(fileName)}`;
  }

  // ─── Mapper ──────────────────────────────────────────────────────────────

  openMapper(row: AreaImageRow) {
    this.activeImage = row;
    this.view = 'mapper';
    this.selectedPartId = 0;
    this.drawRect = null;
    this.dragStart = null;
    this.loadMappings(row.Area_Image_ID);
  }

  closeMapper() {
    this.view = 'list';
    this.activeImage = null;
    this.mappings = [];
    this.loadImages(); // refresh mapping counts
  }

  private loadMappings(areaImageId: number) {
    this.masterService.getAreaImageMappings(areaImageId).subscribe({
      next: (list: any[]) => {
        this.mappings = (list ?? [])
          .map((m) => this.toMappingRow(m))
          .filter((m): m is MappingRow => m !== null);
      },
    });
  }

  private toMappingRow(m: any): MappingRow | null {
    try {
      const shape = JSON.parse(m.Shape);
      if (shape?.type !== 'rect') return null;
      return {
        Mapping_ID: Number(m.Mapping_ID),
        Area_Image_ID: Number(m.Area_Image_ID),
        Part_ID: Number(m.Part_ID),
        Part_Name: m.Part_Name,
        x: Number(shape.x) || 0,
        y: Number(shape.y) || 0,
        w: Number(shape.w) || 0,
        h: Number(shape.h) || 0,
      };
    } catch {
      return null;
    }
  }

  /** Normalize a mouse event into 0..1 coordinates over the image. */
  private normPoint(evt: MouseEvent): { x: number; y: number } | null {
    const img = this.imgEl?.nativeElement;
    if (!img) return null;
    const rect = img.getBoundingClientRect();
    if (rect.width === 0 || rect.height === 0) return null;
    const x = (evt.clientX - rect.left) / rect.width;
    const y = (evt.clientY - rect.top) / rect.height;
    return {
      x: Math.min(1, Math.max(0, x)),
      y: Math.min(1, Math.max(0, y)),
    };
  }

  onMapperMouseDown(evt: MouseEvent) {
    if (this.selectedPartId <= 0) {
      this.toastService.showWarning('Pick a part', 'Select the part first, then drag a box over it on the image.');
      return;
    }
    const p = this.normPoint(evt);
    if (!p) return;
    evt.preventDefault();
    this.dragStart = p;
    this.drawRect = { x: p.x, y: p.y, w: 0, h: 0 };
  }

  onMapperMouseMove(evt: MouseEvent) {
    if (!this.dragStart) return;
    const p = this.normPoint(evt);
    if (!p) return;
    this.drawRect = {
      x: Math.min(this.dragStart.x, p.x),
      y: Math.min(this.dragStart.y, p.y),
      w: Math.abs(p.x - this.dragStart.x),
      h: Math.abs(p.y - this.dragStart.y),
    };
  }

  onMapperMouseUp() {
    if (!this.dragStart || !this.drawRect) return;
    const rect = this.drawRect;
    this.dragStart = null;

    // Ignore accidental clicks — require a box of at least 1.5% in each dim.
    if (rect.w < 0.015 || rect.h < 0.015) {
      this.drawRect = null;
      return;
    }
    this.saveNewMapping(rect);
  }

  onMapperMouseLeave() {
    // Cancel an in-progress drag that leaves the image.
    this.dragStart = null;
    this.drawRect = null;
  }

  private saveNewMapping(rect: { x: number; y: number; w: number; h: number }) {
    if (!this.activeImage || this.selectedPartId <= 0) return;
    const part = this.parts.find((p) => p.Part_ID === this.selectedPartId);
    if (!part) return;

    this.savingMapping = true;
    const payload = {
      Mapping_ID: 0,
      Area_Image_ID: this.activeImage.Area_Image_ID,
      Part_ID: this.selectedPartId,
      Shape: JSON.stringify({
        type: 'rect',
        x: +rect.x.toFixed(4),
        y: +rect.y.toFixed(4),
        w: +rect.w.toFixed(4),
        h: +rect.h.toFixed(4),
      }),
      Inserted_Host: localStorage.getItem('Hostname') ?? '',
      Inserted_User_ID: Number(localStorage.getItem('Employee_ID')) || undefined,
      Plant_Code: localStorage.getItem('Plant_Code') ?? '',
    };

    this.masterService.saveAreaImageMapping(payload).subscribe({
      next: (newId: any) => {
        this.savingMapping = false;
        this.drawRect = null;
        const id = typeof newId === 'number' ? newId : Number(newId) || 0;
        this.mappings = [
          ...this.mappings,
          {
            Mapping_ID: id,
            Area_Image_ID: this.activeImage!.Area_Image_ID,
            Part_ID: part.Part_ID,
            Part_Name: part.Part_Name,
            ...rect,
          },
        ];
        this.toastService.showSuccess('Mapped', part.Part_Name);
      },
      error: () => {
        this.savingMapping = false;
        this.drawRect = null;
      },
    });
  }

  deleteMapping(m: MappingRow) {
    this.confirmationService
      .confirmDelete(
        'Delete Mapping',
        'Remove this part hotspot from the image?',
        m.Part_Name,
        'Delete',
        'Cancel',
      )
      .then((confirmed) => {
        if (!confirmed) return;
        this.masterService.deleteAreaImageMapping(m.Mapping_ID).subscribe({
          next: () => {
            this.mappings = this.mappings.filter((x) => x.Mapping_ID !== m.Mapping_ID);
            this.toastService.showSuccess('Removed', m.Part_Name);
          },
        });
      });
  }
}
