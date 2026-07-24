import { CommonModule } from '@angular/common';
import { Component, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { SearchPipe } from '../../../Shared/pipes/search.pipe';
import { ConfirmationService, ToastService } from '../../../services';
import { MasterService } from '../master.service';
import { MenuService } from '../../../auth';
import { Router } from '@angular/router';
import { LocationData } from '../../../Shared/models/location.model';

@Component({
  selector: 'app-location',
  standalone: true,
  imports: [CommonModule, FormsModule, SearchPipe],
  templateUrl: './location.component.html',
  styleUrl: './location.component.css'
})
export class LocationComponent {
 // Input fields for Location name and sort order
  Location_Name = '';
  SORTORDER?: number | null = null;

  // Search input for filtering table data
  dataSearch = '';

  // Raw Location data fetched from backend
  LocationList: LocationData[] = [];

  // Index of the row being edited
  Edit_Location_ID: number | null = null;

  // Rights
  isEdit: boolean = false;
  isDelete: boolean = false;
  isCreate: boolean = false;

  //*************************************************** Dependency Start *************************************//
  readonly toastService = inject(ToastService);
  readonly confirmationService = inject(ConfirmationService);
  readonly locationService = inject(MasterService);
  readonly menuService = inject(MenuService);
  readonly router = inject(Router);
  //*************************************************** Dependency End ****************************************//

  ngOnInit() {
    const Menu_ID = this.menuService.getMenuIDByActionName(
      window.location.href
    );
    if (Menu_ID) {
      this.isEdit = this.menuService.canEdit(Menu_ID);
      this.isDelete = this.menuService.canDelete(Menu_ID);
      this.isCreate = this.menuService.canCreate(Menu_ID);
      this.loadLocation();
    } else {
      this.toastService.showWarning(
        'You dont have access to this route',
        'Access Denide'
      );
    }
  }

  ngAfterViewChecked() {
    console.log(window.location.href);
  }
  //*************************************************** Location List Start ****************************************//

  // Fetch all Location from backend
  loadLocation() {
    this.locationService.getAllLocations().subscribe({
      next: (data: LocationData[]) => {
        this.LocationList = data;
      },
      error: (err) => {
        console.error('Error fetching Location:', err);
      },
    });
  }

  //*************************************************** Location List End ****************************************//

  //*************************************************** Save Start ****************************************//

  // Add a new Location entry
  onsave() {

    if (!this.Location_Name) {
      this.toastService.showWarning('Please select Location Name', 'Location Name is required');
      return;
    }
    if (!this.SORTORDER) {
      this.toastService.showWarning('Please select SORTORDER', 'SORTORDER is required');
      return;
    }

    // Check for duplicate
    const isDuplicate = this.LocationList.some(
      (location) =>
        location.Location_Name.toLowerCase().trim() === this.Location_Name.toLowerCase().trim()
    );

    if (isDuplicate) {
      this.toastService.showWarning(
        'Duplicate record found',
        `${this.Location_Name} already exists`
      );
      return;
    }

    if (
      this.Location_Name &&
      this.SORTORDER !== null &&
      this.Edit_Location_ID === null
    ) {
      const saveData: LocationData = {
        Location_Name: this.Location_Name,
        SORTORDER: this.SORTORDER,
        Inserted_Host: localStorage.getItem('Hostname') ?? '',
        Inserted_User_ID: Number(localStorage.getItem('Employee_ID')),
        Plant_Code: localStorage.getItem('Plant_Code') ?? '',
      };

      this.locationService.createLocation(saveData).subscribe({
        next: (res) => {
          if (res.isSuccessMessage) {
            this.toastService.showSuccess(res.messageTitle, res.messageDetail);
            this.resetForm();
            this.loadLocation();
          }
          if (res.isErrorMessage) {
            this.toastService.showError(res.messageTitle, res.messageDetail);
          }
        },
        error: (err) => {
          console.error('Error creating Location:', err);
        },
      });
    }
  }

  //*************************************************** Save End ****************************************//

  //*************************************************** Edit Start ****************************************//

  // Populate form fields for editing a row
  startEdit(location: LocationData) {
    const row = location;
    if (!row) {
      this.toastService.showError(
        'Unable to edit.',
        'The selected Location entry does not exist.'
      );
      return;
    }

    this.Location_Name = row.Location_Name;
    this.SORTORDER = row.SORTORDER;
    this.Edit_Location_ID = location.Location_ID ?? 0;

    window.scrollTo(0, 0);
  }

  // Save changes to an existing Location
  saveEdit() {
    // Check for duplicate
    const isDuplicate = this.LocationList.some(
      (location) =>
        location.Location_Name.toLowerCase().trim() === this.Location_Name.toLowerCase().trim() &&       location.Location_ID !== this.Edit_Location_ID
    );

    if (isDuplicate) {
      this.toastService.showWarning(
        'Duplicate record found',
        `${this.Location_Name} already exists`
      );
      return;
    }

    if (
      this.Location_Name &&
      this.SORTORDER !== null &&
      this.Edit_Location_ID !== null
    ) {
      const updatedData: LocationData = {
        Location_ID: this.Edit_Location_ID,
        Location_Name: this.Location_Name,
        SORTORDER: this.SORTORDER,
        Plant_Code: localStorage.getItem('Plant_Code') ?? '',
        Updated_Host: localStorage.getItem('Hostname') ?? '',
        Updated_User_ID: Number(localStorage.getItem('Employee_ID')),
        Inserted_User_ID: 0,
      };

      this.locationService
        .updateLocation(this.Edit_Location_ID, updatedData)
        .subscribe({
          next: (res) => {
            if (res.isSuccessMessage) {
              this.toastService.showSuccess(res.messageTitle, res.messageDetail);
              this.resetForm();
              this.loadLocation();
              this.Edit_Location_ID = null;
            }
            if (res.isErrorMessage) {
              this.toastService.showError(res.messageTitle, res.messageDetail);
            }
          },
          error: (err) => {
            console.error('Error updating Location:', err);
          },
        });
    }
  }

  //*************************************************** Edit End ****************************************//

  //*************************************************** Delete Start ****************************************//

  // Confirm and delete a Location
  onDelete(location: { Location_ID?: number; Location_Name: string }) {
    if (!location.Location_ID) {
      this.toastService.showWarning(
        'Location ID not found',
        'Location ID not found'
      );
      return;
    }

    this.confirmationService
      .confirmDelete(
        'Delete Location',
        'Are you sure you want to delete this Location? This action cannot be undone.',
        `Location: ${location.Location_Name} (${location.Location_ID})`,
        'Delete Location',
        'Cancel'
      )
      .then((confirmed) => {
        if (confirmed) {
          this.locationService.deleteLocation(location.Location_ID!).subscribe({
            next: (response) => {
              if (response.isSuccessMessage) {
                this.toastService.showSuccess(
                  response.messageTitle || 'Deleted',
                  response.messageDetail || 'Location deleted successfully.'
                );
                this.LocationList = this.LocationList.filter(
                  (s) => s.Location_ID !== location.Location_ID
                );
              }
              if (response.isErrorMessage) {
                this.toastService.showError(
                  response.messageTitle,
                  response.messageDetail
                );
              }
            },
            error: (error) => {
              this.toastService.showError(
                'Delete Failed',
                error?.error?.message ||
                'An error occurred while deleting the Location.'
              );
            },
          });
        }
      });
  }

  //*************************************************** Delete End ****************************************//

  // Clear form fields
  resetForm() {
    this.Location_Name = '';
    this.SORTORDER = null;
  }

  // Refresh data
  refreshData() {
    this.loadLocation();
    this.Location_Name = '';
    this.SORTORDER = null;
    this.Edit_Location_ID = null;
  }
}
