import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiRequestService } from '../../services/api-request.service';
import { DocumentModel } from '../../Shared/models/document.model'

@Injectable({
  providedIn: 'root',
})
export class DocumentService {
  constructor(
    private apiRequest: ApiRequestService
  ) { }

  //***************************** Document master Start ************************************//

  getTableData(): Observable<DocumentModel[]> {
    return this.apiRequest.get('MM_Documents_Master/GetTableData');
  }

  uploadDocumentFile(data: FormData): Observable<any> {
    return this.apiRequest.postDocuments('MM_Documents_Master/uploadDocumentFile', data);
  }

  deleteFile(documentId: number): Observable<any> {
    return this.apiRequest.post(`MM_Documents_Master/DeleteFile`, documentId);
  }

  postDownloadDocument(documentPath: string) {
    return this.apiRequest.postForBlob('MM_Documents_Master/PostDownloadDocument',{ Document_Path: documentPath })
    // const headers = {
    //   Authorization: `Bearer ${localStorage.getItem('user') || ''}`,
    //   'Plant-Code': localStorage.getItem('Plant_Code') || '',
    //   'Employee-ID': localStorage.getItem('Employee_ID') || '',
    // };

    // return this.http.post(
    //   `${this.appConfig.baseApiPath}MM_Documents_Master/PostDownloadDocument`,
    //   { Document_Path: documentPath },
    //   {
    //     headers,
    //     responseType: 'blob',
    //   }
    // );

    //***************************** Document master end ************************************//

  }
}
