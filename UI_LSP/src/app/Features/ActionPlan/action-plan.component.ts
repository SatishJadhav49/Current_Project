import { CommonModule } from '@angular/common';
import { Component, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ToastService } from '../../services';
import { VehicleTrackingService } from '../VehicleTracking/vehicle-tracking.service';
import { AttributionItem } from '../VehicleTracking/vehicle-tracking.models';
import {
  ActionPlanDetail,
  ActionPlanProblem,
  ActionPlanService,
} from './action-plan.service';

interface DraftRoot { Root_Cause: string; }
interface DraftIca { Action: string; Responsibility: string; ICA_Date: string | null; }
interface DraftPca { Action: string; Responsibility: string; Target_Date: string | null; Status: string; }

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

  attributions: AttributionItem[] = [];
  selectedAttributionId = 0;

  problems: ActionPlanProblem[] = [];
  loading = false;
  searched = false;

  // ── Modal ──
  showModal = false;
  loadingPlan = false;
  saving = false;
  activeProblem: ActionPlanProblem | null = null;
  planDetail: ActionPlanDetail | null = null;

  draftRoots: DraftRoot[] = [];
  draftIcas: DraftIca[] = [];
  draftPcas: DraftPca[] = [];

  ngOnInit(): void {
    this.vehicleService.getAttributions().subscribe({
      next: (list) => (this.attributions = list ?? []),
      error: () => (this.attributions = []),
    });
  }

  search(): void {
    if (this.selectedAttributionId <= 0) {
      this.toast.showError('Attribution required', 'Please select an attribution.');
      return;
    }
    this.loading = true;
    this.searched = true;
    this.service.getProblems(this.selectedAttributionId).subscribe({
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
    this.problems = [];
    this.searched = false;
  }

  statusClass(status: string): string {
    switch (status) {
      case 'G': return 'bg-emerald-100 text-emerald-700';
      case 'Y': return 'bg-amber-100 text-amber-700';
      default: return 'bg-red-100 text-red-700';
    }
  }

  statusLabel(status: string): string {
    switch (status) {
      case 'G': return 'G · Resolved';
      case 'Y': return 'Y · Watch';
      default: return 'R · Open';
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

  private toInputDate(value: string | null): string {
    if (!value) return '';
    const d = new Date(value);
    if (isNaN(d.getTime())) return '';
    const m = String(d.getMonth() + 1).padStart(2, '0');
    const day = String(d.getDate()).padStart(2, '0');
    return `${d.getFullYear()}-${m}-${day}`;
  }

  /** True once the plan is submitted — existing rows are locked, only append allowed. */
  get isExistingPlan(): boolean {
    return !!this.planDetail && this.planDetail.Is_Submitted && this.planDetail.Action_Plan_ID > 0;
  }

  // ── Modal open/close ──
  openPlan(p: ActionPlanProblem): void {
    this.activeProblem = p;
    this.showModal = true;
    this.planDetail = null;
    this.loadingPlan = true;
    this.service.getPlan(p.Part_ID, p.Defect_ID, p.Attribution_ID, p.Severity_ID).subscribe({
      next: (d) => {
        this.planDetail = d ?? null;
        const existing = this.isExistingPlan;
        // New plan → seed one empty row per section; existing → start empty (add-only).
        this.draftRoots = existing ? [] : [{ Root_Cause: '' }];
        this.draftIcas = existing ? [] : [{ Action: '', Responsibility: '', ICA_Date: null }];
        this.draftPcas = existing ? [] : [{ Action: '', Responsibility: '', Target_Date: null, Status: 'R' }];
        this.loadingPlan = false;
      },
      error: () => (this.loadingPlan = false),
    });
  }

  closeModal(): void {
    this.showModal = false;
    this.activeProblem = null;
    this.planDetail = null;
    this.draftRoots = [];
    this.draftIcas = [];
    this.draftPcas = [];
  }

  // ── Draft row add/remove (new rows only) ──
  addRoot(): void { this.draftRoots.push({ Root_Cause: '' }); }
  removeRoot(i: number): void { this.draftRoots.splice(i, 1); }
  addIca(): void { this.draftIcas.push({ Action: '', Responsibility: '', ICA_Date: null }); }
  removeIca(i: number): void { this.draftIcas.splice(i, 1); }
  addPca(): void { this.draftPcas.push({ Action: '', Responsibility: '', Target_Date: null, Status: 'R' }); }
  removePca(i: number): void { this.draftPcas.splice(i, 1); }

  submit(): void {
    if (!this.activeProblem) return;

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

    const p = this.activeProblem;
    this.saving = true;
    this.service
      .submitPlan({
        Part_ID: p.Part_ID,
        Defect_ID: p.Defect_ID,
        Attribution_ID: p.Attribution_ID,
        Severity_ID: p.Severity_ID,
        RootCauses: roots.map((r) => ({ Root_Cause: r.Root_Cause.trim() })),
        Icas: icas.map((i) => ({ Action: i.Action.trim(), Responsibility: i.Responsibility.trim(), ICA_Date: i.ICA_Date || null })),
        Pcas: pcas.map((p2) => ({ Action: p2.Action.trim(), Responsibility: p2.Responsibility.trim(), Target_Date: p2.Target_Date || null, Status: p2.Status || 'R' })),
        Inserted_Host: localStorage.getItem('Hostname') ?? '',
        Inserted_User_ID: Number(localStorage.getItem('Employee_ID')) || undefined,
        Plant_Code: localStorage.getItem('Plant_Code') ?? '',
      })
      .subscribe({
        next: () => {
          this.saving = false;
          this.toast.showSuccess('Saved', 'Action plan saved.');
          this.closeModal();
          this.search();
        },
        error: () => (this.saving = false),
      });
  }
}
