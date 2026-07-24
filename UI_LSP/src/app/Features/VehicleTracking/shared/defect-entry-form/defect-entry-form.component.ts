import { CommonModule } from '@angular/common';
import {
  Component,
  EventEmitter,
  inject,
  Input,
  OnInit,
  Output,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ConfirmationService, ToastService } from '../../../../services';
import { VehicleTrackingService } from '../../vehicle-tracking.service';
import {
  AttributionItem,
  ConcernRow,
  DefectItem,
  DefectTypeItem,
  LocationItem,
  PartItem,
  SeverityItem,
  VehicleAreaItem,
} from '../../vehicle-tracking.models';

interface Selection {
  Vehicle_Area_ID: number;
  Vehicle_Area_Name: string;
  Part_ID: number;
  Part_Name: string;
  Defect_ID: number;
  Defect_Name: string;
  Location_ID: number;
  Location_Name: string;
  Severity_ID: number;
  Severity_Code: string;
  Attribution_ID: number;
  Attribution_Name: string;
  Defect_Type_ID: number;
  Defect_Type_Name: string;
}

const EMPTY_SELECTION: Selection = {
  Vehicle_Area_ID: 0,
  Vehicle_Area_Name: '',
  Part_ID: 0,
  Part_Name: '',
  Defect_ID: 0,
  Defect_Name: '',
  Location_ID: 0,
  Location_Name: '',
  Severity_ID: 0,
  Severity_Code: '',
  Attribution_ID: 0,
  Attribution_Name: '',
  Defect_Type_ID: 0,
  Defect_Type_Name: '',
};

@Component({
  selector: 'app-defect-entry-form',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './defect-entry-form.component.html',
})
export class DefectEntryFormComponent implements OnInit {
  /** Concerns already saved for this stage (owned by parent). */
  @Input() concerns: ConcernRow[] = [];

  /** Disables Add/Update button while parent is saving. */
  @Input() saving = false;

  /** When false, the picker (column grid + remark + Add button) is hidden
   *  unless an edit is in progress. Used by Recheck which only edits/deletes. */
  @Input() pickerEnabled = true;

  /** When true, the concerns table shows a "Stage" column (first col after #).
   *  Used by Recheck which lists concerns across stages. */
  @Input() showStageColumn = false;

  /** Vehicle-wide lock — set by parent when RFD has been issued. When true
   *  the picker is fully hidden (even mid-edit) and edit + delete buttons
   *  are disabled on every row. */
  @Input() locked = false;

  /** When non-null, the Part column is restricted to these Part_IDs (image
   *  hotspot hit on the Paint Verification screen). Null = show all parts. */
  @Input() partFilterIds: number[] | null = null;

  /** User tapped "+ Add Concern" — parent should save to server then update [concerns]. */
  @Output() addRequest = new EventEmitter<ConcernRow>();

  /** User tapped "✓ Update" while editing — parent should PATCH the row by Defect_Entry_ID. */
  @Output() updateRequest = new EventEmitter<{ defectEntryId: number; row: ConcernRow }>();

  /** User tapped 🗑 on a row — parent should delete on server then update [concerns]. */
  @Output() removeRequest = new EventEmitter<{ index: number; row: ConcernRow }>();

  /** Defect_Entry_ID currently being edited, or null when in "add" mode. */
  editingDefectEntryId: number | null = null;

  readonly vehicleService = inject(VehicleTrackingService);
  readonly toast = inject(ToastService);
  readonly confirmationService = inject(ConfirmationService);

  // Master lists
  vehicleAreas: VehicleAreaItem[] = [];
  parts: PartItem[] = [];
  defects: DefectItem[] = [];
  locations: LocationItem[] = [];
  severities: SeverityItem[] = [];
  attributions: AttributionItem[] = [];
  defectTypes: DefectTypeItem[] = [];

  // Per-column search text
  search = {
    area: '',
    part: '',
    defect: '',
    location: '',
    severity: '',
    attribution: '',
    defectType: '',
  };

  selection: Selection = { ...EMPTY_SELECTION };
  remark = '';

  ngOnInit(): void {
    this.loadMasters();
  }

  private loadMasters() {
    this.vehicleService.getVehicleAreas().subscribe((d) => (this.vehicleAreas = d ?? []));
    this.vehicleService.getParts().subscribe((d) => (this.parts = d ?? []));
    this.vehicleService.getDefects().subscribe((d) => (this.defects = d ?? []));
    this.vehicleService.getLocations().subscribe((d) => (this.locations = d ?? []));
    this.vehicleService.getSeverities().subscribe((d) => (this.severities = d ?? []));
    this.vehicleService.getAttributions().subscribe((d) => (this.attributions = d ?? []));
    this.vehicleService.getDefectTypes().subscribe((d) => (this.defectTypes = d ?? []));
  }

