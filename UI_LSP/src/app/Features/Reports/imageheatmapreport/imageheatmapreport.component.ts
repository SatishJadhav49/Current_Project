import { CommonModule } from '@angular/common';
import { Component, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Workbook } from 'exceljs';
import { saveAs } from 'file-saver';
import { AppConfig } from '../../../config/app.config.service';
import { ToastService } from '../../../services';
import { VehicleTrackingService } from '../../VehicleTracking/vehicle-tracking.service';
import {
  ModelItem,
  VehicleAreaItem,
} from '../../VehicleTracking/vehicle-tracking.models';
import {
  HeatHotspot,
  HeatImage,
  ImageDefectRow,
  ImageHeatmapResponse,
  ReportsService,
} from '../reports.service';

@Component({
  selector: 'app-imageheatmapreport',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './imageheatmapreport.component.html',
  styleUrl: './imageheatmapreport.component.css',
})
export class ImageheatmapreportComponent implements OnInit {
  private readonly reportsService = inject(ReportsService);
  private readonly vehicleService = inject(VehicleTrackingService);
  private readonly toast = inject(ToastService);
  readonly appConfig = inject(AppConfig);

  models: ModelItem[] = [];
  areas: VehicleAreaItem[] = [];
  selectedModelId = 0;
  selectedAreaId = 0;
  startDate = '';
  endDate = '';

  report: ImageHeatmapResponse | null = null;
  loading = false;
  searched = false;

  /** Table filter: a clicked part region, or null for "all". */
  selectedImageId: number | null = null;
  selectedPartId: number | null = null;
  selectedPartName = '';

  ngOnInit(): void {
    const now = new Date();
    const from = new Date();
    from.setDate(now.getDate() - 30);
    this.startDate = this.toInputDate(from);
    this.endDate = this.toInputDate(now);

    this.vehicleService.getModels().subscribe({
      next: (list) => (this.models = list ?? []),
      error: () => (this.models = []),
    });
    this.vehicleService.getVehicleAreas().subscribe({
      next: (list) => (this.areas = list ?? []),
      error: () => (this.areas = []),
    });
  }

  private toInputDate(d: Date): string {
    const m = String(d.getMonth() + 1).padStart(2, '0');
    const day = String(d.getDate()).padStart(2, '0');
    return `${d.getFullYear()}-${m}-${day}`;
  }

  get selectedModelName(): string {
    return this.models.find((m) => m.Model_ID === this.selectedModelId)?.Model_Name ?? '';
  }
  get selectedAreaName(): string {
    return this.areas.find((a) => a.Vehicle_Area_ID === this.selectedAreaId)?.Vehicle_Area_Name ?? '';
  }

  imageUrl(fileName: string): string {
    const base = (this.appConfig.baseApiPath || '').replace(/\/$/, '');
    return `${base}/MM_Area_Image_Master/GetImage?fileName=${encodeURIComponent(fileName)}`;
  }

  search(): void {
    if (this.selectedModelId <= 0) {
      this.toast.showError('Model required', 'Please select a model.');
      return;
    }
    if (this.selectedAreaId <= 0) {
      this.toast.showError('Area required', 'Please select a vehicle area.');
      return;
    }
    if (!this.startDate || !this.endDate) {
      this.toast.showError('Dates required', 'Please select start and end dates.');
      return;
    }
    if (this.endDate < this.startDate) {
      this.toast.showError('Invalid range', 'End date cannot be before start date.');
      return;
    }

    this.loading = true;
    this.searched = true;
    this.clearFilter();
    this.reportsService
      .getImageHeatmapReport({
        Model_ID: this.selectedModelId,
        Vehicle_Area_ID: this.selectedAreaId,
        StartDate: this.startDate,
        EndDate: this.endDate,
      })
      .subscribe({
        next: (data) => {
          this.report = data ?? null;
          this.loading = false;
        },
        error: () => {
          this.report = null;
          this.loading = false;
        },
      });
  }

  clear(): void {
    this.selectedModelId = 0;
    this.selectedAreaId = 0;
    this.report = null;
    this.searched = false;
    this.clearFilter();
  }

  get hasImages(): boolean {
    return (this.report?.Images?.length ?? 0) > 0;
  }

  /** Defect count for a part region (drives the on-image badge). */
  countFor(image: HeatImage, hotspot: HeatHotspot): number {
    if (!this.report) return 0;
    return this.report.Defects.filter(
      (d) => d.Area_Image_ID === image.Area_Image_ID && d.Part_ID === hotspot.Part_ID,
    ).length;
  }

  isSelected(image: HeatImage, hotspot: HeatHotspot): boolean {
    return this.selectedImageId === image.Area_Image_ID && this.selectedPartId === hotspot.Part_ID;
  }

  /** Click a count badge → filter the table to that part (toggle off if same). */
  selectPart(image: HeatImage, hotspot: HeatHotspot): void {
    if (this.countFor(image, hotspot) === 0) return;
    if (this.isSelected(image, hotspot)) {
      this.clearFilter();
      return;
    }
    this.selectedImageId = image.Area_Image_ID;
    this.selectedPartId = hotspot.Part_ID;
    this.selectedPartName = hotspot.Part_Name;
  }

  clearFilter(): void {
    this.selectedImageId = null;
    this.selectedPartId = null;
    this.selectedPartName = '';
  }

