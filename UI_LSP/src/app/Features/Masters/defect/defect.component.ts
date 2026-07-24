import { CommonModule } from '@angular/common';
import { Component, EventEmitter, inject, OnInit, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { SearchPipe } from '../../../Shared/pipes/search.pipe';
import { ConfirmationService, ToastService } from '../../../services';
import { MasterService } from '../master.service';
import { MenuService } from '../../../auth';
import { Router } from '@angular/router';
import { DefectData } from '../../../Shared/models/defect.model';

@Component({
  selector: 'app-defect',
  standalone: true,
  imports: [CommonModule, FormsModule, SearchPipe],
  templateUrl: './defect.component.html',
  styleUrl: './defect.component.css'
})
export class DefectComponent {

  // Input fields for Defect name, sort order
  Defect_Name = '';
  SORTORDER?: number | null = null;

  // Search input for filtering table data
  dataSearch = '';

  // Raw Defect data fetched from backend
  DefectList: DefectData[] = [];

  // Index of the row being edited
  Edit_Defect_ID: number | null = null;

  //update excel
  showUploadPopup = false;

  // Rights
  isEdit: boolean = false;
  isDelete: boolean = false;
  isCreate: boolean = false;

  @Output() close = new EventEmitter<void>();

  //*************************************************** Dependency Start *************************************//
  readonly toastService = inject(ToastService);
  readonly confirmationService = inject(ConfirmationService);
  readonly masterService = inject(MasterService);
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
      this.getdefectList();
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

  //*************************************************** DefectList  Start *************************************//
  // Fetch all Defect from backend
  getdefectList() {
    this.masterService.getAllDefects().subscribe({
      next: (data: DefectData[]) => {
        this.DefectList = data;
      },
      error: (err) => {
        console.error('Error fetching Defect:', err);
      },
    });
  }

  //*************************************************** DefectList  end *************************************//

  //*************************************************** Save Defect Master Start *************************************//
  // Add a new Defect entry
  onSave() {

    if (!this.Defect_Name) {
      this.toastService.showWarning('Please select Defect Name', 'Defect Name is required');
      return;
    }
    if (!this.SORTORDER) {
      this.toastService.showWarning('Please select SORTORDER', 'SORTORDER is required');
      return;
    }

    // Check for duplicate
    const isDuplicate = this.DefectList.some(
      (defect) =>
        defect.Defect_Name.toLowerCase().trim() ===
        this.Defect_Name.toLowerCase().trim()
    );

    if (isDuplicate) {
      this.toastService.showWarning(
        'Duplicate record found',
        this.Defect_Name + ' already exist'
      );
      return;
    }

    if (
      this.masterService &&
      this.SORTORDER !== null &&
      this.Edit_Defect_ID === null
    ) {
      const saveData: DefectData = {
        Defect_Name: this.Defect_Name,
        SORTORDER: this.SORTORDER,
        Inserted_Host: localStorage.getItem('Hostname') ?? '',
        Inserted_User_ID: Number(localStorage.getItem('Employee_ID')),
        Plant_Code: localStorage.getItem('Plant_Code') ?? '',
      };

      this.masterService.createDefect(saveData).subscribe({
        next: (res) => {
          if (res.isSuccessMessage) {
            this.toastService.showSuccess(res.messageTitle, res.messageDetail);
            this.resetForm();
            this.getdefectList();
          }
          if (res.isErrorMessage) {
            this.toastService.showError(res.messageTitle, res.messageDetail);
          }
        },
        error: (err) => {
          console.error('Error creating Defect:', err);
        },
      });
    }
  }
  //*************************************************** Save Defect Master End *************************************//

  //*************************************************** Update Defect Master Start *************************************//

  // Populate form fields for editing a row
  startEdit(vehicle: DefectData) {
    const row = vehicle;
    if (!row) {
      this.toastService.showError(
        'Unable to edit.',
        'The selected Defect entry does not exist.'
      );
      return;
    }
    this.Defect_Name = row.Defect_Name;
    this.SORTORDER = row.SORTORDER;
    this.Edit_Defect_ID = vehicle.Defect_ID ?? 0;

    window.scrollTo(0, 0);
  }

  // Save changes to an existing Defect entry
  saveEdit() {
    // Check for duplicate
    const isDuplicate = this.DefectList.some(
      (defect) =>
        defect.Defect_Name.toLowerCase().trim() ==
        this.Defect_Name.toLowerCase().trim() && defect.SORTORDER == this.SORTORDER
    );

    if (isDuplicate) {
      this.toastService.showWarning(
        'Duplicate record found',
        this.Defect_Name + ' already exist'
      );
      return;
    }

    if (
      this.Defect_Name &&
      this.SORTORDER !== null &&
      this.Edit_Defect_ID !== null
    ) {
      const updatedData: DefectData = {
        Defect_ID: this.Edit_Defect_ID,
        Defect_Name: this.Defect_Name,
        SORTORDER: this.SORTORDER,
        Plant_Code: localStorage.getItem('Plant_Code') ?? '',
        Updated_Host: localStorage.getItem('Hostname') ?? '',
        Updated_User_ID: Number(localStorage.getItem('Employee_ID')),
        Inserted_User_ID: 0,
      };

      this.masterService.updateDefect(this.Edit_Defect_ID, updatedData).subscribe({
        next: (res) => {
          if (res.isSuccessMessage) {
            this.toastService.showSuccess(res.messageTitle, res.messageDetail);
            this.resetForm();
            this.getdefectList();
            this.Edit_Defect_ID = null;
          }
          if (res.isErrorMessage) {
            this.toastService.showError(res.messageTitle, res.messageDetail);
          }
        },
        error: (err) => {
          console.error('Error updating Defect:', err);
        },
      });
    }
  }
  //*************************************************** Update Defect Master end *************************************//

  //*************************************************** Delete Defect Master Start *************************************//

  // Confirm and delete a Defect entry
  onDelete(defect: { Defect_ID?: number; Defect_Name: string }) {
    if (!defect.Defect_ID) {
      this.toastService.showWarning('Defect ID not found', 'Defect ID not found');
      return;
    }

    this.confirmationService
      .confirmDelete(
        'Delete Defect',
        'Are you sure you want to delete this Defect? This action cannot be undone.',
        `Defect: ${defect.Defect_Name} (${defect.Defect_ID})`,
        'Delete Defect',
        'Cancel'
      )
      .then((confirmed) => {
        if (confirmed) {
          this.masterService.deleteDefect(defect.Defect_ID!).subscribe({
            next: (response) => {
              if (response.isSuccessMessage) {
                this.toastService.showSuccess(
                  response.messageTitle || 'Deleted',
                  response.messageDetail || 'Defect deleted successfully.'
                );
                this.DefectList = this.DefectList.filter(
                  (s) => s.Defect_ID !== defect.Defect_ID
                );
              }
              if (response.isErrorMessage) {
                this.toastService.showError(response.messageTitle, response.messageDetail);
              }
            },
            error: (error) => {
              this.toastService.showError(
                'Delete Failed',
                error?.error?.message ||
                'An error occurred while deleting the Defect.'
              );
            },
          });
        }
      });
  }

  //*************************************************** Delete Defect Master end *************************************//

  //*************************************************** ExcelUpload Popup Defect Master Start *************************************//

  openUploadPopup() {

      this.showUploadPopup = true;

  }

  closeUploadPopup() {
    this.showUploadPopup = false;
    this.refreshData();
  }



  // Clear form fields
  resetForm() {
    this.Defect_Name = '';
    this.SORTORDER = null;
  }

  // Refresh data
  refreshData() {
    this.getdefectList();
    this.Defect_Name = '';
    this.SORTORDER = null;
    this.Edit_Defect_ID = null;
  }
}
