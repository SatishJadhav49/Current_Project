// Angular core imports
import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

// Custom services and pipes
import { MasterService } from '../master.service';
import { SearchPipe } from '../../../Shared/pipes/search.pipe';
import { ToastService } from '../../../services/toast.service';
import { ConfirmationService } from '../../../services/confirmation.service';
import { AttributionMaster } from '../../../Shared/models/attributionmaster.model';
import { MenuService } from '../../../auth';
import { ActivatedRoute, Router } from '@angular/router';

@Component({
  selector: 'app-attribution-master',
  standalone: true,
  imports: [CommonModule, FormsModule, SearchPipe],
  templateUrl: './attribution.component.html',
})
export class AttributionMasterComponent implements OnInit {
  // Input fields for attribution name and sort order
  Attribution_Name = '';
  SORTORDER?: number | null = null;

  // Search input for filtering table data
  dataSearch = '';

  // Raw attribution data fetched from backend
  attributionList: AttributionMaster[] = [];

  // Index of the row being edited
  Edit_Attribution_ID: number | null = null;

  // Rights
  isEdit: boolean = false;
  isDelete: boolean = false;
  isCreate: boolean = false;

  //*************************************************** Dependency Start *************************************//
  readonly toastService = inject(ToastService);
  readonly confirmationService = inject(ConfirmationService);
  readonly attributionService = inject(MasterService);
  readonly menuService = inject(MenuService);
  readonly router = inject(Router);
  //*************************************************** Dependency End ****************************************//

  ngOnInit() {
    const Menu_ID = this.menuService.getMenuIDByActionName(
      window.location.href,
    );
    if (Menu_ID) {
      this.isEdit = this.menuService.canEdit(Menu_ID);
      this.isDelete = this.menuService.canDelete(Menu_ID);
      this.isCreate = this.menuService.canCreate(Menu_ID);
      this.loadAttributions();
    } else {
      this.toastService.showWarning(
        'You dont have access to this route',
        'Access Denide',
      );
    }
  }

  ngAfterViewChecked() {
    console.log(window.location.href);
    // const Menu_ID = this.menuService.getMenuIDByActionName(window.location.href);
    // if (Menu_ID) {
    //   this.isEdit = this.menuService.canEdit(Menu_ID);
    //   this.isDelete = this.menuService.canDelete(Menu_ID);
    //   this.isCreate = this.menuService.canCreate(Menu_ID);
    // }
  }

  //*************************************************** Fetch Data Opration Start *************************************//

  // Fetch all attributions from backend
  loadAttributions() {
    this.attributionService.getAllAttributions().subscribe({
      next: (data: AttributionMaster[]) => {
        this.attributionList = data;
      },
      error: (err) => {
        console.error('Error fetching attributions:', err);
      },
    });
  }

  //*************************************************** Fetch Data Opration End *************************************//

  //*************************************************** Save Opration Start *************************************//

  // Add a new attribution entry
  onsave() {
    if (
      this.Attribution_Name &&
      this.SORTORDER !== null &&
      this.Edit_Attribution_ID === null
    ) {
      const saveData: AttributionMaster = {
        Attribution_Name: this.Attribution_Name,
        SORTORDER: this.SORTORDER,
        Inserted_Host: localStorage.getItem('Hostname') ?? '',
        Inserted_User_ID: Number(localStorage.getItem('Employee_ID')),
        Plant_Code: localStorage.getItem('Plant_Code') ?? '',
      };

      this.attributionService.createAttributions(saveData).subscribe({
        next: (res) => {
          this.toastService.showSuccess(res.messageTitle, res.messageDetail);
          this.resetForm();
          this.loadAttributions();
        },
        error: (err) => {
          console.error('Error creating attribution:', err);
        },
      });
    }
  }

  //*************************************************** Save Opration End *************************************//

  //*************************************************** Edit Opration Start *************************************//

  // Populate form fields for editing a row
  startEdit(attribution: AttributionMaster) {
    const row = attribution;
    if (!row) {
      this.toastService.showError(
        'Unable to edit.',
        'The selected Attribution entry does not exist.',
      );
      return;
    }
    // this.attributionList = this.attributionList.find((a) => a.Attribution_ID === row.Attribution_ID) || null;

    this.Attribution_Name = row.Attribution_Name;
    this.SORTORDER = row.SORTORDER;
    this.Edit_Attribution_ID = attribution.Attribution_ID ?? 0;

    window.scrollTo(0, 0);
  }

  // Save changes to an existing attribution
  saveEdit() {
    if (
      this.Attribution_Name &&
      this.SORTORDER !== null &&
      this.Edit_Attribution_ID !== null
    ) {
      const updatedData: AttributionMaster = {
        Attribution_ID: this.Edit_Attribution_ID,
        Attribution_Name: this.Attribution_Name,
        SORTORDER: this.SORTORDER,

        Plant_Code: localStorage.getItem('Plant_Code') ?? '',
        Updated_Host: localStorage.getItem('Hostname') ?? '',
        Updated_User_ID: Number(localStorage.getItem('Employee_ID')),
        Inserted_User_ID: 0,
      };

      this.attributionService
        .updateAttribution(this.Edit_Attribution_ID, updatedData)
        .subscribe({
          next: (res) => {
            this.toastService.showSuccess(res.messageTitle, res.messageDetail);
            this.resetForm();
            this.loadAttributions();
            this.Edit_Attribution_ID = null;
          },
          error: (err) => {
            console.error('Error updating attribution:', err);
          },
        });
    }
  }

  //*************************************************** Edit Opration End *************************************//

  //*************************************************** Delete Opration Start *************************************//

  // Confirm and delete a attribution
  onDelete(attribution:any) {
    debugger;
    if (!attribution.Attribution_ID) {
      this.toastService.showWarning(
        'Attribution ID not found',
        'Attribution ID not found',
      );
      return;
    }

    this.confirmationService
      .confirmDelete(
        'Delete Attribution',
        'Are you sure you want to delete this Attribution? This action cannot be undone.',
        `Attribution: ${attribution.Attribution_Name} (${attribution.Attribution_ID})`,
        'Delete Attribution',
        'Cancel',
      )
      .then((confirmed) => {
        if (confirmed) {
          this.attributionService
            .deleteAttribution(attribution.Attribution_ID!)
            .subscribe({
              next: (response) => {
                if (response.isSuccessMessage) {
                  this.toastService.showSuccess(
                    response.messageTitle || 'Deleted',
                    response.messageDetail || 'Location deleted successfully.',
                  );
                  this.attributionList = this.attributionList.filter(
                    (s) => s.Attribution_ID !== attribution.Attribution_ID,
                  );
                }
                if (response.isErrorMessage) {
                  this.toastService.showError(
                    response.messageTitle,
                    response.messageDetail,
                  );
                }
              },
              error: (error) => {
                this.toastService.showError(
                  'Delete Failed',
                  error?.error?.message ||
                    'An error occurred while deleting the attribution.',
                );
              },
            });
        }
      });
  }

  //*************************************************** Delete Opration End *************************************//

  // Clear form fields
  resetForm() {
    this.Attribution_Name = '';
    this.SORTORDER = null;
  }

  // Refresh data
  refreshData() {
    this.loadAttributions();
    this.Attribution_Name = '';
    this.SORTORDER = null;
    this.Edit_Attribution_ID = null;
  }
}
