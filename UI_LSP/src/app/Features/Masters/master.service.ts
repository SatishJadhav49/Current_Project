import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiRequestService } from '../../services/api-request.service';
import { AttributionMaster } from '../../Shared/models/attributionmaster.model';
import { PartData } from '../../Shared/models/part.model';
import { DefectData } from '../../Shared/models/defect.model';
import { LocationData } from '../../Shared/models/location.model';
import { AuditorList, Model } from '../../Shared/models/common.model';
import { DefectTypeData } from '../../Shared/models/defecttype.model';

@Injectable({
  providedIn: 'root',
})
export class MasterService {
  constructor(private apiRequest: ApiRequestService) {}

  getAuditAreaList() {
    return this.apiRequest.get('MM_Common/GetAuditAreaList');
  }

  //***************************** Attribution Services start ***********************************//
  getAllAttributions(): Observable<AttributionMaster[]> {
    return this.apiRequest.get('MM_Attribution_Master/GetAllAttributions');
  }

  createAttributions(data: AttributionMaster): Observable<any> {
    return this.apiRequest.post(
      'MM_Attribution_Master/CreateAttribution',
      data,
    );
  }

  updateAttribution(
    attributionId: number,
    data: AttributionMaster,
  ): Observable<any> {
    return this.apiRequest.put(
      'MM_Attribution_Master/UpdateAttribution',
      data.Attribution_ID ?? 0,
      data,
    );
  }

  deleteAttribution(Attribution_ID: number): Observable<any> {
    debugger;
    return this.apiRequest.post(
      `MM_Attribution_Master/DeleteAttribution`,
      Attribution_ID,
    );
  }

  //***************************** Attribution Services end ***********************************//

  //***************************** Part Master Services Start ***********************************//

  getAllParts(): Observable<[PartData]> {
    return this.apiRequest.get('MM_Part_Master/GetAllParts');
  }

  createPart(data: PartData): Observable<any> {
    return this.apiRequest.post('MM_Part_Master/CreatePart', data);
  }

  updatePart(partId: number, data: PartData): Observable<any> {
    return this.apiRequest.post(
      'MM_Part_Master/UpdatePart/' + data.Part_ID,
      data,
    );
  }

  deletePart(Part_Id: number): Observable<any> {
    return this.apiRequest.post(`MM_Part_Master/DeletePart`, Part_Id);
  }

  //***************************** Part Master Services end ***********************************//

  //***************************** Defect Master Services Start ***********************************//

  getAllDefects(): Observable<[DefectData]> {
    return this.apiRequest.get('MM_Defect_Master/GetAllDefects');
  }

  createDefect(data: DefectData): Observable<any> {
    return this.apiRequest.post('MM_Defect_Master/CreateDefect', data);
  }

  updateDefect(defectId: number, data: DefectData): Observable<any> {
    return this.apiRequest.post(
      'MM_Defect_Master/UpdateDefect/' + data.Defect_ID,
      data,
    );
  }

  deleteDefect(Defect_Id: number): Observable<any> {
    return this.apiRequest.post(`MM_Defect_Master/DeleteDefect`, Defect_Id);
  }

  //***************************** Defect Master Services end ***********************************//

  //***************************** Location Services start ***********************************//

  getAllLocations(): Observable<LocationData[]> {
    return this.apiRequest.get('MM_Location_Master/GetAllLocations');
  }

  createLocation(data: LocationData): Observable<any> {
    return this.apiRequest.post('MM_Location_Master/CreateLocation', data);
  }

  updateLocation(locationId: number, data: LocationData): Observable<any> {
    return this.apiRequest.post(
      'MM_Location_Master/UpdateLocation/' + data.Location_ID,
      data,
    );
  }

  deleteLocation(locationId: number): Observable<any> {
    return this.apiRequest.post(
      `MM_Location_Master/DeleteLocation`,
      locationId,
    );
  }

  //***************************** Location Services end ***********************************//
  //***************************** Auditor Master Services Start ***********************************//

  getAllAuditors(): Observable<[AuditorList]> {
    return this.apiRequest.get('MM_Auditor_Master/GetAllAuditors');
  }

  createAuditor(data: AuditorList): Observable<any> {
    return this.apiRequest.post('MM_Auditor_Master/CreateAuditor', data);
  }

  updateAuditor(auditorId: number, data: AuditorList): Observable<any> {
    return this.apiRequest.post(
      'MM_Auditor_Master/UpdateAuditor/' + data.Auditor_ID,
      data,
    );
  }

  deleteAuditor(Auditor_Id: number): Observable<any> {
    return this.apiRequest.post(`MM_Auditor_Master/DeleteAuditor`, Auditor_Id);
  }

  //***************************** Auditor Master Services end ***********************************//

  //***************************** Defect Type Master Services Start ***********************************//

  getAllDefectTypes(): Observable<[DefectTypeData]> {
    return this.apiRequest.get('MM_Defect_Type_Master/GetAllDefectTypes');
  }

  createDefectType(data: DefectTypeData): Observable<any> {
    return this.apiRequest.post('MM_Defect_Type_Master/CreateDefectType', data);
  }

  updateDefectType(
    defectTypeId: number,
    data: DefectTypeData,
  ): Observable<any> {
    return this.apiRequest.post(
      'MM_Defect_Type_Master/UpdateDefectType/' + data.Defect_Type_ID,
      data,
    );
  }

  deleteDefectType(Defect_Type_Id: number): Observable<any> {
    return this.apiRequest.post(
      `MM_Defect_Type_Master/DeleteDefectType`,
      Defect_Type_Id,
    );
  }

  //***************************** Defect Type Master Services end ***********************************//

  //***************************** ModelMaster Services end ***********************************//

  GetAllModels(): Observable<[Model]> {
    return this.apiRequest.get('MM_Model_Master/GetModelList');
  }

  updateModel(data:Model): Observable<any> {
    // Model list changed — drop the sessionStorage cache so the next
    // getModels() call re-fetches fresh data (see VehicleTrackingService).
    sessionStorage.removeItem('models_cache');
    return this.apiRequest.post(
      'MM_Model_Master/UpdateModelStatus',
      data,
    );
  }

  //***************************** ModelMaster Services end ***********************************//

  //***************************** Area Image Master (Paint Verification) ******************//

  getAllAreaImages(): Observable<any[]> {
    return this.apiRequest.get('MM_Area_Image_Master/GetAllAreaImages');
  }

  /** Multipart save — FormData with metadata fields + optional File.
   *  postForGet so the new Area_Image_ID comes back. */
  saveAreaImage(form: FormData): Observable<any> {
    return this.apiRequest.postForGet('MM_Area_Image_Master/SaveAreaImage', form);
  }

  deleteAreaImage(areaImageId: number): Observable<any> {
    return this.apiRequest.post(`MM_Area_Image_Master/DeleteAreaImage/${areaImageId}`, {});
  }

  getAreaImageMappings(areaImageId: number): Observable<any[]> {
    return this.apiRequest.get(`MM_Area_Image_Master/GetMappings/${areaImageId}`);
  }

  saveAreaImageMapping(payload: any): Observable<any> {
    return this.apiRequest.postForGet('MM_Area_Image_Master/SaveMapping', payload);
  }

  deleteAreaImageMapping(mappingId: number): Observable<any> {
    return this.apiRequest.post(`MM_Area_Image_Master/DeleteMapping/${mappingId}`, {});
  }

  //***************************** Area Image Master end ***********************************//
}
