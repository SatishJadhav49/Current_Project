import { CommonModule } from '@angular/common';
import { Component, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ToastService } from '../../services';
import { VehicleTrackingService } from '../VehicleTracking/vehicle-tracking.service';
import {
  AttributionItem,
  AuditorItem,
  PendingReworkItem,
  ReworkUpsertPayload,
  STAGE,
} from '../VehicleTracking/vehicle-tracking.models';

/** A pending-rework row — the standalone page is single-vehicle-agnostic, so
 *  the row only needs the server payload; edits happen in the modal. */
interface ReworkListRow extends PendingReworkItem {
  /** Row-level spinner flag while the modal save is in flight. */
  saving?: boolean;
}

/** Working copy edited inside the modal — kept separate from the row so
 *  cancelling doesn't mutate the underlying list. */
interface ReworkModalDraft {
  Rework_Done: boolean;
  Rework_By_Name: string;
  Rework_Remark: string;
}

@Component({
  selector: 'app-rework',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './rework.component.html',
})
export class ReworkComponent implements OnInit {
  private readonly vehicleService = inject(VehicleTrackingService);
  private readonly toast = inject(ToastService);

  /** Filter state — defaults to "All" + empty VIN, so the team lands on
   *  every pending defect across all vehicles. */
  selectedAttributionId = 0;
  vinFilter = '';

  attributions: AttributionItem[] = [];
  /** Auditor names registered for the Rework area — feed a datalist so the
   *  rework team can either pick a known name or type a free-form one. */
  reworkAuditors: AuditorItem[] = [];

  rows: ReworkListRow[] = [];
  loading = false;

  ngOnInit(): void {
    // Load filter masters first (independent of the list reload).
    this.vehicleService.getAttributions().subscribe({
      next: (list) => (this.attributions = list ?? []),
    });
    this.vehicleService.getAuditorsByArea(STAGE.REWORK).subscribe({
      next: (list) => (this.reworkAuditors = list ?? []),
    });

    this.reload();
  }

  // ─── Filter handlers ─────────────────────────────────────────────────────

  onAttributionChange() {
    this.reload();
  }

  onVinSearch() {
    this.reload();
  }

  clearFilters() {
    this.selectedAttributionId = 0;
    this.vinFilter = '';
    this.reload();
  }

  reload() {
    this.loading = true;
    this.vehicleService
      .getPendingReworkList(this.selectedAttributionId || undefined, this.vinFilter || undefined)
      .subscribe({
        next: (data: any) => {
          this.loading = false;
          const items: PendingReworkItem[] = data?.Concerns ?? [];
          this.rows = items.map((it) => ({ ...it, saving: false }));
        },
        error: () => (this.loading = false),
      });
  }

  // ─── Modal-driven save ────────────────────────────────────────────────

  /** Row currently open in the modal (null = closed). */
  activeRow: ReworkListRow | null = null;
  draft: ReworkModalDraft = { Rework_Done: false, Rework_By_Name: '', Rework_Remark: '' };
  savingModal = false;

  openReworkModal(row: ReworkListRow) {
    this.activeRow = row;
    this.draft = {
      Rework_Done: false,
      Rework_By_Name: '',
      Rework_Remark: '',
    };
  }

  closeModal() {
    this.activeRow = null;
    this.savingModal = false;
  }

  /** Save requires: Done ticked AND Rework By present AND remark not empty.
   *  The remark documents WHAT was actually done — auditable trail. */
  canSaveModal(): boolean {
    return (
      !this.savingModal &&
      this.draft.Rework_Done === true &&
      this.draft.Rework_By_Name.trim().length > 0 &&
      this.draft.Rework_Remark.trim().length > 0
    );
  }

  saveModal() {
    if (!this.activeRow) return;
    if (!this.canSaveModal()) {
      if (!this.draft.Rework_Done) {
        this.toast.showWarning('Mark Done', 'Tick the "Rework Done" checkbox.');
      } else if (this.draft.Rework_By_Name.trim().length === 0) {
        this.toast.showWarning('Rework By required', 'Enter or pick a name.');
      } else if (this.draft.Rework_Remark.trim().length === 0) {
        this.toast.showWarning('Remark required', 'Describe what rework was done.');
      }
      return;
    }

    const row = this.activeRow;
    const draft = this.draft;
    this.savingModal = true;

    const payload: ReworkUpsertPayload = {
      Defect_Entry_ID: row.Defect_Entry_ID,
      Rework_Done: draft.Rework_Done,
      Rework_By: draft.Rework_By_Name.trim(),
      Remark: draft.Rework_Remark.trim(),
      Inserted_Host: localStorage.getItem('Hostname') ?? '',
      Inserted_User_ID: Number(localStorage.getItem('Employee_ID')) || undefined,
      Plant_Code: localStorage.getItem('Plant_Code') ?? '',
    };

    this.vehicleService.upsertRework(payload).subscribe({
      next: () => {
        this.savingModal = false;
        this.toast.showSuccess(
          'Rework saved',
          `${this.last8(row.VIN_No)} — ${row.Part_Name} done by ${draft.Rework_By_Name.trim()}.`,
        );
        // Done rows are excluded from the pending list — drop it locally.
        this.rows = this.rows.filter((r) => r.Defect_Entry_ID !== row.Defect_Entry_ID);
        this.closeModal();
      },
      error: () => (this.savingModal = false),
    });
  }

  // ─── Display helpers ─────────────────────────────────────────────────────

  /** Show the last 8 chars of a VIN — unique per vehicle and easier on the eye
   *  than the full 17-char string for line operators. */
  last8(vin: string | null | undefined): string {
    if (!vin) return '';
    return vin.length <= 8 ? vin : vin.slice(-8);
  }

  /** Resolve the display name for the currently selected attribution
   *  (shown in the stage-style header). */
  attributionLabel(id: number): string {
    return (
      this.attributions.find((a) => a.Attribution_ID === id)?.Attribution_Name ??
      'All attributions'
    );
  }
}
