import { CommonModule } from '@angular/common';
import { Component, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Workbook } from 'exceljs';
import { saveAs } from 'file-saver';
import { ToastService } from '../../../services';
import { VehicleTrackingService } from '../../VehicleTracking/vehicle-tracking.service';
import { ModelItem } from '../../VehicleTracking/vehicle-tracking.models';
import {
  DailyDefectRow,
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
    this.clearSeverity();
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
    this.clearSeverity();
  }

  get hasDefects(): boolean {
    return (this.report?.Defects?.length ?? 0) > 0;
  }

  // ─── Headline card values ───────────────────────────────────────────────
  private stageDone(auditAreaId: number): number {
    return this.report?.StageCounts.find((s) => s.Audit_Area_ID === auditAreaId)?.Completed_Count ?? 0;
  }

  /** Road Test (Audit_Area_ID = 1) vehicles completed among the day's vehicles. */
  get roadTestDone(): number {
    return this.stageDone(1);
  }

  /** Static Audit (Audit_Area_ID = 6) vehicles completed. */
  get staticCheckDone(): number {
    return this.stageDone(6);
  }

  /** Rework (Audit_Area_ID = 9) vehicles with all concerns reworked. */
  get reworkDone(): number {
    return this.stageDone(9);
  }

  /** WIP = the day's vehicles that have not yet reached Physical RFD. */
  get wipCount(): number {
    if (!this.report) return 0;
    return Math.max(0, this.report.VehiclesActiveCount - this.report.RfdDoneCount);
  }

  // ─── Defect summary + severity filter ───────────────────────────────────
  /** Selected severity code for the table filter; '' = Unrated, null = all. */
  selectedSeverity: string | null = null;

  /** Fixed display order for severity cards; anything else falls to the end. */
  private static readonly SEVERITY_ORDER = ['V1+', 'V1', 'V2', 'V3'];

  private severityRank(code: string): number {
    const i = DailyreportComponent.SEVERITY_ORDER.indexOf((code || '').toUpperCase());
    return i === -1 ? 999 : i;
  }

  /**
   * Severity-wise concern counts, derived from the day's defects, ordered
   * V1+ → V1 → V2 → V3 (unknown/blank severities after, by count).
   */
  get severitySummary(): { code: string; label: string; count: number }[] {
    if (!this.report) return [];
    const map = new Map<string, number>();
    for (const d of this.report.Defects) {
      const code = (d.Severity_Code || '').trim();
      map.set(code, (map.get(code) ?? 0) + 1);
    }
    return Array.from(map.entries())
      .map(([code, count]) => ({ code, label: code || 'Unrated', count }))
      .sort((a, b) => {
        const ra = this.severityRank(a.code);
        const rb = this.severityRank(b.code);
        return ra !== rb ? ra - rb : b.count - a.count;
      });
  }

  isSeveritySelected(code: string): boolean {
    return this.selectedSeverity !== null && this.selectedSeverity === code;
  }

  /** Click a severity card → filter the table (toggle off if already active). */
  selectSeverity(code: string): void {
    this.selectedSeverity = this.selectedSeverity === code ? null : code;
  }

  clearSeverity(): void {
    this.selectedSeverity = null;
  }

  /** Defects after the severity card filter. */
  get filteredDefects(): DailyDefectRow[] {
    if (!this.report) return [];
    if (this.selectedSeverity === null) return this.report.Defects;
    const target = this.selectedSeverity;
    return this.report.Defects.filter((d) => (d.Severity_Code || '').trim() === target);
  }

  get selectedSeverityLabel(): string {
    if (this.selectedSeverity === null) return '';
    return this.selectedSeverity || 'Unrated';
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
    s1.addRow(['Road Test Done', this.roadTestDone]);
    s1.addRow(['Static Check Done', this.staticCheckDone]);
    s1.addRow(['Rework Done', this.reworkDone]);
    s1.addRow(['RFD Done', r.RfdDoneCount]);
    s1.addRow(['WIP', this.wipCount]);
    s1.addRow(['Total Defects', r.TotalDefectsCount]);
    s1.getColumn(1).font = { bold: true };

    s1.addRow([]);
    const sevHdr = s1.addRow(['Severity', 'Concerns']);
    styleHeader(sevHdr);
    this.severitySummary.forEach((sv) => s1.addRow([sv.label, sv.count]));
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

    this.filteredDefects.forEach((d, i) => {
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
