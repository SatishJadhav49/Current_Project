import { CommonModule } from '@angular/common';
import { Component, EventEmitter, inject, OnInit, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { SearchPipe } from '../../../Shared/pipes/search.pipe';
import { ConfirmationService, ToastService } from '../../../services';
import { MasterService } from '../master.service';
import { MenuService } from '../../../auth';
import { Router } from '@angular/router';
import { Audit_Area, AuditorList } from '../../../Shared/models/common.model';

@Component({
  selector: 'app-auditor',
  standalone: true,
  imports: [CommonModule, FormsModule, SearchPipe],
  templateUrl: './auditor.component.html',
  styleUrl: './auditor.component.css'
})
export class AuditorComponent {
// Audit_Area
  selectedAuditArea!: Audit_Area;
  AuditAreaList: Audit_Area[] = [];

  // Input fields for Auditor name, sort order
  Auditor_Name = '';
  Audit_Area_ID: number = 0;

  // Search input for filtering table data
  dataSearch = '';

  // Raw Auditor data fetched from backend
  AuditorList: AuditorList[] = [];

  // Index of the row being edited
  Edit_Auditor_ID: number | null = null;



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
      this.getAuditorList();
      this.getAuditAreaList();
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

  //*************************************************** AuditorList & AuditAreaList Start *************************************//
  // Fetch all Auditors from backend
  getAuditorList() {
    this.masterService.getAllAuditors().subscribe({
      next: (data: AuditorList[]) => {
        this.AuditorList = data;
      },
      error: (err) => {
        console.error('Error fetching Auditor:', err);
      },
    });
  }

  getAuditAreaList() {
    // Fetch Audit Area list first, then load Lines
    this.masterService.getAuditAreaList().subscribe({
      next: (auditAreaData: Audit_Area[]) => {
        this.AuditAreaList = auditAreaData;
      },
      error: (err: any) => {
        console.error('Error fetching Audit Areas:', err);
      },
    });
  }
  //*************************************************** AuditorList & AuditAreaList end *************************************//

  //*************************************************** Save Auditor Master Start *************************************//
  // Add a new Auditor entry
  onSave() {

    if (!this.Auditor_Name) {
      this.toastService.showWarning('Please select Auditor Name', 'Auditor Name is required');
      return;
    }
    if (!this.Audit_Area_ID) {
      this.toastService.showWarning('Please select Audit Area', 'Audit Area is required');
      return;
    }

    // Check for duplicate
    const isDuplicate = this.AuditorList.some(
      (auditor) =>
        auditor.Auditor_Name.toLowerCase().trim() ===
        this.Auditor_Name.toLowerCase().trim() && auditor.Audit_Area_ID === this.Audit_Area_ID
    );

    if (isDuplicate) {
      this.toastService.showWarning(
        'Duplicate record found',
        this.Auditor_Name + ' already exist'
      );
      return;
    }

    if (
      this.masterService &&
      this.Edit_Auditor_ID === null
    ) {
      const saveData: AuditorList = {
        Auditor_Name: this.Auditor_Name,
        Audit_Area_ID: this.Audit_Area_ID,
        Inserted_Host: localStorage.getItem('Hostname') ?? '',
        Inserted_User_ID: Number(localStorage.getItem('Employee_ID')),
        Plant_Code: localStorage.getItem('Plant_Code') ?? '',
        Auditor_ID: 0
      };

      this.masterService.createAuditor(saveData).subscribe({
        next: (res) => {
          if (res.isSuccessMessage) {
            this.toastService.showSuccess(res.messageTitle, res.messageDetail);
            this.resetForm();
            this.getAuditorList();
          }
          if (res.isErrorMessage) {
            this.toastService.showError(res.messageTitle, res.messageDetail);
          }
        },
        error: (err) => {
          console.error('Error creating Auditor:', err);
        },
      });
    }
  }
  //*************************************************** Save Auditor Master End *************************************//

  //*************************************************** Update Auditor Master Start *************************************//

  // Populate form fields for editing a row
  startEdit(vehicle: AuditorList) {
    const row = vehicle;
    if (!row) {
      this.toastService.showError(
        'Unable to edit.',
        'The selected Auditor entry does not exist.'
      );
      return;
    }
    this.Auditor_Name = row.Auditor_Name;
    this.Audit_Area_ID = row.Audit_Area_ID;
    this.Edit_Auditor_ID = vehicle.Auditor_ID ?? 0;

    window.scrollTo(0, 0);
  }

  // Save changes to an existing Auditor
  saveEdit() {
    // Check for duplicate
    const isDuplicate = this.AuditorList.some(
      (auditor) =>
        auditor.Auditor_Name.toLowerCase().trim() ===
        this.Auditor_Name.toLowerCase().trim() && auditor.Audit_Area_ID === this.Audit_Area_ID
    );

    if (isDuplicate) {
      this.toastService.showWarning(
        'Duplicate record found',
        this.Auditor_Name + ' already exist'
      );
      return;
    }

    if (
      this.Auditor_Name &&
      this.Edit_Auditor_ID !== null
    ) {
      const updatedData: AuditorList = {
        Auditor_ID: this.Edit_Auditor_ID,
        Auditor_Name: this.Auditor_Name,
        Audit_Area_ID: this.Audit_Area_ID,
        Plant_Code: localStorage.getItem('Plant_Code') ?? '',
        Updated_Host: localStorage.getItem('Hostname') ?? '',
        Updated_User_ID: Number(localStorage.getItem('Employee_ID')),
        Inserted_User_ID: 0,
      };

      this.masterService.updateAuditor(this.Edit_Auditor_ID, updatedData).subscribe({
        next: (res) => {
          if (res.isSuccessMessage) {
            this.toastService.showSuccess(res.messageTitle, res.messageDetail);
            this.resetForm();
            this.getAuditorList();
            this.Edit_Auditor_ID = null;
          }
          if (res.isErrorMessage) {
            this.toastService.showError(res.messageTitle, res.messageDetail);
          }
        },
        error: (err) => {
          console.error('Error updating Auditor:', err);
        },
      });
    }
  }
  //*************************************************** Update Auditor Master end *************************************//

  //*************************************************** Delete Auditor Master Start *************************************//

  // Confirm and delete a Auditor
  onDelete(auditor: { Auditor_ID?: number; Auditor_Name: string }) {
    if (!auditor.Auditor_ID) {
      this.toastService.showWarning('Auditor ID not found', 'Auditor ID not found');
      return;
    }

    this.confirmationService
      .confirmDelete(
        'Delete Auditor',
        'Are you sure you want to delete this Auditor? This action cannot be undone.',
        `Auditor: ${auditor.Auditor_Name} (${auditor.Auditor_ID})`,
        'Delete Auditor',
        'Cancel'
      )
      .then((confirmed) => {
        if (confirmed) {
          this.masterService.deleteAuditor(auditor.Auditor_ID!).subscribe({
            next: (response) => {
              if (response.isSuccessMessage) {
                this.toastService.showSuccess(
                  response.messageTitle || 'Deleted',
                  response.messageDetail || 'Auditor deleted successfully.'
                );
                this.AuditorList = this.AuditorList.filter(
                  (s) => s.Auditor_ID !== auditor.Auditor_ID
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
                'An error occurred while deleting the Auditor.'
              );
            },
          });
        }
      });
  }

  //*************************************************** Delete Auditor Master end *************************************//

  // Clear form fields
  resetForm() {
    this.Auditor_Name = '';
    this.Audit_Area_ID = 0;
  }

  // Refresh data
  refreshData() {
    this.getAuditorList();
    this.Auditor_Name = '';
    this.Audit_Area_ID = 0;
    this.Edit_Auditor_ID = null;
  }
}
