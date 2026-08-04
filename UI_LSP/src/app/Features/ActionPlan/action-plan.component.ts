import { CommonModule } from '@angular/common';
import { Component, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { ToastService } from '../../services';
import { VehicleTrackingService } from '../VehicleTracking/vehicle-tracking.service';
import {
  AttributionItem,
  ModelItem,
  SeverityItem,
} from '../VehicleTracking/vehicle-tracking.models';
import { ActionPlanProblem, ActionPlanService } from './action-plan.service';

interface StatusBucket { problems: number; concerns: number; }

@Component({
  selector: 'app-action-plan',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './action-plan.component.html',
})
export class ActionPlanComponent implements OnInit {
  private readonly service = inject(ActionPlanService);
  private readonly vehicleService = inject(VehicleTrackingService);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  // Filters
  models: ModelItem[] = [];
  attributions: AttributionItem[] = [];
  severities: SeverityItem[] = [];
  selectedModelId = 0;             // required
  selectedAttributionId = 0;       // 0 = All
  selectedStatus = 'All';          // All | R | Y | G
  selectedSeverityId = -1;         // -1 = All (0 = "no severity")

  problems: ActionPlanProblem[] = [];
  loading = false;
  searched = false;

  ngOnInit(): void {
    this.vehicleService.getModels().subscribe({ next: (l) => (this.models = l ?? []) });
    this.vehicleService.getAttributions().subscribe({ next: (l) => (this.attributions = l ?? []) });
    this.vehicleService.getSeverities().subscribe({ next: (l) => (this.severities = l ?? []) });

    // Returning from the edit page — re-run the last model automatically.
    const model = Number(this.route.snapshot.queryParamMap.get('modelId'));
    if (model > 0) {
      this.selectedModelId = model;
      this.search();
    }
  }

  search(): void {
    if (this.selectedModelId <= 0) {
      this.toast.showError('Model required', 'Please select a model.');
      return;
    }
    this.loading = true;
    this.searched = true;
    this.service.getProblems(this.selectedModelId).subscribe({
      next: (list) => {
        this.problems = list ?? [];
        this.loading = false;
      },
      error: () => {
        this.problems = [];
        this.loading = false;
      },
    });
  }

  clear(): void {
    this.selectedAttributionId = 0;
    this.selectedStatus = 'All';
    this.selectedSeverityId = -1;
  }

  /** Base set after Attribution + Severity (not Status) — drives the summary. */
  private get filteredBase(): ActionPlanProblem[] {
    return this.problems.filter(
      (p) =>
        (this.selectedAttributionId === 0 || p.Attribution_ID === this.selectedAttributionId) &&
        (this.selectedSeverityId === -1 || p.Severity_ID === this.selectedSeverityId),
    );
  }

  /** Rows shown in the table (adds the Status filter on top of the base). */
  get displayedProblems(): ActionPlanProblem[] {
    return this.filteredBase.filter(
      (p) => this.selectedStatus === 'All' || p.Status === this.selectedStatus,
    );
  }

  /** Per-status problem + concern (frequency) counts for the summary strip. */
  get statusCounts(): { R: StatusBucket; Y: StatusBucket; G: StatusBucket; total: StatusBucket } {
    const base = this.filteredBase;
    const bucket = (rows: ActionPlanProblem[]): StatusBucket => ({
      problems: rows.length,
      concerns: rows.reduce((s, r) => s + r.Frequency, 0),
    });
    return {
      R: bucket(base.filter((p) => p.Status === 'R')),
      Y: bucket(base.filter((p) => p.Status === 'Y')),
      G: bucket(base.filter((p) => p.Status === 'G')),
      total: bucket(base),
    };
  }

  statusClass(status: string): string {
    switch (status) {
      case 'G': return 'bg-emerald-100 text-emerald-700';
      case 'Y': return 'bg-amber-100 text-amber-700';
      default: return 'bg-red-100 text-red-700';
    }
  }

  /** Action button background tinted by the row's status (R/Y/G). */
  statusBtnClass(status: string): string {
    switch (status) {
      case 'G': return 'bg-emerald-600 hover:bg-emerald-700';
      case 'Y': return 'bg-amber-500 hover:bg-amber-600';
      default: return 'bg-red-600 hover:bg-red-700';
    }
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
    return `${day}-${ActionPlanComponent.MONTHS[d.getMonth()]}-${d.getFullYear()}`;
  }

  get selectedModelName(): string {
    return this.models.find((m) => m.Model_ID === this.selectedModelId)?.Model_Name ?? '';
  }

  /** Open the full-page editor, carrying the problem identity + labels. */
  openPlan(p: ActionPlanProblem): void {
    this.router.navigate(['/action/edit'], {
      queryParams: {
        modelId: p.Model_ID,
        modelName: this.selectedModelName,
        partId: p.Part_ID,
        defectId: p.Defect_ID,
        attributionId: p.Attribution_ID,
        severityId: p.Severity_ID,
        partName: p.Part_Name,
        defectName: p.Defect_Name,
        attributionName: p.Attribution_Name,
        severityCode: p.Severity_Code,
      },
    });
  }
}
