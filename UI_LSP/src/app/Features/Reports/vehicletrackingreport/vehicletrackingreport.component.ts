import { CommonModule } from '@angular/common';
import { Component, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Workbook } from 'exceljs';
import { saveAs } from 'file-saver';
import { ToastService } from '../../../services';
import { VehicleTrackingService } from '../../VehicleTracking/vehicle-tracking.service';
import { ModelItem } from '../../VehicleTracking/vehicle-tracking.models';
import {
  ReportsService,
  VehicleTrackingReportRow,
} from '../reports.service';

@Component({
  selector: 'app-vehicletrackingreport',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './vehicletrackingreport.component.html',
  styleUrl: './vehicletrackingreport.component.css',
})
export class VehicletrackingreportComponent implements OnInit {
  private readonly reportsService = inject(ReportsService);
  private readonly vehicleService = inject(VehicleTrackingService);
  private readonly toast = inject(ToastService);

  // ─── Filters ──────────────────────────────────────────────────────────
  models: ModelItem[] = [];
  selectedModelId = 0;
  startDate = '';
  endDate = '';

  /** Stage + status client-side filters (applied to already-fetched rows). */
  readonly stageOptions: { key: string; label: string }[] = [
    { key: 'RoadTest', label: 'Road Test' },
    { key: 'UnderBody', label: 'Under Body' },
    { key: 'Dtc', label: 'DTC' },
    { key: 'Shower', label: 'Shower' },
    { key: 'StaticAudit', label: 'Static Audit' },
    { key: 'Vms', label: 'VMS' },
    { key: 'Paint', label: 'Paint' },
    { key: 'Recheck', label: 'Recheck' },
    { key: 'Rework', label: 'Rework' },
    { key: 'Rfd', label: 'Physical RFD' },
  ];
  selectedStage = '';       // '' = All Stages
  selectedStatus = 'All';   // All | Pending | Done

  // ─── Results ──────────────────────────────────────────────────────────
  rows: VehicleTrackingReportRow[] = [];
  loading = false;
  searched = false;

  /**
   * Rows after the on-screen Stage + Status filter. Filtering only kicks in
   * when a specific stage is chosen AND a specific status (Pending/Done) is
   * selected; otherwise every fetched row shows.
   */
  get displayedRows(): VehicleTrackingReportRow[] {
    if (!this.selectedStage || this.selectedStatus === 'All') return this.rows;
    const prop = `${this.selectedStage}_Status`;
    return this.rows.filter(
      (r) => (r as unknown as Record<string, string>)[prop] === this.selectedStatus,
    );
  }

  ngOnInit(): void {
    // Default the range to the current month for convenience.
    const now = new Date();
    this.startDate = this.toInputDate(new Date(now.getFullYear(), now.getMonth(), 1));
    this.endDate = this.toInputDate(now);

    this.vehicleService.getModels().subscribe({
      next: (list) => (this.models = list ?? []),
      error: () => (this.models = []),
    });
  }

  /** Format a Date as yyyy-MM-dd for <input type="date">. */
  private toInputDate(d: Date): string {
    const m = String(d.getMonth() + 1).padStart(2, '0');
    const day = String(d.getDate()).padStart(2, '0');
    return `${d.getFullYear()}-${m}-${day}`;
  }

  get selectedModelName(): string {
    return this.models.find((m) => m.Model_ID === this.selectedModelId)?.Model_Name ?? '';
  }

