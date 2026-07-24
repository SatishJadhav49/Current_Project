import { CommonModule } from '@angular/common';
import { Component, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ToastService } from '../../../services/toast.service';
import { DocumentModel } from '../../../Shared/models/document.model';
import { ConfirmationService } from '../../../services/confirmation.service';
import { DocumentService } from '../document.service';
import { MenuService } from '../../../auth';


@Component({
  selector: 'app-documentsmanagement',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './documentsmanagement.component.html',
  styleUrls: ['./documentsmanagement.component.css']
})
export class DocumentsmanagementComponent implements OnInit {

  selectedFile: File | null = null;
  documentTitle: string = '';
  documents: DocumentModel[] = [];

   DocumentInfo: DocumentModel ={
     Document_ID: 0,
     Inserted_User_ID: 0,
   }

    // Rights
  isEdit: boolean = false;
  isDelete: boolean = false;
  isCreate: boolean = false;

  //*************************************************** Dependency Start *************************************//
  // Injecting services using Angular's inject() function
  readonly toastService = inject(ToastService);
  readonly confirmationService = inject(ConfirmationService);
  readonly DocumentService = inject(DocumentService)
  readonly menuService = inject(MenuService)
  //*************************************************** Dependency End ****************************************//

  ngOnInit(): void {
    const Menu_ID = this.menuService.getMenuIDByActionName(window.location.href);
    if (Menu_ID) {
      this.isEdit = this.menuService.canEdit(Menu_ID);
      this.isDelete = this.menuService.canDelete(Menu_ID);
      this.isCreate = this.menuService.canCreate(Menu_ID);
    this.loadDocuments();
    } else {
      this.toastService.showWarning("You dont have access to this route", "Access Denide");
    }
  }

  onFileChange(event: any) {
    const file: File = event.target.files[0];
    if (file) {
      this.selectedFile = file;
    }
  }

  uploadDocument() {
    if (!this.selectedFile) {
      this.toastService.showWarning('', 'Please select a file to upload');
      return;
    }
    if (!this.documentTitle) {
      this.toastService.showWarning('', 'Please enter a document title');
      return;
    }

    // Check for duplicate
      const isDuplicate = this.documents.some(document =>
        document.Document_Title?.toLowerCase().trim() === this.documentTitle.toLowerCase().trim()
      );

      if (isDuplicate) {
        this.toastService.showWarning('Duplicate record found', this.documentTitle + ' already exist');
        return;
      }

    const formData = new FormData();
    formData.append('file', this.selectedFile);
    formData.append('Document_Title', this.documentTitle);
    formData.append('Inserted_User_ID', localStorage.getItem('Employee_ID') || '');
    formData.append('Plant_Code', localStorage.getItem('Plant_Code') || '');
    formData.append('Inserted_Host', localStorage.getItem('Hostname') || '');

    this.DocumentService.uploadDocumentFile(formData).subscribe({
      next: (res) => {
        if (res.isErrorMessage || res.isExceptionMessage) {
          this.toastService.showError(res.messageDetail, res.messageTitle);
        } else if (res.isSuccessMessage) {
          this.toastService.showSuccess(res.messageDetail, res.IsTitle);
          this.selectedFile = null;
          this.documentTitle = '';
          this.loadDocuments();
        } else if (res.isAlertMessage) {
          this.toastService.showWarning(res.messageDetail, res.IsTitle);
        }
      },
      error: () => {
        this.toastService.showError('', 'File upload failed');
      }
    });
  }


  loadDocuments() {
    this.DocumentService.getTableData().subscribe({
      next: (data: DocumentModel[]) => {
        this.documents = data;
      },
      error: () => {
        this.toastService.showError('', 'Failed to load documents');
      }
    });
  }


    downloadDocument(path: string) {
    if (path) {
      this.DocumentService.postDownloadDocument(path).subscribe(
        (blob: Blob) => {
          // Create a blob URL and download the file
          const url = window.URL.createObjectURL(blob);
          const link = document.createElement('a');
          link.href = url;

          // Use the original filename if available, otherwise create a default name
          const documentPath =
            path || `document_${this.DocumentInfo.Document_ID}`;
          const firstUnderscoreIndex = documentPath.indexOf('_');
          const fileName =
            firstUnderscoreIndex !== -1
              ? documentPath.substring(firstUnderscoreIndex + 1)
              : documentPath;
          link.download = fileName;

          // Append to body, click, and remove
          document.body.appendChild(link);
          link.click();
          document.body.removeChild(link);

          // Clean up the blob URL
          window.URL.revokeObjectURL(url);

          this.toastService.showSuccess(
            'Success',
            `Document "${fileName}" downloaded successfully`
          );
        },
        (error) => {
          console.error('Download document error:', error);
          this.toastService.showError(
            'Error',
            `Failed to download document: ${error.message || 'Unknown error'}`
          );
        }
      );
    }
  }



  deleteDocument(Document_ID?: number, documentTitle?: string) {
    if (!Document_ID) {
      this.toastService.showWarning(
        'Document ID not found: ' + Document_ID,
        'Missing Document ID'
      );
      return;
    }

    this.confirmationService
      .confirmDelete(
        'Delete Document',
        'Are you sure you want to delete this document? This action cannot be undone.',
        `Document: ${documentTitle} (${Document_ID})`,
        'Delete Document',
        'Cancel'
      )
      .then((confirmed) => {
        if (confirmed) {
          this.DocumentService.deleteFile(Document_ID!).subscribe({
            next: (response) => {
              this.toastService.showSuccess(
                response.messageTitle || 'Deleted',
                response.messageDetail || 'Document deleted successfully.'
              );

              // Remove from table
              this.documents = this.documents.filter(
                d => d.Document_ID !== Document_ID
              );

              // Optional: reset form or selection
              this.selectedFile = null;
              this.documentTitle = '';
            },
            error: (error) => {
              this.toastService.showError(
                'Delete Failed',
                error?.error?.message || 'An error occurred while deleting the document.'
              );
            }
          });
        }
      });
  }

  resetform() {
    this.selectedFile = null;
    this.documentTitle = '';
  }

  refreshData() {
    this.selectedFile = null;
    this.documentTitle = '';
  }
}




