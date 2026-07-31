import { CommonModule } from '@angular/common';
import { Component, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ToastService } from '../../../services';
import { MasterService } from '../master.service';
import { MenuService } from '../../../auth';

interface StageSummary {
  Audit_Area_ID: number;
  Audit_Area_Name: string;
  Mapped_Count: number;
}
interface DefectRow {
  Defect_ID: number;
  Defect_Name: string;
}

@Component({
  selector: 'app-defectstagemapping',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './defectstagemapping.component.html',
})
export class DefectStageMappingComponent implements OnInit {
  private readonly masterService = inject(MasterService);
  private readonly toast = inject(ToastService);
  private readonly menuService = inject(MenuService);

  stages: StageSummary[] = [];
  allDefects: DefectRow[] = [];
  /** Checked defect ids for the selected stage. */
  checked = new Set<number>();

  selectedStageId = 0;
  search = '';
  loadingDefects = false;
  saving = false;

  isCreate = false;
  isEdit = false;

  ngOnInit(): void {
    const menuId = this.menuService.getMenuIDByActionName(window.location.href);
    if (menuId) {
      this.isCreate = this.menuService.canCreate(menuId);
      this.isEdit = this.menuService.canEdit(menuId);
    }
    this.loadStages();
    this.masterService.getAllDefects().subscribe({
      next: (list: any) => (this.allDefects = list ?? []),
      error: () => (this.allDefects = []),
    });
  }

  private loadStages(): void {
    this.masterService.getDefectStageSummaries().subscribe({
      next: (list) => (this.stages = (list as StageSummary[]) ?? []),
      error: () => (this.stages = []),
    });
  }

  get canSave(): boolean {
    return this.selectedStageId > 0 && (this.isCreate || this.isEdit);
  }

  get selectedStageName(): string {
    return this.stages.find((s) => s.Audit_Area_ID === this.selectedStageId)?.Audit_Area_Name ?? '';
  }

  onStageChange(): void {
    this.checked.clear();
    this.search = '';
    if (this.selectedStageId <= 0) return;
    this.loadingDefects = true;
    this.masterService.getDefectStageIds(this.selectedStageId).subscribe({
      next: (ids) => {
        this.checked = new Set((ids ?? []).map((id) => Number(id)));
        this.loadingDefects = false;
      },
      error: () => (this.loadingDefects = false),
    });
  }

  filteredDefects(): DefectRow[] {
    const q = this.search.trim().toLowerCase();
    if (!q) return this.allDefects;
    return this.allDefects.filter((d) => (d.Defect_Name || '').toLowerCase().includes(q));
  }

  isChecked(id: number): boolean {
    return this.checked.has(id);
  }

  toggle(id: number): void {
    if (this.checked.has(id)) this.checked.delete(id);
    else this.checked.add(id);
  }

  selectAllVisible(): void {
    this.filteredDefects().forEach((d) => this.checked.add(d.Defect_ID));
  }

  clearAll(): void {
    this.checked.clear();
  }

  save(): void {
    if (this.selectedStageId <= 0) {
      this.toast.showError('Stage required', 'Please select a stage.');
      return;
    }
    this.saving = true;
    const payload = {
      Audit_Area_ID: this.selectedStageId,
      Defect_IDs: Array.from(this.checked),
      Inserted_Host: localStorage.getItem('Hostname') ?? '',
      Inserted_User_ID: Number(localStorage.getItem('Employee_ID')) || undefined,
      Plant_Code: localStorage.getItem('Plant_Code') ?? '',
    };
    this.masterService.saveDefectStageMapping(payload).subscribe({
      next: () => {
        this.saving = false;
        this.toast.showSuccess('Saved', `Defect mapping updated for ${this.selectedStageName}.`);
        this.loadStages();
      },
      error: () => (this.saving = false),
    });
  }
}
