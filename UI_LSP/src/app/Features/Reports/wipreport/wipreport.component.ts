import { CommonModule } from '@angular/common';
import { Component, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Workbook } from 'exceljs';
import { saveAs } from 'file-saver';
import { ToastService } from '../../../services';
import { VehicleTrackingService } from '../../VehicleTracking/vehicle-tracking.service';
import { ModelItem } from '../../VehicleTracking/vehicle-tracking.models';
import { ReportsService, WipReportResponse, WipRow } from '../reports.service';
import { interval, Subscription } from 'rxjs';
@Component({
  selector: 'app-wipreport',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './wipreport.component.html',
  styleUrl: './wipreport.component.css',
})
export class WipreportComponent implements OnInit {
  private readonly reportsService = inject(ReportsService);
  private readonly vehicleService = inject(VehicleTrackingService);
  private readonly toast = inject(ToastService);

  models: ModelItem[] = [];
  selectedModelId = 0;
  startDate = '';
  endDate = '';

  report: WipReportResponse | null = null;
  loading = false;
  searched = false;


  private pollingSubscription!: Subscription;

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

    // // Refresh every 10 seconds
    // this.pollingSubscription = interval(10000).subscribe(() => {
    //   this.loadWipReport();
    // });

    // // Initial load
    // this.loadWipReport();
  }

  loadWipReport() {
    if (this.selectedModelId && this.startDate && this.endDate) {
      this.loading = true;

      this.reportsService
        .getWipReport({
          Model_ID: this.selectedModelId,
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
  }

  ngOnDestroy(): void {
    if (this.pollingSubscription) {
      this.pollingSubscription.unsubscribe();
    }
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
    this.clearStuckStage();
    this.reportsService
      .getWipReport({
        Model_ID: this.selectedModelId,
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
    this.report = null;
    this.searched = false;
    this.clearStuckStage();
  }

  get hasRows(): boolean {
    return (this.report?.Rows?.length ?? 0) > 0;
  }

  // ─── Stuck-stage card filter ────────────────────────────────────────────
  /** Selected stuck-stage name for the table filter; null = all. */
  selectedStuckStage: string | null = null;

  isStuckSelected(name: string): boolean {
    return this.selectedStuckStage === name;
  }

  /** Click a stuck-stage card → filter the table (toggle off if active). */
  selectStuckStage(name: string): void {
    this.selectedStuckStage = this.selectedStuckStage === name ? null : name;
  }

  clearStuckStage(): void {
    this.selectedStuckStage = null;
  }

  /** WIP rows after the stuck-stage card filter. */
  get filteredRows(): WipRow[] {
    if (!this.report) return [];
    if (this.selectedStuckStage === null) return this.report.Rows;
    return this.report.Rows.filter((r) => r.Stuck_Stage_Name === this.selectedStuckStage);
  }

  /** Age since Road Test → now, as "Xd Yh Zm". '–' when no Road Test date. */
  fmtAge(value: string | null): string {
    if (!value) return '–';
    const start = new Date(value).getTime();
    if (isNaN(start)) return '–';
    let mins = Math.round((Date.now() - start) / 60000);
    if (mins < 0) mins = 0;
    const days = Math.floor(mins / 1440);
    mins -= days * 1440;
    const hours = Math.floor(mins / 60);
    mins -= hours * 60;
    if (days > 0) return `${days}d ${hours}h ${mins}m`;
    if (hours > 0) return `${hours}h ${mins}m`;
    return `${mins}m`;
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
    const mon = WipreportComponent.MONTHS[d.getMonth()];
    return `${day}-${mon}-${d.getFullYear()}`;
  }

  /** Amber ≥7 idle days, red ≥14 — draws the eye to stale vehicles. */
  ageClass(days: number): string {
    if (days >= 14) return 'text-red-600 font-semibold';
    if (days >= 7) return 'text-amber-600 font-semibold';
    return 'text-gray-600';
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

    // Summary sheet
    const s1 = workbook.addWorksheet('Summary');
    s1.mergeCells('A1:B1');
    const t = s1.getCell('A1');
    t.value = 'WIP / Pending Report';
    t.font = { name: 'Arial', bold: true, size: 14 };
    s1.addRow([]);
    s1.addRow(['Model', r.Model_Name]);
    s1.addRow(['Range', `${this.fmtDate(r.StartDate)} to ${this.fmtDate(r.EndDate)}`]);
    s1.addRow(['Total WIP Vehicles', r.TotalWip]);
    s1.getColumn(1).font = { bold: true };
    s1.addRow([]);
    const h1 = s1.addRow(['Stuck At Stage', 'Vehicles']);
    styleHeader(h1);
    r.StuckCounts.forEach((s) => s1.addRow([s.Stage_Name, s.Count]));
    s1.getColumn(1).width = 26;
    s1.getColumn(2).width = 14;

    // Detail sheet
    const s2 = workbook.addWorksheet('WIP Vehicles');
    const headers = [
      'Sr.No', 'VIN No', 'Model', 'Roll Down Date', 'Road Test Date', 'Age (since RT)',
      'Last Stage', 'Last Stage Date', 'Stuck At', 'Last Activity', 'Idle (days)',
      'Pending Concerns',
    ];
    styleHeader(s2.addRow(headers));
    this.filteredRows.forEach((row, i) => {
      s2.addRow([
        i + 1,
        row.VIN_No,
        row.Model_Name,
        this.fmtDate(row.Roll_Down_Date),
        this.fmtDate(row.RoadTest_Date),
        this.fmtAge(row.RoadTest_Date),
        row.Last_Stage_Name,
        this.fmtDate(row.Last_Stage_Date),
        row.Stuck_Stage_Name,
        this.fmtDate(row.Last_Activity_Date),
        row.Idle_Days,
        row.Pending_Concerns,
      ]);
    });
    s2.columns.forEach((c) => (c.width = 15));
    s2.getColumn(1).width = 6;
    s2.getColumn(2).width = 20;
    s2.views = [{ state: 'frozen', ySplit: 1 }];

    const buffer = await workbook.xlsx.writeBuffer();
    const modelPart = this.selectedModelName.replace(/[^a-z0-9]+/gi, '-');
    saveAs(
      new Blob([buffer], {
        type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
      }),
      `WIP_Report_${modelPart}_${this.startDate}_to_${this.endDate}.xlsx`,
    );
  }
}
