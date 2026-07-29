import { CommonModule } from '@angular/common';
import { Component, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Workbook } from 'exceljs';
import { saveAs } from 'file-saver';
import { ToastService } from '../../../services';
import { VehicleTrackingService } from '../../VehicleTracking/vehicle-tracking.service';
import { ModelItem } from '../../VehicleTracking/vehicle-tracking.models';
import { ReportsService, TatReportResponse } from '../reports.service';

@Component({
  selector: 'app-tatreport',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './tatreport.component.html',
  styleUrl: './tatreport.component.css',
})
export class TatreportComponent implements OnInit {
  private readonly reportsService = inject(ReportsService);
  private readonly vehicleService = inject(VehicleTrackingService);
  private readonly toast = inject(ToastService);

  models: ModelItem[] = [];
  selectedModelId = 0;
  startDate = '';
  endDate = '';
  /** Optional VIN — when set, TAT is computed for that vehicle only. */
  vinNo = '';

  report: TatReportResponse | null = null;
  loading = false;
  searched = false;

  ngOnInit(): void {
    // Default range = last 30 days.
    const now = new Date();
    const from = new Date();
    from.setDate(now.getDate() - 30);
    this.startDate = this.toInputDate(from);
    this.endDate = this.toInputDate(now);

    this.vehicleService.getModels().subscribe({
      next: (list) => (this.models = list ?? []),
      error: () => (this.models = []),
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

  search(): void {
    const vin = this.vinNo.trim();
    const hasVin = vin.length > 0;

    // VIN overrides Model + date range; otherwise Model + range are required.
    if (!hasVin) {
      if (this.selectedModelId <= 0) {
        this.toast.showError('Model or VIN required', 'Select a model or enter a VIN.');
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
    }

    this.loading = true;
    this.searched = true;
    this.reportsService
      .getTatReport({
        Model_ID: this.selectedModelId,
        StartDate: this.startDate,
        EndDate: this.endDate,
        Vin_No: vin,
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
    this.vinNo = '';
  }

  get hasTransitions(): boolean {
    return (this.report?.Transitions?.length ?? 0) > 0;
  }

  /** Highest avg among transitions with data = the bottleneck to flag. */
  get bottleneckAvg(): number {
    if (!this.report) return 0;
    return this.report.Transitions.reduce(
      (max, t) => (t.Vehicle_Count > 0 && t.Avg_Hours > max ? t.Avg_Hours : max),
      0,
    );
  }

  /** Hours → human duration: "3.2 h", "1 d 4 h", "6 d". Blank when no data. */
  fmtDuration(hours: number, count: number): string {
    if (count === 0) return '—';
    if (hours < 24) return `${hours.toFixed(1)} h`;
    const days = Math.floor(hours / 24);
    const rem = Math.round(hours - days * 24);
    return rem > 0 ? `${days} d ${rem} h` : `${days} d`;
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
    const mon = TatreportComponent.MONTHS[d.getMonth()];
    return `${day}-${mon}-${d.getFullYear()}`;
  }

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

    const sheet = workbook.addWorksheet('Stage TAT');
    sheet.mergeCells('A1:F1');
    const t = sheet.getCell('A1');
    t.value = 'Stage TAT (Turnaround) Report';
    t.font = { name: 'Arial', bold: true, size: 14 };
    sheet.addRow([]);
    sheet.addRow(['Model', r.Model_Name]);
    sheet.addRow(['Range', `${this.fmtDate(r.StartDate)} to ${this.fmtDate(r.EndDate)}`]);
    sheet.addRow(['Vehicles in range', r.VehicleCount]);
    sheet.addRow([
      'Overall cycle (Roll Down → RFD)',
      this.fmtDuration(r.OverallAvgHours ?? 0, r.OverallCount),
      `${r.OverallCount} vehicles`,
    ]);
    sheet.getColumn(1).font = { bold: true };
    sheet.addRow([]);

    styleHeader(sheet.addRow(['From Stage', 'To Stage', 'Avg', 'Min', 'Max', 'Vehicles']));
    r.Transitions.forEach((tr) => {
      sheet.addRow([
        tr.From_Stage,
        tr.To_Stage,
        this.fmtDuration(tr.Avg_Hours, tr.Vehicle_Count),
        this.fmtDuration(tr.Min_Hours, tr.Vehicle_Count),
        this.fmtDuration(tr.Max_Hours, tr.Vehicle_Count),
        tr.Vehicle_Count,
      ]);
    });
    sheet.getColumn(1).width = 22;
    sheet.getColumn(2).width = 22;
    [3, 4, 5, 6].forEach((c) => (sheet.getColumn(c).width = 14));

    const buffer = await workbook.xlsx.writeBuffer();
    const modelPart = this.selectedModelName.replace(/[^a-z0-9]+/gi, '-');
    saveAs(
      new Blob([buffer], {
        type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
      }),
      `Stage_TAT_${modelPart}_${this.startDate}_to_${this.endDate}.xlsx`,
    );
  }
}
