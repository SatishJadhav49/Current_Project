import { CommonModule } from '@angular/common';
import { Component, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { ToastService } from '../../services';
import {
  ActionPlanDetail,
  ActionPlanService,
  ProblemHistoryRow,
} from './action-plan.service';

interface DraftRoot { Root_Cause: string; }
interface DraftIca { Action: string; Responsibility: string; ICA_Date: string | null; }
interface DraftPca { Action: string; Responsibility: string; Target_Date: string | null; }

@Component({
  selector: 'app-action-plan-edit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './action-plan-edit.component.html',
})
export class ActionPlanEditComponent implements OnInit {
  private readonly service = inject(ActionPlanService);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  // Problem identity + labels (from query params).
  modelId = 0;
  partId = 0;
  defectId = 0;
  attributionId = 0;
  severityId = 0;
  modelName = '';
  partName = '';
  defectName = '';
  attributionName = '';
  severityCode = '';

  loadingPlan = true;
  saving = false;
  planDetail: ActionPlanDetail | null = null;

  draftRoots: DraftRoot[] = [];
  draftIcas: DraftIca[] = [];
  draftPcas: DraftPca[] = [];

  // ── Problem history modal ──
  showHistory = false;
  loadingHistory = false;
  private historyLoaded = false;
  historyRows: ProblemHistoryRow[] = [];

  /** Open the history modal; rows are fetched once and cached for the visit. */
  openHistory(): void {
    this.showHistory = true;
    if (this.historyLoaded) return;
    this.loadingHistory = true;
    this.service
      .getProblemHistory(this.modelId, this.partId, this.defectId, this.attributionId, this.severityId)
      .subscribe({
        next: (rows) => {
          this.historyRows = rows ?? [];
          this.historyLoaded = true;
          this.loadingHistory = false;
        },
        error: () => (this.loadingHistory = false),
      });
  }

  closeHistory(): void {
    this.showHistory = false;
  }

  ngOnInit(): void {
    const q = this.route.snapshot.queryParamMap;
    this.modelId = Number(q.get('modelId')) || 0;
    this.partId = Number(q.get('partId')) || 0;
    this.defectId = Number(q.get('defectId')) || 0;
    this.attributionId = Number(q.get('attributionId')) || 0;
    this.severityId = Number(q.get('severityId')) || 0;
    this.modelName = q.get('modelName') ?? '';
    this.partName = q.get('partName') ?? '';
    this.defectName = q.get('defectName') ?? '';
    this.attributionName = q.get('attributionName') ?? '';
    this.severityCode = q.get('severityCode') ?? '';

    if (this.modelId <= 0 || this.partId <= 0 || this.defectId <= 0 || this.attributionId <= 0) {
      this.goBack();
      return;
    }

    this.service.getPlan(this.modelId, this.partId, this.defectId, this.attributionId, this.severityId).subscribe({
      next: (d) => {
        this.planDetail = d ?? null;
        const existing = this.isExistingPlan;
        this.draftRoots = existing ? [] : [{ Root_Cause: '' }];
        this.draftIcas = existing ? [] : [{ Action: '', Responsibility: '', ICA_Date: null }];
        this.draftPcas = existing ? [] : [{ Action: '', Responsibility: '', Target_Date: null }];
        this.loadingPlan = false;
      },
      error: () => (this.loadingPlan = false),
    });
  }

  get isExistingPlan(): boolean {
    return !!this.planDetail && this.planDetail.Is_Submitted && this.planDetail.Action_Plan_ID > 0;
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
    return `${day}-${ActionPlanEditComponent.MONTHS[d.getMonth()]}-${d.getFullYear()}`;
  }

  /** Compact date + time for the added-by meta (dd-MMM-yy h:mm AM/PM). */
  fmtDateTime(value: string | null): string {
    if (!value) return '';
    const d = new Date(value);
    if (isNaN(d.getTime())) return '';
    const day = String(d.getDate()).padStart(2, '0');
    const mon = ActionPlanEditComponent.MONTHS[d.getMonth()];
    const yy = String(d.getFullYear()).slice(-2);
    let h = d.getHours();
    const ampm = h >= 12 ? 'PM' : 'AM';
    h = h % 12 || 12;
    const mm = String(d.getMinutes()).padStart(2, '0');
    return `${day}-${mon}-${yy} ${h}:${mm} ${ampm}`;
  }

  statusPill(status: string): string {
    switch ((status || '').toUpperCase()) {
      case 'G': return 'bg-emerald-100 text-emerald-700';
      case 'Y': return 'bg-amber-100 text-amber-700';
      default: return 'bg-red-100 text-red-700';
    }
  }

  // ── Draft row add/remove ──
  addRoot(): void { this.draftRoots.push({ Root_Cause: '' }); }
  removeRoot(i: number): void { this.draftRoots.splice(i, 1); }
  addIca(): void { this.draftIcas.push({ Action: '', Responsibility: '', ICA_Date: null }); }
  removeIca(i: number): void { this.draftIcas.splice(i, 1); }
  addPca(): void { this.draftPcas.push({ Action: '', Responsibility: '', Target_Date: null }); }

  /**
   * Auto status per PCA row from its target date (no manual selection):
   * R until the target date passes, Y after it, G after target + 30 days.
   * A SQL job will later maintain the stored value with the full concern
   * recurrence check; this keeps the display consistent until then.
   */
  computePcaStatus(dateStr: string | null): string {
    if (!dateStr) return 'R';
    const t = new Date(dateStr);
    if (isNaN(t.getTime())) return 'R';
    const target = new Date(t.getFullYear(), t.getMonth(), t.getDate(), 23, 59, 59);
    const now = new Date();
    if (now <= target) return 'R';
    const green = new Date(target);
    green.setDate(green.getDate() + 30);
    return now > green ? 'G' : 'Y';
  }
  removePca(i: number): void { this.draftPcas.splice(i, 1); }

  goBack(): void {
    this.router.navigate(['/action/actionplan'], { queryParams: { modelId: this.modelId } });
  }

  submit(): void {
    const roots = this.draftRoots.filter((r) => r.Root_Cause.trim());
    const icas = this.draftIcas.filter((i) => i.Action.trim() || i.Responsibility.trim() || i.ICA_Date);
    const pcas = this.draftPcas.filter((p) => p.Action.trim() || p.Responsibility.trim() || p.Target_Date);

    if (!this.isExistingPlan) {
      if (!roots.length || !icas.length || !pcas.length) {
        this.toast.showError('Incomplete', 'Root Cause, ICA and PCA each need at least one entry.');
        return;
      }
    } else if (!roots.length && !icas.length && !pcas.length) {
      this.toast.showError('Nothing to add', 'Add at least one new row.');
      return;
    }

    this.saving = true;
    this.service
      .submitPlan({
        Model_ID: this.modelId,
        Part_ID: this.partId,
        Defect_ID: this.defectId,
        Attribution_ID: this.attributionId,
        Severity_ID: this.severityId,
        RootCauses: roots.map((r) => ({ Root_Cause: r.Root_Cause.trim() })),
        Icas: icas.map((i) => ({ Action: i.Action.trim(), Responsibility: i.Responsibility.trim(), ICA_Date: i.ICA_Date || null })),
        Pcas: pcas.map((p) => ({ Action: p.Action.trim(), Responsibility: p.Responsibility.trim(), Target_Date: p.Target_Date || null, Status: this.computePcaStatus(p.Target_Date) })),
        Inserted_Host: localStorage.getItem('Hostname') ?? '',
        Inserted_User_ID: Number(localStorage.getItem('Employee_ID')) || undefined,
        Plant_Code: localStorage.getItem('Plant_Code') ?? '',
      })
      .subscribe({
        next: () => {
          this.saving = false;
          this.toast.showSuccess('Saved', 'Action plan saved.');
          this.goBack();
        },
        error: () => (this.saving = false),
      });
  }
}