  search(): void {
    if (this.selectedModelId <= 0) {
      this.toast.showError('Model required', 'Please select a model.');
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
    this.reportsService
      .getVehicleTrackingReport({
        Model_ID: this.selectedModelId,
        StartDate: this.startDate,
        EndDate: this.endDate,
      })
      .subscribe({
        next: (rows) => {
          this.rows = rows ?? [];
          this.loading = false;
        },
        error: () => {
          this.rows = [];
          this.loading = false;
        },
      });
  }

  clear(): void {
    this.selectedModelId = 0;
    this.rows = [];
    this.searched = false;
    this.selectedStage = '';
    this.selectedStatus = 'All';
  }

  private static readonly MONTHS = [
    'Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun',
    'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec',
  ];

  /** Short date (dd-MMM-yyyy) for the compact on-screen cell; blank when null. */
  fmtDate(value: string | null): string {
    if (!value) return '';
    const d = new Date(value);
    if (isNaN(d.getTime())) return '';
    const day = String(d.getDate()).padStart(2, '0');
    const mon = VehicletrackingreportComponent.MONTHS[d.getMonth()];
    return `${day}-${mon}-${d.getFullYear()}`;
  }

  /** Full date + time (dd-MMM-yyyy hh:mm a) for tooltip + Excel; blank when null. */
  fmtDateTime(value: string | null): string {
    if (!value) return '';
    const d = new Date(value);
    if (isNaN(d.getTime())) return '';
    const day = String(d.getDate()).padStart(2, '0');
    const mon = VehicletrackingreportComponent.MONTHS[d.getMonth()];
    let h = d.getHours();
    const ampm = h >= 12 ? 'PM' : 'AM';
    h = h % 12 || 12;
    const hh = String(h).padStart(2, '0');
    const mm = String(d.getMinutes()).padStart(2, '0');
    return `${day}-${mon}-${d.getFullYear()} ${hh}:${mm} ${ampm}`;
  }

  /**
   * TAT (turnaround) = Physical RFD date/time − Road Test date/time, shown as
   * a compact duration ("2d 5h", "6h 40m", "45m"). Blank ("–") when either
   * date is missing (not yet reached RFD or Road Test).
   */
  fmtTat(row: VehicleTrackingReportRow): string {
    if (!row.RoadTest_Date || !row.Rfd_Date) return '–';
    const start = new Date(row.RoadTest_Date).getTime();
    const end = new Date(row.Rfd_Date).getTime();
    if (isNaN(start) || isNaN(end) || end < start) return '–';

    let mins = Math.round((end - start) / 60000);
    const days = Math.floor(mins / 1440);
    mins -= days * 1440;
    const hours = Math.floor(mins / 60);
    mins -= hours * 60;

    if (days > 0) return `${days}d ${hours}h`;
    if (hours > 0) return `${hours}h ${mins}m`;
    return `${mins}m`;
  }

  /** Tailwind classes for a status chip — only Done / Pending. */
  statusClass(status: string): string {
    return status === 'Done'
      ? 'bg-emerald-100 text-emerald-700'
      : 'bg-gray-100 text-gray-500';
  }

  // ─── Excel export (matches LSP_Vehicle_Tracking.xlsx column order) ──────
  async downloadExcel(): Promise<void> {
    const exportRows = this.displayedRows;
    if (exportRows.length === 0) return;

    const headers = [
      'Sr.No', 'VIN No', 'Serial No', 'Model Name', 'Model Description',
      'Variant', 'Color', 'Engine No', 'Build Phase', 'Drive Type', 'Fuel Type',
      'Country', 'Roll Down Date', 'CAI OK date', 'TAT (RT→RFD)', 'Road Test Date', 'Road Test Status',
      'Under Body Date', 'Under Body Status', 'DTC Date', 'DTC Status', 'Shower Test Date',
      'Shower Test Status', 'Static Audit Date', 'Static Audit Status', 'VMS Date',
      'VMS status', 'Paint Verifi. Date', 'Paint Veri. Status', 'Recheck',
      'Rework', 'RFD Date', 'Physical RFD Status',
    ];

    const workbook = new Workbook();
    const sheet = workbook.addWorksheet('Vehicle Tracking');

    const headerRow = sheet.addRow(headers);
    headerRow.eachCell((cell) => {
      cell.font = { name: 'Arial', bold: true, color: { argb: 'FFFFFFFF' } };
      cell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: '008b8b' } };
      cell.alignment = { vertical: 'middle', horizontal: 'center', wrapText: true };
      cell.border = {
        top: { style: 'thin' }, left: { style: 'thin' },
        bottom: { style: 'thin' }, right: { style: 'thin' },
      };
    });

    exportRows.forEach((r, i) => {
      sheet.addRow([
        i + 1,
        r.VIN_No,
        r.Body_Serial_No,
        r.Model_Name,
        r.Model_Desc,
        r.Variant,
        r.Colour_Desc,
        r.Engine_No,
        r.Build_Phase_Name,
        r.Drive_Name,
        r.Fuel_Name,
        r.Country_Name,
        this.fmtDateTime(r.Roll_Down_Date),
        this.fmtDateTime(r.CAI_OK_Date),
        this.fmtTat(r),
        this.fmtDateTime(r.RoadTest_Date),
        r.RoadTest_Status,
        this.fmtDateTime(r.UnderBody_Date),
        r.UnderBody_Status,
        this.fmtDateTime(r.Dtc_Date),
        r.Dtc_Status,
        this.fmtDateTime(r.Shower_Date),
        r.Shower_Status,
        this.fmtDateTime(r.StaticAudit_Date),
        r.StaticAudit_Status,
        this.fmtDateTime(r.Vms_Date),
        r.Vms_Status,
        this.fmtDateTime(r.Paint_Date),
        r.Paint_Status,
        r.Recheck_Status,
        r.Rework_Status,
        this.fmtDateTime(r.Rfd_Date),
        r.Rfd_Status,
      ]);
    });

    sheet.columns.forEach((col) => {
      col.width = 16;
    });
    sheet.getColumn(1).width = 6;
    sheet.getColumn(2).width = 20; // VIN
    sheet.getColumn(5).width = 30; // VIN
    sheet.getColumn(8).width = 20; // Engine No
    sheet.getColumn(15).width = 13; // TAT
    // Date columns hold "dd-MMM-yyyy hh:mm a" — widen them to fit.
    [13, 14, 16, 18, 20, 22, 24, 26, 28, 32].forEach((c) => {
      sheet.getColumn(c).width = 21;
    });
    sheet.views = [{ state: 'frozen', ySplit: 1 }];

    // [13, 14, 15, 17, 19, 21, 23, 25, 27, 31].forEach((c) => {
    //    sheet.getColumn(1).alignment = { horizontal: 'center' };
    // });

    const buffer = await workbook.xlsx.writeBuffer();
    const stamp = `${this.startDate}_to_${this.endDate}`;
    const modelPart = this.selectedModelName.replace(/[^a-z0-9]+/gi, '-');
    saveAs(
      new Blob([buffer], {
        type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
      }),
      `Vehicle_Tracking_${modelPart}_${stamp}.xlsx`,
    );
  }
}
