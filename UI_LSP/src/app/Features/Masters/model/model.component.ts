import { CommonModule } from '@angular/common';
import { Component, EventEmitter, inject, OnInit, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { SearchPipe } from '../../../Shared/pipes/search.pipe';
import { ConfirmationService, ToastService } from '../../../services';
import { MasterService } from '../master.service';
import { MenuService } from '../../../auth';
import { Router } from '@angular/router';
import { Model } from '../../../Shared/models/common.model';

@Component({
  selector: 'app-model',
  standalone: true,
  imports: [CommonModule, FormsModule, SearchPipe],
  templateUrl: './model.component.html',
  styleUrl: './model.component.css',
})
export class ModelComponent {
  // Search input for filtering table data
  dataSearch = '';

  // Raw Defect Type data fetched from backend
  ModelList: Model[] = [];

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
      window.location.href,
    );
    if (Menu_ID) {
      this.isEdit = this.menuService.canEdit(Menu_ID);
      this.isDelete = this.menuService.canDelete(Menu_ID);
      this.isCreate = this.menuService.canCreate(Menu_ID);
      this.getModelList();
    } else {
      this.toastService.showWarning(
        'You dont have access to this route',
        'Access Denide',
      );
    }
  }

  ngAfterViewChecked() {
    console.log(window.location.href);
  }

  //*************************************************** ModelList  Start *************************************//
  // Fetch all Models from backend
  getModelList() {
    this.masterService.GetAllModels().subscribe({
      next: (data: Model[]) => {
        this.ModelList = data;
      },
      error: (err) => {
        console.error('Error fetching Models:', err);
      },
    });
  }

  //*************************************************** ModelList  end *************************************//

  //*************************************************** Update Model Master Start *************************************//

  // Save changes to an existing Defect Type entry
  saveEdit(data: Model) {
    if (data.Model_Name && data.Model_ID !== null) {
      data.Is_LSP_Active = !data.Is_LSP_Active;
      data.Updated_User_ID = Number(localStorage.getItem('Employee_ID') ||0);
      data.Updated_Host = localStorage.getItem('Hostname') ?? '';
      this.masterService.updateModel(data).subscribe({
        next: (res) => {
          if (res.isSuccessMessage) {
            this.toastService.showSuccess(res.messageTitle, res.messageDetail);
          }
          if (res.isErrorMessage) {
            this.toastService.showError(res.messageTitle, res.messageDetail);
          }
        },
        error: (err) => {
          console.error('Error updating Model:', err);
        },
      });
    }
  }
  //*************************************************** Update Model Master end *************************************//
}
