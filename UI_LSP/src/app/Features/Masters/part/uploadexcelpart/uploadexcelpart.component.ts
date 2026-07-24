import { HttpClient } from '@angular/common/http';
import { Component, EventEmitter, inject, Input, Output } from '@angular/core';
import { AppConfig, ToastService } from '../../../../services';
import ExcelJS from 'exceljs';
import * as fs from 'file-saver';

@Component({
  selector: 'app-uploadexcelpart',
  standalone: true,
  imports: [],
  templateUrl: './uploadexcelpart.component.html',
  styleUrl: './uploadexcelpart.component.css'
})
export class UploadexcelpartComponent {

  selectedFile: File | null = null;
  fileError = '';
  showPopup!: false;
  $fileError: any;

  @Output() close = new EventEmitter<boolean>();
  @Input() selectedModel: number | undefined = undefined;

  constructor(private http: HttpClient, private appConfig: AppConfig) { }
  readonly toastService = inject(ToastService);

  onFileChange(event: Event) {
    const input = event.target as HTMLInputElement;

    if (!input.files || input.files.length === 0) {
      this.fileError = 'No file selected.';
      return;
    }

    const file = input.files[0];

    if (file.size > 10 * 1024 * 1024) {
      this.fileError = 'File size should not exceed 10MB.';
      this.selectedFile = null;
    } else {
      this.fileError = '';
      this.selectedFile = file;
    }
  }

  onUpload() {

    if (!this.selectedFile) {
      this.fileError = 'Please select a valid file before uploading.';
      this.toastService.showError("File is required", "Please select excel to upload ");
      return;
    }

    const formData = new FormData();

    formData.append('file', this.selectedFile!);
    formData.append('otherinfo', JSON.stringify({
      Model_ID: this.selectedModel,
      Inserted_Host: localStorage.getItem('Hostname'),
      Inserted_User_ID: Number(localStorage.getItem('Employee_ID')),
      Inserted_Date: new Date(),
      Plant_Code: localStorage.getItem('Plant_Code'),
    }));

    this.http.post(this.appConfig.baseApiPath + 'MM_Part_Master/UploadData', formData).subscribe({
      next: (res: any) => {
        this.toastService.showSuccess('Upload Successful', 'File uploaded successfully.');
        this.close.emit(true);
      },
      error: (err: any) => {
        console.error('Upload failed:', err);
        this.toastService.showError('Upload Failed', 'Could not upload file.');
      },
    });
  }

  onCancel() {
    this.close.emit(false);
  }

  downloadTemplate() {
    const workbook = new ExcelJS.Workbook();
    const worksheet = workbook.addWorksheet('Part Template');

    // Header
    worksheet.addRow(['Part Name', 'Sort Order']);

    // Sample data rows
    worksheet.addRow(['Part_1', 1]);
    worksheet.addRow(['Part_2', 2]);

    // Auto width for better UX
    worksheet.columns.forEach(column => {
      column.width = 20;
    });

    // Save file
    workbook.xlsx.writeBuffer().then((buffer) => {
      const blob = new Blob([buffer], {
        type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
      });
      fs.saveAs(blob, 'Part-template.xlsx');
    });
  }
}