  // ─── Filtering helpers ──────────────────────────────────────────────────

  private match(name: string, q: string): boolean {
    if (!q) return true;
    return (name || '').toLowerCase().includes(q.toLowerCase());
  }

  filteredAreas() { return this.vehicleAreas.filter((a) => this.match(a.Vehicle_Area_Name, this.search.area)); }
  filteredParts() {
    let list = this.parts;
    if (this.partFilterIds && this.partFilterIds.length > 0) {
      const ids = this.partFilterIds;
      list = list.filter((p) => ids.includes(p.Part_ID));
    }
    return list.filter((p) => this.match(p.Part_Name, this.search.part));
  }
  filteredDefects() { return this.defects.filter((d) => this.match(d.Defect_Name, this.search.defect)); }
  filteredLocations() { return this.locations.filter((l) => this.match(l.Location_Name, this.search.location)); }
  filteredSeverities() {
    return this.severities.filter(
      (s) => this.match(s.Severity_Name, this.search.severity) || this.match(s.Severity_Code, this.search.severity),
    );
  }
  filteredAttributions() { return this.attributions.filter((a) => this.match(a.Attribution_Name, this.search.attribution)); }
  filteredDefectTypes() { return this.defectTypes.filter((d) => this.match(d.Defect_Type_Name, this.search.defectType)); }

  // ─── Selection toggles ──────────────────────────────────────────────────

  pickArea(a: VehicleAreaItem) {
    const same = this.selection.Vehicle_Area_ID === a.Vehicle_Area_ID;
    this.selection.Vehicle_Area_ID = same ? 0 : a.Vehicle_Area_ID;
    this.selection.Vehicle_Area_Name = same ? '' : a.Vehicle_Area_Name;
  }
  pickPart(p: PartItem) {
    const same = this.selection.Part_ID === p.Part_ID;
    this.selection.Part_ID = same ? 0 : p.Part_ID;
    this.selection.Part_Name = same ? '' : p.Part_Name;
  }
  pickDefect(d: DefectItem) {
    const same = this.selection.Defect_ID === d.Defect_ID;
    this.selection.Defect_ID = same ? 0 : d.Defect_ID;
    this.selection.Defect_Name = same ? '' : d.Defect_Name;
  }
  pickLocation(l: LocationItem) {
    const same = this.selection.Location_ID === l.Location_ID;
    this.selection.Location_ID = same ? 0 : l.Location_ID;
    this.selection.Location_Name = same ? '' : l.Location_Name;
  }
  pickSeverity(s: SeverityItem) {
    const same = this.selection.Severity_ID === s.Severity_ID;
    this.selection.Severity_ID = same ? 0 : s.Severity_ID;
    this.selection.Severity_Code = same ? '' : s.Severity_Code;
  }
  pickAttribution(a: AttributionItem) {
    const same = this.selection.Attribution_ID === a.Attribution_ID;
    this.selection.Attribution_ID = same ? 0 : a.Attribution_ID;
    this.selection.Attribution_Name = same ? '' : a.Attribution_Name;
  }
  pickDefectType(d: DefectTypeItem) {
    const same = this.selection.Defect_Type_ID === d.Defect_Type_ID;
    this.selection.Defect_Type_ID = same ? 0 : d.Defect_Type_ID;
    this.selection.Defect_Type_Name = same ? '' : d.Defect_Type_Name;
  }

  /** Programmatically preselect Part and/or Vehicle Area — used by the Paint
   *  Verification screen when the auditor taps a mapped hotspot on the image.
   *  Only overwrites the given fields; the rest of the selection is kept. */
  presetSelection(opts: {
    Part_ID?: number;
    Part_Name?: string;
    Vehicle_Area_ID?: number;
    Vehicle_Area_Name?: string;
  }) {
    if (opts.Part_ID !== undefined) {
      this.selection.Part_ID = opts.Part_ID;
      this.selection.Part_Name = opts.Part_Name ?? '';
    }
    if (opts.Vehicle_Area_ID !== undefined) {
      this.selection.Vehicle_Area_ID = opts.Vehicle_Area_ID;
      this.selection.Vehicle_Area_Name = opts.Vehicle_Area_Name ?? '';
    }
  }

  // ─── Actions ────────────────────────────────────────────────────────────

  isValid(): boolean {
    const s = this.selection;
    return (
      s.Vehicle_Area_ID > 0 &&
      s.Part_ID > 0 &&
      s.Defect_ID > 0 &&
      s.Location_ID > 0 &&
      s.Severity_ID > 0 &&
      s.Attribution_ID > 0 &&
      s.Defect_Type_ID > 0
    );
  }

