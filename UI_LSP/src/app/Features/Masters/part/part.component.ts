import { CommonModule } from '@angular/common';
import { Component, EventEmitter, inject, OnInit, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { SearchPipe } from '../../../Shared/pipes/search.pipe';
import { ConfirmationService, ToastService } from '../../../services';
import { MasterService } from '../master.service';
import { MenuService } from '../../../auth';
import { Router } from '@angular/router';
// import ExcelJS from 'exceljs';
// import * as fs from 'file-saver';
import { PartData } from '../../../Shared/models/part.model';
import { UploadexcelpartComponent } from './uploadexcelpart/uploadexcelpart.component';

@Component({
  selector: 'app-part',
  standalone: true,
  imports: [CommonModule, FormsModule, SearchPipe, UploadexcelpartComponent],
  templateUrl: './part.component.html',
  styleUrl: './part.component.css',
})
export class PartComponent {
  // Input fields for Part name, sort order
  Part_Name = '';
  SORTORDER?: number | null = null;

  // Search input for filtering table data
  dataSearch = '';

  // Raw Part data fetched from backend
  PartList: PartData[] = [];

  // Index of the row being edited
  Edit_Part_ID: number | null = null;

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
  readonly PartService = inject(MasterService);
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
      this.getpartList();
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

  //*************************************************** PartList  Start *************************************//
  // Fetch all Part from backend
  getpartList() {
    this.PartService.getAllParts().subscribe({
      next: (data: PartData[]) => {
        this.PartList = data;
      },
      error: (err) => {
        console.error('Error fetching Part:', err);
      },
    });
  }

  //*************************************************** PartList  end *************************************//

  //*************************************************** Save Part Master Start *************************************//
  // Add a new Part entry
  onSave() {
    if (!this.Part_Name) {
      this.toastService.showWarning(
        'Please select Part Name',
        'Part Name is required',
      );
      return;
    }
    if (!this.SORTORDER) {
      this.toastService.showWarning(
        'Please select SORTORDER',
        'SORTORDER is required',
      );
      return;
    }

    // Check for duplicate
    const isDuplicate = this.PartList.some(
      (part) =>
        part.Part_Name.toLowerCase().trim() ===
        this.Part_Name.toLowerCase().trim(),
    );

    if (isDuplicate) {
      this.toastService.showWarning(
        'Duplicate record found',
        this.Part_Name + ' already exist',
      );
      return;
    }

    if (
      this.PartService &&
      this.SORTORDER !== null &&
      this.Edit_Part_ID === null
    ) {
      const saveData: PartData = {
        Part_Name: this.Part_Name,
        SORTORDER: this.SORTORDER,
        Inserted_Host: localStorage.getItem('Hostname') ?? '',
        Inserted_User_ID: Number(localStorage.getItem('Employee_ID')),
        Plant_Code: localStorage.getItem('Plant_Code') ?? '',
      };

      this.PartService.createPart(saveData).subscribe({
        next: (res) => {
          if (res.isSuccessMessage) {
            this.toastService.showSuccess(res.messageTitle, res.messageDetail);
            this.resetForm();
            this.getpartList();
          }
          if (res.isErrorMessage) {
            this.toastService.showError(res.messageTitle, res.messageDetail);
          }
        },
        error: (err) => {
          console.error('Error creating Part:', err);
        },
      });
    }
  }
  //*************************************************** Save Part Master End *************************************//

  //*************************************************** Update Part Master Start *************************************//

  // Populate form fields for editing a row
  startEdit(vehicle: PartData) {
    const row = vehicle;
    if (!row) {
      this.toastService.showError(
        'Unable to edit.',
        'The selected Part entry does not exist.',
      );
      return;
    }
    this.Part_Name = row.Part_Name;
    this.SORTORDER = row.SORTORDER;
    this.Edit_Part_ID = vehicle.Part_ID ?? 0;

    window.scrollTo(0, 0);
  }

  // Save changes to an existing Part
  saveEdit() {
    // Check for duplicate
    const isDuplicate = this.PartList.some(
      (part) =>
        part.Part_Name.toLowerCase().trim() ==
          this.Part_Name.toLowerCase().trim() &&
        part.SORTORDER == this.SORTORDER,
    );

    if (isDuplicate) {
      this.toastService.showWarning(
        'Duplicate record found',
        this.Part_Name + ' already exist',
      );
      return;
    }

    if (
      this.Part_Name &&
      this.SORTORDER !== null &&
      this.Edit_Part_ID !== null
    ) {
      const updatedData: PartData = {
        Part_ID: this.Edit_Part_ID,
        Part_Name: this.Part_Name,
        SORTORDER: this.SORTORDER,
        Plant_Code: localStorage.getItem('Plant_Code') ?? '',
        Updated_Host: localStorage.getItem('Hostname') ?? '',
        Updated_User_ID: Number(localStorage.getItem('Employee_ID')),
        Inserted_User_ID: 0,
      };

      this.PartService.updatePart(this.Edit_Part_ID, updatedData).subscribe({
        next: (res) => {
          if (res.isSuccessMessage) {
            this.toastService.showSuccess(res.messageTitle, res.messageDetail);
            this.resetForm();
            this.getpartList();
            this.Edit_Part_ID = null;
          }
          if (res.isErrorMessage) {
            this.toastService.showError(res.messageTitle, res.messageDetail);
          }
        },
        error: (err) => {
          console.error('Error updating Part:', err);
        },
      });
    }
  }
  //*************************************************** Update Part Master end *************************************//

  //*************************************************** Delete Part Master Start *************************************//

  // Confirm and delete a Part
  onDelete(part: { Part_ID?: number; Part_Name: string }) {
    if (!part.Part_ID) {
      this.toastService.showWarning('Part ID not found', 'Part ID not found');
      return;
    }

    this.confirmationService
      .confirmDelete(
        'Delete Part',
        'Are you sure you want to delete this Part? This action cannot be undone.',
        `Part: ${part.Part_Name} (${part.Part_ID})`,
        'Delete Part',
        'Cancel',
      )
      .then((confirmed) => {
        if (confirmed) {
          this.PartService.deletePart(part.Part_ID!).subscribe({
            next: (response) => {
              if (response.isSuccessMessage) {
                this.toastService.showSuccess(
                  response.messageTitle || 'Deleted',
                  response.messageDetail || 'Part deleted successfully.',
                );
                this.PartList = this.PartList.filter(
                  (s) => s.Part_ID !== part.Part_ID,
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
                  'An error occurred while deleting the Part.',
              );
            },
          });
        }
      });
  }

  //*************************************************** Delete Part Master end *************************************//

  //*************************************************** ExcelUpload Popup Part Master Start *************************************//

  openUploadPopup() {
    this.showUploadPopup = true;
  }

  closeUploadPopup() {
    this.showUploadPopup = false;
    this.refreshData();
  }

  //*************************************************** ExcelUpload Popup Part Master end *************************************//
  downloadExcel() {
    this.generateExcelFile(this.PartList);
  }

  private generateExcelFile(data: any[]): void {
    // const workbook = new ExcelJS.Workbook();
    // const worksheet = workbook.addWorksheet('Part Master Data');

    // // Define column headers
    // const headers = ['Part Name', 'SORTORDER'];

    // // Add header row
    // const headerRow = worksheet.addRow(headers);

    // // Style the header row
    // headerRow.eachCell((cell, colNumber) => {
    //   cell.font = { bold: true, color: { argb: 'FFFFFF' } };
    //   cell.fill = {
    //     type: 'pattern',
    //     pattern: 'solid',
    //     fgColor: { argb: '366092' },
    //   };
    //   cell.border = {
    //     top: { style: 'thin' },
    //     left: { style: 'thin' },
    //     bottom: { style: 'thin' },
    //     right: { style: 'thin' },
    //   };
    //   cell.alignment = { vertical: 'middle', horizontal: 'center' };
    // });

    // // Add data rows
    // data.forEach((item, index) => {
    //   const row = worksheet.addRow([
    //     item.Part_Name || '',
    //     item.SORTORDER || '',
    //   ]);

    //   // Style data rows
    //   row.eachCell((cell, colNumber) => {
    //     cell.border = {
    //       top: { style: 'thin' },
    //       left: { style: 'thin' },
    //       bottom: { style: 'thin' },
    //       right: { style: 'thin' },
    //     };
    //     // Alternate row colors
    //     if (index % 2 === 0) {
    //       cell.fill = {
    //         type: 'pattern',
    //         pattern: 'solid',
    //         fgColor: { argb: 'F8F9FA' },
    //       };
    //     }
    //   });
    // });

    // // Auto-fit columns
    // worksheet.columns.forEach((column, index) => {
    //   let maxLength = 0;

    //   // Use column number (1-based) instead of column letter
    //   worksheet
    //     .getColumn(index + 1)
    //     .eachCell({ includeEmpty: true }, (cell) => {
    //       const columnLength = cell.value ? cell.value.toString().length : 10;
    //       if (columnLength > maxLength) {
    //         maxLength = columnLength;
    //       }
    //     });

    //   column.width = Math.min(Math.max(maxLength + 2, 12), 50);
    // });

    // // Generate filename with current date
    // const currentDate = new Date().toISOString().split('T')[0];
    // const filename = `BLT_Part_Master_Data_Export_${currentDate}.xlsx`;

    // // Save file
    // workbook.xlsx
    //   .writeBuffer()
    //   .then((buffer) => {
    //     const blob = new Blob([buffer], {
    //       type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
    //     });
    //     fs.saveAs(blob, filename);

    //     this.toastService.showSuccess(
    //       'Export Successful',
    //       `Excel file '${filename}' has been downloaded successfully.`,
    //     );
    //   })
    //   .catch((error) => {
    //     this.toastService.showError(
    //       'Export Failed',
    //       'Failed to generate Excel file. Please try again.',
    //     );
    //     console.error('Excel generation error:', error);
    //   });
  }

  // Clear form fields
  resetForm() {
    this.Part_Name = '';
    this.SORTORDER = null;
  }

  // Refresh data
  refreshData() {
    this.getpartList();
    this.Part_Name = '';
    this.SORTORDER = null;
    this.Edit_Part_ID = null;
  }
}
