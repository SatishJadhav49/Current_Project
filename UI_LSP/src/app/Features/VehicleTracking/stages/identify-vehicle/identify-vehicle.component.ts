import { CommonModule } from '@angular/common';
import {
  Component,
  EventEmitter,
  inject,
  Input,
  OnChanges,
  Output,
  SimpleChanges,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ToastService } from '../../../../services';
import { VehicleTrackingService } from '../../vehicle-tracking.service';
import {
  BuildPhase,
  CountryItem,
  DriveType,
  FuelType,
  ModelItem,
  VehicleDetails,
} from '../../vehicle-tracking.models';

type MatchState = 'matched' | 'pick' | 'edited';

@Component({
  selector: 'app-identify-vehicle',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './identify-vehicle.component.html',
})
export class IdentifyVehicleComponent implements OnChanges {
  /** Vehicle data is owned by the shell; we just edit a working copy. */
  @Input() vehicle: VehicleDetails | null = null;

  /** When the vehicle has been RFD'd, everything is read-only. */
  @Input() locked = false;

  @Output() vehicleSaved = new EventEmitter<VehicleDetails>();

  readonly vehicleService = inject(VehicleTrackingService);
  readonly toast = inject(ToastService);

  Vehicle_Data: VehicleDetails = new VehicleDetails();
  saving = false;

  // Master lists
  buildPhases: BuildPhase[] = [];
  drives: DriveType[] = [];
  fuels: FuelType[] = [];
  countries: CountryItem[] = [];
  models: ModelItem[] = [];

  matchState: Record<string, MatchState> = {
    Model_ID: 'pick',
    Build_Phase_ID: 'pick',
    Drive_ID: 'pick',
    Fuel_ID: 'pick',
    Country_ID: 'pick',
  };

  plantCode = localStorage.getItem('Plant_Code') ?? '';

  constructor() {
    this.loadMasters();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['vehicle'] && this.vehicle) {
      this.Vehicle_Data = { ...new VehicleDetails(), ...this.vehicle };
      this.computeMatchStates();
    }
  }

  private loadMasters() {
    this.vehicleService.getBuildPhases().subscribe((d) => (this.buildPhases = d ?? []));
    this.vehicleService.getDriveTypes().subscribe((d) => (this.drives = d ?? []));
    this.vehicleService.getFuelTypes().subscribe((d) => (this.fuels = d ?? []));
    this.vehicleService.getCountries().subscribe((d) => (this.countries = d ?? []));
    this.vehicleService.getModels().subscribe((d) => (this.models = d ?? []));
  }

  onDropdownChange(field: keyof VehicleDetails) {
    if (this.Vehicle_Data[field] && Number(this.Vehicle_Data[field]) > 0) {
      this.matchState[field as string] = 'edited';
    } else {
      this.matchState[field as string] = 'pick';
    }
  }

  private computeMatchStates() {
    const v = this.Vehicle_Data;
    this.matchState['Model_ID'] = v.Model_ID > 0 ? 'matched' : 'pick';
    this.matchState['Build_Phase_ID'] = v.Build_Phase_ID > 0 ? 'matched' : 'pick';
    this.matchState['Drive_ID'] = v.Drive_ID > 0 ? 'matched' : 'pick';
    this.matchState['Fuel_ID'] = v.Fuel_ID > 0 ? 'matched' : 'pick';
    this.matchState['Country_ID'] = v.Country_ID > 0 ? 'matched' : 'pick';
  }

  isValid(): boolean {
    const v = this.Vehicle_Data;
    return (
      !!v.VIN_No &&
      v.Model_ID > 0 &&
      v.Build_Phase_ID > 0 &&
      v.Drive_ID > 0 &&
      v.Fuel_ID > 0 &&
      v.Country_ID > 0
    );
  }

  save() {
    if (this.locked) return;
    if (!this.isValid()) {
      this.toast.showWarning('Incomplete', 'Please fill all required fields.');
      return;
    }
    const isUpdate = (this.Vehicle_Data.Vehicle_ID ?? 0) > 0;
    this.saving = true;
    this.vehicleService.SaveVehicle(this.Vehicle_Data).subscribe({
      next: (res: any) => {
        this.saving = false;
        const saved: VehicleDetails = res?.datalist ?? res;
        this.toast.showSuccess(
          isUpdate ? 'Updated' : 'Saved',
          isUpdate
            ? 'Vehicle details updated.'
            : 'Vehicle details saved. Next stage activated.',
        );
        this.vehicleSaved.emit(saved);
      },
      error: () => (this.saving = false),
    });
  }

  chip(field: string): { label: string; cls: string } | null {
    const s = this.matchState[field];
    if (!s) return null;
    if (s === 'matched') return { label: '✓ matched', cls: 'bg-green-100 text-green-700' };
    if (s === 'edited') return { label: '✏ edited', cls: 'bg-blue-100 text-blue-700' };
    return { label: '⚠ pick', cls: 'bg-yellow-100 text-yellow-800' };
  }
}