  /** Emit either addRequest (new concern) or updateRequest (editing) — parent
   *  saves to server, then calls resetAfterAdd() on success. We do NOT clear
   *  selection here, so if the parent rejects the save (e.g. auditor not chosen)
   *  the user keeps their picks. */
  emitSubmit() {
    if (!this.isValid() || this.saving) return;

    const row: ConcernRow = {
      Defect_Entry_ID: this.editingDefectEntryId ?? undefined,
      Vehicle_Area_ID: this.selection.Vehicle_Area_ID || undefined,
      Vehicle_Area_Name: this.selection.Vehicle_Area_Name || undefined,
      Part_ID: this.selection.Part_ID,
      Part_Name: this.selection.Part_Name,
      Defect_ID: this.selection.Defect_ID,
      Defect_Name: this.selection.Defect_Name,
      Location_ID: this.selection.Location_ID,
      Location_Name: this.selection.Location_Name,
      Severity_ID: this.selection.Severity_ID,
      Severity_Code: this.selection.Severity_Code,
      Attribution_ID: this.selection.Attribution_ID,
      Attribution_Name: this.selection.Attribution_Name,
      Defect_Type_ID: this.selection.Defect_Type_ID,
      Defect_Type_Name: this.selection.Defect_Type_Name,
      Remark: this.remark?.trim() || undefined,
    };

    if (this.editingDefectEntryId != null) {
      this.updateRequest.emit({ defectEntryId: this.editingDefectEntryId, row });
    } else {
      this.addRequest.emit(row);
    }
  }

  /** Load a saved row into the picker for editing. */
  startEdit(index: number) {
    if (this.locked) return;
    const row = this.concerns[index];
    if (!row || !row.Defect_Entry_ID) return;
    if (row.Rework_Done) {
      this.toast.showWarning(
        'Locked',
        'This concern has been reworked and can no longer be edited.',
      );
      return;
    }

    this.editingDefectEntryId = row.Defect_Entry_ID;
    this.selection = {
      Vehicle_Area_ID: row.Vehicle_Area_ID ?? 0,
      Vehicle_Area_Name: row.Vehicle_Area_Name ?? '',
      Part_ID: row.Part_ID,
      Part_Name: row.Part_Name,
      Defect_ID: row.Defect_ID,
      Defect_Name: row.Defect_Name,
      Location_ID: row.Location_ID,
      Location_Name: row.Location_Name,
      Severity_ID: row.Severity_ID,
      Severity_Code: row.Severity_Code,
      Attribution_ID: row.Attribution_ID,
      Attribution_Name: row.Attribution_Name,
      Defect_Type_ID: row.Defect_Type_ID,
      Defect_Type_Name: row.Defect_Type_Name,
    };
    this.remark = row.Remark ?? '';
    this.search = { area: '', part: '', defect: '', location: '', severity: '', attribution: '', defectType: '' };
  }

  /** Abandon edit, return to add mode with a blank picker. */
  cancelEdit() {
    this.editingDefectEntryId = null;
    this.clearSelection();
  }

  /** Called by the parent after a successful add. Full clear — all columns reset. */
  resetAfterAdd() {
    this.clearSelection();
  }

  /** Called by the parent after a successful update — exits edit mode, clears picker. */
  resetAfterUpdate() {
    this.editingDefectEntryId = null;
    this.clearSelection();
  }

  clearSelection() {
    this.selection = { ...EMPTY_SELECTION };
    this.remark = '';
    this.search = { area: '', part: '', defect: '', location: '', severity: '', attribution: '', defectType: '' };
  }

  emitRemove(index: number) {
    if (this.locked) return;
    const row = this.concerns[index];
    if (!row) return;
    if (row.Rework_Done) {
      this.toast.showWarning(
        'Locked',
        'This concern has been reworked and can no longer be deleted.',
      );
      return;
    }

    const label = `${row.Part_Name} · ${row.Defect_Name}` + (row.Location_Name ? ` · ${row.Location_Name}` : '');

    this.confirmationService
      .confirmDelete(
        'Delete Concern',
        'Are you sure you want to delete this concern? This action cannot be undone.',
        label,
        'Delete',
        'Cancel',
      )
      .then((confirmed) => {
        if (!confirmed) return;
        // If we were editing this row, drop edit state so the picker is consistent.
        if (this.editingDefectEntryId && row.Defect_Entry_ID === this.editingDefectEntryId) {
          this.cancelEdit();
        }
        this.removeRequest.emit({ index, row });
      });
  }

  isPicked(currentId: number, candidateId: number): boolean {
    return currentId === candidateId;
  }
}
