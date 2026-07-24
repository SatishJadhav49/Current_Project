import { CommonModule } from '@angular/common';
import { Component, EventEmitter, inject, OnInit, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { SearchPipe } from '../../../Shared/pipes/search.pipe';
import { ConfirmationService, ToastService } from '../../../services';
import { MasterService } from '../master.service';
import { MenuService } from '../../../auth';
import { Router } from '@angular/router';
import { DefectTypeData } from '../../../Shared/models/defecttype.model';


@Component({
  selector: 'app-defecttype',
  standalone: true,
  imports: [CommonModule, FormsModule, SearchPipe],
  templateUrl: './defecttype.component.html',
  styleUrl: './defecttype.component.css'
})
export class DefecttypeComponent {

  // Input fields for Defect Type name, sort order
  Defect_Type_Name = '';
  SORTORDER?: number | null = null;

  // Search input for filtering table data
  dataSearch = '';

  // Raw Defect Type data fetched from backend
  DefectTypeList: DefectTypeData[] = [];

  // Index of the row being edited
  Edit_Defect_Type_ID: number | null = null;

  //update excel
  showUploadPopup = false;

  // Rights
  isEdit: boolean = false;
  isDelete: boolean = false;
  isCreate: boolean = false;


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
      this.getdefectTypeList();
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

  //*************************************************** DefectTypeList  Start *************************************//
  // Fetch all Defect Types from backend
  getdefectTypeList() {
    this.masterService.getAllDefectTypes().subscribe({
      next: (data: DefectTypeData[]) => {
        this.DefectTypeList = data;
      },
      error: (err) => {
        console.error('Error fetching Defect Types:', err);
      },
    });
  }

  //*************************************************** DefectTypeList  end *************************************//

  //*************************************************** Save Defect Master Start *************************************//
  // Add a new Defect entry
  onSave() {

    if (!this.Defect_Type_Name) {
      this.toastService.showWarning('Please select Defect Name', 'Defect Type Name is required');
      return;
    }
    if (!this.SORTORDER) {
      this.toastService.showWarning('Please select SORTORDER', 'SORTORDER is required');
      return;
    }

    // Check for duplicate
    const isDuplicate = this.DefectTypeList.some(
      (defectType) =>
        defectType.Defect_Type_Name.toLowerCase().trim() ===
        this.Defect_Type_Name.toLowerCase().trim()
    );

    if (isDuplicate) {
      this.toastService.showWarning(
        'Duplicate record found',
        this.Defect_Type_Name + ' already exist'
      );
      return;
    }

    if (
      this.masterService &&
      this.SORTORDER !== null &&
      this.Edit_Defect_Type_ID === null
    ) {
      const saveData: DefectTypeData = {
        Defect_Type_Name: this.Defect_Type_Name,
        SORTORDER: this.SORTORDER,
        Inserted_Host: localStorage.getItem('Hostname') ?? '',
        Inserted_User_ID: Number(localStorage.getItem('Employee_ID')),
        Plant_Code: localStorage.getItem('Plant_Code') ?? '',
      };

      this.masterService.createDefectType(saveData).subscribe({
        next: (res) => {
          if (res.isSuccessMessage) {
            this.toastService.showSuccess(res.messageTitle, res.messageDetail);
            this.resetForm();
            this.getdefectTypeList();
          }
          if (res.isErrorMessage) {
            this.toastService.showError(res.messageTitle, res.messageDetail);
          }
        },
        error: (err) => {
          console.error('Error creating Defect Type:', err);
        },
      });
    }
  }
  //*************************************************** Save Defect Type Master End *************************************//

  //*************************************************** Update Defect Type Master Start *************************************//

  // Populate form fields for editing a row
  startEdit(vehicle: DefectTypeData) {
    const row = vehicle;
    if (!row) {
      this.toastService.showError(
        'Unable to edit.',
        'The selected Defect Type entry does not exist.'
      );
      return;
    }
    this.Defect_Type_Name = row.Defect_Type_Name;
    this.SORTORDER = row.SORTORDER;
    this.Edit_Defect_Type_ID = vehicle.Defect_Type_ID ?? 0;

    window.scrollTo(0, 0);
  }

  // Save changes to an existing Defect Type entry
  saveEdit() {
    // Check for duplicate
    const isDuplicate = this.DefectTypeList.some(
      (defectType) =>
        defectType.Defect_Type_Name.toLowerCase().trim() ==
        this.Defect_Type_Name.toLowerCase().trim() && defectType.SORTORDER == this.SORTORDER&& defectType.Defect_Type_ID !== this.Edit_Defect_Type_ID
    );

    if (isDuplicate) {
      this.toastService.showWarning(
        'Duplicate record found',
        this.Defect_Type_Name + ' already exist'
      );
      return;
    }

    if (
      this.Defect_Type_Name &&
      this.SORTORDER !== null &&
      this.Edit_Defect_Type_ID !== null
    ) {
      const updatedData: DefectTypeData = {
        Defect_Type_ID: this.Edit_Defect_Type_ID,
        Defect_Type_Name: this.Defect_Type_Name,
        SORTORDER: this.SORTORDER,
        Plant_Code: localStorage.getItem('Plant_Code') ?? '',
        Updated_Host: localStorage.getItem('Hostname') ?? '',
        Updated_User_ID: Number(localStorage.getItem('Employee_ID')),
        Inserted_User_ID: 0,
      };

      this.masterService.updateDefectType(this.Edit_Defect_Type_ID, updatedData).subscribe({
        next: (res) => {
          if (res.isSuccessMessage) {
            this.toastService.showSuccess(res.messageTitle, res.messageDetail);
            this.resetForm();
            this.getdefectTypeList();
            this.Edit_Defect_Type_ID = null;
          }
          if (res.isErrorMessage) {
            this.toastService.showError(res.messageTitle, res.messageDetail);
          }
        },
        error: (err) => {
          console.error('Error updating Defect Type:', err);
        },
      });
    }
  }
  //*************************************************** Update Defect Type Master end *************************************//

  //*************************************************** Delete Defect Type Master Start *************************************//

  // Confirm and delete a Defect Type entry
  onDelete(defectType: { Defect_Type_ID?: number; Defect_Type_Name: string }) {
    if (!defectType.Defect_Type_ID) {
      this.toastService.showWarning('Defect Type ID not found', 'Defect Type ID not found');
      return;
    }

    this.confirmationService
      .confirmDelete(
        'Delete Defect Type',
        'Are you sure you want to delete this Defect Type? This action cannot be undone.',
        `Defect Type: ${defectType.Defect_Type_Name} (${defectType.Defect_Type_ID})`,
        'Delete Defect Type',
        'Cancel'
      )
      .then((confirmed) => {
        if (confirmed) {
          this.masterService.deleteDefectType(defectType.Defect_Type_ID!).subscribe({
            next: (response) => {
              if (response.isSuccessMessage) {
                this.toastService.showSuccess(
                  response.messageTitle || 'Deleted',
                  response.messageDetail || 'Defect Type deleted successfully.'
                );
                this.DefectTypeList = this.DefectTypeList.filter(
                  (s) => s.Defect_Type_ID !== defectType.Defect_Type_ID
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
                'An error occurred while deleting the Defect Type.'
              );
            },
          });
        }
      });
  }

  //*************************************************** Delete Defect Type Master end *************************************//

  //*************************************************** ExcelUpload Popup Defect Type Master Start *************************************//

  openUploadPopup() {

      this.showUploadPopup = true;

  }

  closeUploadPopup() {
    this.showUploadPopup = false;
    this.refreshData();
  }



  // Clear form fields
  resetForm() {
    this.Defect_Type_Name = '';
    this.SORTORDER = null;
  }

  // Refresh data
  refreshData() {
    this.getdefectTypeList();
    this.Defect_Type_Name = '';
    this.SORTORDER = null;
    this.Edit_Defect_Type_ID = null;
  }
}