  get filteredDefects(): ImageDefectRow[] {
    if (!this.report) return [];
    if (this.selectedPartId == null) return this.report.Defects;
    return this.report.Defects.filter(
      (d) => d.Area_Image_ID === this.selectedImageId && d.Part_ID === this.selectedPartId,
    );
  }

  reworkClass(status: string): string {
    return status === 'Done'
      ? 'bg-emerald-100 text-emerald-700'
      : 'bg-amber-100 text-amber-700';
  }

  private static readonly MONTHS = [
    'Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun',
    'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec',
  ];
  fmtDate(value: string | null): string {
    if (!value) return '';
    const d = new Date(value);
    if (isNaN(d.getTime())) return '';
    const day = String(d.getDate()).padStart(2, '0');
    const mon = ImageheatmapreportComponent.MONTHS[d.getMonth()];
    return `${day}-${mon}-${d.getFullYear()}`;
  }

  // ─── PNG snapshot: background image + count badges drawn to canvas ─────────
  async downloadImagePng(image: HeatImage): Promise<void> {
    try {
      const src = new Image();
      src.crossOrigin = 'anonymous';
      src.src = this.imageUrl(image.Image_FileName);
      await src.decode();

      const W = src.naturalWidth || 1000;
      const H = src.naturalHeight || 1000;
      const canvas = document.createElement('canvas');
      canvas.width = W;
      canvas.height = H;
      const ctx = canvas.getContext('2d');
      if (!ctx) return;
      ctx.drawImage(src, 0, 0, W, H);

      const r = Math.max(W, H) * 0.025;
      ctx.font = `bold ${Math.round(r * 1.1)}px Arial`;
      ctx.textAlign = 'center';
      ctx.textBaseline = 'middle';

      image.Hotspots.forEach((h) => {
        const count = this.countFor(image, h);
        if (count === 0) return;
        const x = h.CenterX * W;
        const y = h.CenterY * H;
        ctx.beginPath();
        ctx.arc(x, y, r, 0, Math.PI * 2);
        ctx.fillStyle = '#dc2626';
        ctx.fill();
        ctx.lineWidth = Math.max(1, r * 0.15);
        ctx.strokeStyle = '#ffffff';
        ctx.stroke();
        ctx.fillStyle = '#ffffff';
        ctx.fillText(String(count), x, y + r * 0.05);
      });

      await new Promise<void>((resolve) => {
        canvas.toBlob((blob) => {
          if (blob) {
            const safe = (image.Image_Name || 'image').replace(/[^a-z0-9]+/gi, '-');
            saveAs(blob, `ImageDefects_${safe}_${this.startDate}.png`);
          }
          resolve();
        }, 'image/png');
      });
    } catch {
      this.toast.showError('Snapshot failed', 'Could not render the image to PNG.');
    }
  }

  // ─── Excel: summary + defect list ─────────────────────────────────────────
  async downloadExcel(): Promise<void> {
    if (!this.report) return;
    const r = this.report;
    const workbook = new Workbook();
    const HEADER_FILL = 'FF1D4ED8';
    const styleHeader = (row: import('exceljs').Row) => {
      row.eachCell((cell) => {
        cell.font = { name: 'Arial', bold: true, color: { argb: 'FFFFFFFF' } };
        cell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: HEADER_FILL } };
        cell.alignment = { vertical: 'middle', horizontal: 'center', wrapText: true };
        cell.border = {
          top: { style: 'thin' }, left: { style: 'thin' },
          bottom: { style: 'thin' }, right: { style: 'thin' },
        };
      });
    };

    const s1 = workbook.addWorksheet('Summary');
    s1.mergeCells('A1:B1');
    const t = s1.getCell('A1');
    t.value = 'Image Defect Report (Paint)';
    t.font = { name: 'Arial', bold: true, size: 14 };
    s1.addRow([]);
    s1.addRow(['Model', r.Model_Name]);
    s1.addRow(['Vehicle Area', r.Vehicle_Area_Name]);
    s1.addRow(['Range', `${this.fmtDate(r.StartDate)} to ${this.fmtDate(r.EndDate)}`]);
    s1.addRow(['Total Defects', r.TotalDefects]);
    s1.getColumn(1).font = { bold: true };

    const s2 = workbook.addWorksheet('Defects');
    styleHeader(s2.addRow([
      'Sr.No', 'VIN No', 'Part', 'Defect', 'Location', 'Severity',
      'Attribution', 'Defect Type', 'Auditor', 'Remark', 'Rework',
    ]));
    r.Defects.forEach((d, i) => {
      s2.addRow([
        i + 1, d.VIN_No, d.Part_Name, d.Defect_Name, d.Location_Name,
        d.Severity_Code, d.Attribution_Name, d.Defect_Type_Name,
        d.Auditor_Name, d.Remark, d.Rework_Status,
      ]);
    });
    s2.columns.forEach((c) => (c.width = 16));
    s2.getColumn(1).width = 6;
    s2.getColumn(2).width = 20;
    s2.getColumn(10).width = 30;
    s2.views = [{ state: 'frozen', ySplit: 1 }];

    const buffer = await workbook.xlsx.writeBuffer();
    const modelPart = this.selectedModelName.replace(/[^a-z0-9]+/gi, '-');
    const areaPart = this.selectedAreaName.replace(/[^a-z0-9]+/gi, '-');
    saveAs(
      new Blob([buffer], {
        type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
      }),
      `Image_Defects_${modelPart}_${areaPart}_${this.startDate}_to_${this.endDate}.xlsx`,
    );
  }
}
