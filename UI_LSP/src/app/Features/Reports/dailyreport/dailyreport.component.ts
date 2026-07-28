import { CommonModule } from '@angular/common';
import { Component, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Workbook } from 'exceljs';
import { saveAs } from 'file-saver';
import { ToastService } from '../../../services';
import { VehicleTrackingService } from '../../VehicleTracking/vehicle-tracking.service';
import { ModelItem } from '../../VehicleTracking/vehicle-tracking.models';
import {
  DailyReportResponse,
  ReportsService,
} from '../reports.service';

@Component({
  selector: 'app-dailyreport',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './dailyreport.component.html',
  styleUrl: './dailyreport.component.css',
})
export class DailyreportComponent implements OnInit {
  private readonly reportsService = inject(ReportsService);
  private readonly vehicleService = inject(VehicleTrackingService);
  private readonly toast = inject(ToastService);

  // ─── Filters ──────────────────────────────────────────────────────────
  models: ModelItem[] = [];
  selectedModelId = 0;
  reportDate = '';

  // ─── Results ──────────────────────────────────────────────────────────
  report: DailyReportResponse | null = null;
  loading = false;
  searched = false;

  ngOnInit(): void {
    this.reportDate = this.toInputDate(new Date());

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
    if (!this.reportDate) {
      this.toast.showError('Date required', 'Please select a date.');
      return;
    }

    this.loading = true;
    this.searched = true;
    this.reportsService
      .getDailyReport({
        Model_ID: this.selectedModelId,
        ReportDate: this.reportDate,
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
    this.report = null;
    this.searched = false;
  }

  get hasDefects(): boolean {
    return (this.report?.Defects?.length ?? 0) > 0;
  }

  private static readonly MONTHS = [
    'Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun',
    'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec',
  ];

  /** Short date (dd-MMM-yyyy); blank when null. */
  fmtDate(value: string | null): string {
    if (!value) return '';
    const d = new Date(value);
    if (isNaN(d.getTime())) return '';
    const day = String(d.getDate()).padStart(2, '0');
    const mon = DailyreportComponent.MONTHS[d.getMonth()];
    return `${day}-${mon}-${d.getFullYear()}`;
  }

  /** Full date + time (dd-MMM-yyyy hh:mm a); blank when null. */
  fmtDateTime(value: string | null): string {
    if (!value) return '';
    const d = new Date(value);
    if (isNaN(d.getTime())) return '';
    const day = String(d.getDate()).padStart(2, '0');
    const mon = DailyreportComponent.MONTHS[d.getMonth()];
    let h = d.getHours();
    const ampm = h >= 12 ? 'PM' : 'AM';
    h = h % 12 || 12;
    const hh = String(h).padStart(2, '0');
    const mm = String(d.getMinutes()).padStart(2, '0');
    return `${day}-${mon}-${d.getFullYear()} ${hh}:${mm} ${ampm}`;
  }

  /** Tailwind classes for the rework status chip. */
  reworkClass(status: string): string {
    return status === 'Done'
      ? 'bg-emerald-100 text-emerald-700'
      : 'bg-amber-100 text-amber-700';
  }

  // ─── Excel export — Sheet 1 Summary, Sheet 2 Defects ────────────────────
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

    // ── Sheet 1: Summary ──
    const s1 = workbook.addWorksheet('Summary');
    s1.mergeCells('A1:B1');
    const t = s1.getCell('A1');
    t.value = 'Daily Report';
    t.font = { name: 'Arial', bold: true, size: 14 };

    s1.addRow([]);
    s1.addRow(['Model', r.Model_Name]);
    s1.addRow(['Date', this.fmtDate(r.ReportDate)]);
    s1.addRow(['Physical RFD Done', r.RfdDoneCount]);
    s1.addRow(['Vehicles Active', r.VehiclesActiveCount]);
    s1.addRow(['Total Defects', r.TotalDefectsCount]);
    s1.getColumn(1).font = { bold: true };

    s1.addRow([]);
    const stageHdr = s1.addRow(['Stage', 'Completed']);
    styleHeader(stageHdr);
    r.StageCounts.forEach((sc) => s1.addRow([sc.Stage_Name, sc.Completed_Count]));
    s1.getColumn(1).width = 24;
    s1.getColumn(2).width = 18;

    // ── Sheet 2: Defects ──
    const s2 = workbook.addWorksheet('Defects');
    const headers = [
      'Sr.No', 'VIN No', 'Stage', 'Vehicle Area', 'Part', 'Defect',
      'Location', 'Severity', 'Attribution', 'Defect Type', 'Auditor',
      'Remark', 'Rework', 'Entered On',
    ];
    const hRow = s2.addRow(headers);
    styleHeader(hRow);

    r.Defects.forEach((d, i) => {
      s2.addRow([
        i + 1,
        d.VIN_No,
        d.Stage_Name,
        d.Vehicle_Area_Name,
        d.Part_Name,
        d.Defect_Name,
        d.Location_Name,
        d.Severity_Code,
        d.Attribution_Name,
        d.Defect_Type_Name,
        d.Auditor_Name,
        d.Remark,
        d.Rework_Status,
        this.fmtDateTime(d.Entered_Date),
      ]);
    });

    s2.columns.forEach((col) => (col.width = 16));
    s2.getColumn(1).width = 6;
    s2.getColumn(2).width = 20; // VIN
    s2.getColumn(12).width = 30; // Remark
    s2.getColumn(14).width = 21; // Entered On
    s2.views = [{ state: 'frozen', ySplit: 1 }];

    const buffer = await workbook.xlsx.writeBuffer();
    const modelPart = this.selectedModelName.replace(/[^a-z0-9]+/gi, '-');
    saveAs(
      new Blob([buffer], {
        type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
      }),
      `Daily_Report_${modelPart}_${this.reportDate}.xlsx`,
    );
  }
}
