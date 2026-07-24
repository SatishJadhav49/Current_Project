import { inject, Injectable } from '@angular/core';
import { ApiRequestService } from '../../services';
import { Observable, of, tap } from 'rxjs';
import {
  AttributionItem,
  AuditArea,
  AuditorItem,
  BuildPhase,
  CountryItem,
  DefectItem,
  DefectTypeItem,
  DriveType,
  FuelType,
  LocationItem,
  ModelItem,
  PartItem,
  PlantItem,
  ReworkUpsertPayload,
  StageReworkUpsertPayload,
  SeverityItem,
  ShowerImportRequest,
  ShowerImportResult,
  StageSavePayload,
  StageStatusPayload,
  VehicleAreaItem,
  VehicleDetails,
} from './vehicle-tracking.models';

@Injectable({
  providedIn: 'root',
})
export class VehicleTrackingService {
  readonly apiRequest = inject(ApiRequestService);

  GetVinDetails(vin: string): Observable<VehicleDetails> {
    return this.apiRequest.get(`MM_Vehicle_Details/GetVinDetails/${vin}`);
  }

  SaveVehicle(payload: VehicleDetails): Observable<any> {
    // postForGet returns the datalist (the saved row with Vehicle_ID),
    // not just the response metadata that post() returns.
    return this.apiRequest.postForGet('MM_Vehicle_Details/SaveVehicle', payload);
  }

  getAuditAreas(): Observable<AuditArea[]> {
    return this.apiRequest.get('MM_Common/GetAuditAreaList');
  }

  getBuildPhases(): Observable<BuildPhase[]> {
    return this.apiRequest.get('MM_Common/GetBuildPhaseList');
  }

  getDriveTypes(): Observable<DriveType[]> {
    return this.apiRequest.get('MM_Common/GetDriveList');
  }

  getFuelTypes(): Observable<FuelType[]> {
    return this.apiRequest.get('MM_Common/GetFuelList');
  }

  getCountries(): Observable<CountryItem[]> {
    return this.apiRequest.get('MM_Common/GetCountryList');
  }

  getPlants(): Observable<PlantItem[]> {
    return this.apiRequest.get('MM_Common/GetPlantList');
  }

  /** SessionStorage key for the cached model list. Cleared by the Model
   *  Master admin page on any model change (see MasterService.updateModel). */
  static readonly MODELS_CACHE_KEY = 'models_cache';

  /** Models are common, small, and rarely change — cache them in
   *  sessionStorage so each component doesn't re-hit the API. */
  getModels(): Observable<ModelItem[]> {
    try {
      const cached = sessionStorage.getItem(VehicleTrackingService.MODELS_CACHE_KEY);
      if (cached) {
        const parsed = JSON.parse(cached);
        if (Array.isArray(parsed) && parsed.length > 0) {
          return of(parsed as ModelItem[]);
        }
      }
    } catch {
      // Corrupt cache — fall through to the API and rewrite it.
    }

    return this.apiRequest.get('MM_Model_Master/GetModelList').pipe(
      tap((list: ModelItem[]) => {
        try {
          if (Array.isArray(list) && list.length > 0) {
            sessionStorage.setItem(
              VehicleTrackingService.MODELS_CACHE_KEY,
              JSON.stringify(list),
            );
          }
        } catch {
          // Storage full/unavailable — caching is best-effort only.
        }
      }),
    );
  }

  getAuditorsByArea(auditAreaId: number): Observable<AuditorItem[]> {
    return this.apiRequest.get(
      `MM_Auditor_Master/GetAuditorsByArea/${auditAreaId}`,
    );
  }

  // ─── Masters used by the defect-entry-form ───────────────────────────────

  getVehicleAreas(): Observable<VehicleAreaItem[]> {
    return this.apiRequest.get('MM_Common/GetVehicleAreaList');
  }

  /** Active area images (with part hotspots) for one Vehicle Area + Model —
   *  powers the image-based Paint Verification audit. Images are per-model. */
  getAreaImagesByArea(vehicleAreaId: number, modelId: number): Observable<any[]> {
    return this.apiRequest.get(
      `MM_Area_Image_Master/GetImagesByArea/${vehicleAreaId}/${modelId}`,
    );
  }

  getSeverities(): Observable<SeverityItem[]> {
    return this.apiRequest.get('MM_Common/GetSeverityList');
  }

  getParts(): Observable<PartItem[]> {
    return this.apiRequest.get('MM_Part_Master/GetAllParts');
  }

  getDefects(): Observable<DefectItem[]> {
    return this.apiRequest.get('MM_Defect_Master/GetAllDefects');
  }

  getLocations(): Observable<LocationItem[]> {
    return this.apiRequest.get('MM_Location_Master/GetAllLocations');
  }

  getAttributions(): Observable<AttributionItem[]> {
    return this.apiRequest.get('MM_Attribution_Master/GetAllAttributions');
  }

  getDefectTypes(): Observable<DefectTypeItem[]> {
    return this.apiRequest.get('MM_Defect_Type_Master/GetAllDefectTypes');
  }

  // ─── Stage save endpoints ────────────────────────────────────────────────

  saveStage(payload: StageSavePayload): Observable<any> {
    return this.apiRequest.post('MM_Vehicle_Tracking/SaveStage', payload);
  }

  /** Trigger external shower-test SP and import its rows into our LSP tables.
   *  Uses postForGet because we need the ShowerImportResult payload back. */
  importShower(payload: ShowerImportRequest): Observable<ShowerImportResult> {
    return this.apiRequest.postForGet('MM_Shower_Test/ImportForVehicle', payload);
  }

  saveNoDefect(payload: StageSavePayload): Observable<any> {
    return this.apiRequest.post('MM_Vehicle_Tracking/SaveNoDefect', payload);
  }

  addConcern(payload: StageSavePayload): Observable<any> {
    // postForGet returns the datalist ({ Track_Log_ID, Defect_Entry_ID }) so
    // the parent can stamp the new ID on the row (needed for edit / delete).
    return this.apiRequest.postForGet('MM_Vehicle_Tracking/AddConcern', payload);
  }

  updateConcern(defectEntryId: number, payload: StageSavePayload): Observable<any> {
    return this.apiRequest.post(
      `MM_Vehicle_Tracking/UpdateConcern/${defectEntryId}`,
      payload,
    );
  }

  updateRoadTestKm(
    vehicleId: number,
    startKm: number | null,
    endKm: number | null,
  ): Observable<any> {
    return this.apiRequest.postForGet('MM_Vehicle_Tracking/UpdateRoadTestKm', {
      Vehicle_ID: vehicleId,
      Start_KM: startKm,
      End_KM: endKm,
      Inserted_Host: localStorage.getItem('Hostname') ?? '',
      Inserted_User_ID: Number(localStorage.getItem('Employee_ID')) || undefined,
      Plant_Code: localStorage.getItem('Plant_Code') ?? '',
    });
  }

  updateAuditor(
    vehicleId: number,
    auditAreaId: number,
    auditorName: string,
  ): Observable<any> {
    // postForGet so we get the boolean back ({datalist: true|false}).
    return this.apiRequest.postForGet('MM_Vehicle_Tracking/UpdateAuditor', {
      Vehicle_ID: vehicleId,
      Audit_Area_ID: auditAreaId,
      Auditor_Name: auditorName,
      Inserted_Host: localStorage.getItem('Hostname') ?? '',
      Inserted_User_ID: Number(localStorage.getItem('Employee_ID')) || undefined,
    });
  }

  deleteConcern(defectEntryId: number): Observable<any> {
    return this.apiRequest.post(
      `MM_Vehicle_Tracking/DeleteConcern/${defectEntryId}`,
      {},
    );
  }

  getStageLogsByVehicle(vehicleId: number): Observable<any[]> {
    return this.apiRequest.get(
      `MM_Vehicle_Tracking/GetStageLogsByVehicle/${vehicleId}`,
    );
  }

  getStageData(vehicleId: number, auditAreaId: number): Observable<any> {
    return this.apiRequest.get(
      `MM_Vehicle_Tracking/GetStageData/${vehicleId}/${auditAreaId}`,
    );
  }

  getRecheckData(vehicleId: number): Observable<any> {
    return this.apiRequest.get(
      `MM_Vehicle_Tracking/GetRecheckData/${vehicleId}`,
    );
  }

  // ─── Rework ──────────────────────────────────────────────────────────

  getReworkData(vehicleId: number): Observable<any> {
    return this.apiRequest.get(
      `MM_Vehicle_Tracking/GetReworkData/${vehicleId}`,
    );
  }

  /** Cross-vehicle pending rework list — powers the standalone Rework team
   *  screen at /rework. Pass attributionId=0 (or undefined) for "All",
   *  vinNo for a substring filter (full VIN or last 8 chars). */
  getPendingReworkList(
    attributionId?: number,
    vinNo?: string,
  ): Observable<any> {
    const params: string[] = [];
    if (attributionId && attributionId > 0) params.push(`attributionId=${attributionId}`);
    if (vinNo && vinNo.trim().length > 0) params.push(`vinNo=${encodeURIComponent(vinNo.trim())}`);
    const qs = params.length ? `?${params.join('&')}` : '';
    return this.apiRequest.get(`MM_Vehicle_Tracking/GetPendingReworkList${qs}`);
  }

  upsertRework(payload: ReworkUpsertPayload): Observable<any> {
    return this.apiRequest.postForGet(
      'MM_Vehicle_Tracking/UpsertRework',
      payload,
    );
  }

  /** Auto-save a stage's Documents list alone (Paint Verification attachments). */
  updateStageDocuments(payload: {
    Vehicle_ID: number;
    Audit_Area_ID: number;
    Documents: string[];
    Inserted_Host?: string;
    Inserted_User_ID?: number;
    Plant_Code?: string;
  }): Observable<any> {
    return this.apiRequest.post('MM_Vehicle_Tracking/UpdateStageDocuments', payload);
  }

  /** Stage-level rework upsert (DTC / ECOS / VMS failure resolution). */
  upsertStageRework(payload: StageReworkUpsertPayload): Observable<any> {
    return this.apiRequest.postForGet(
      'MM_Vehicle_Tracking/UpsertStageRework',
      payload,
    );
  }

  // ─── OK/NOK stages (DTC / ECOS / VMS) ───────────────────────────────────

  saveStageStatus(payload: StageStatusPayload): Observable<any> {
    return this.apiRequest.postForGet(
      'MM_Vehicle_Tracking/SaveStageStatus',
      payload,
    );
  }

  uploadStageFile(file: File, folder: string): Observable<any> {
    const form = new FormData();
    form.append('File', file);
    form.append('Folder', folder);
    // postForGet returns the saved file name string from datalist.
    return this.apiRequest.postForGet(
      'MM_Vehicle_Tracking/UploadStageFile',
      form,
    );
  }

  deleteStageFile(folder: string, fileName: string): Observable<any> {
    return this.apiRequest.postForGet(
      'MM_Vehicle_Tracking/DeleteStageFile',
      { Folder: folder, FileName: fileName },
    );
  }

  // ─── RFD ─────────────────────────────────────────────────────────────

  getRfdReadiness(vehicleId: number): Observable<any> {
    return this.apiRequest.get(
      `MM_Vehicle_Tracking/GetRfdReadiness/${vehicleId}`,
    );
  }

  saveRfd(payload: {
    Vehicle_ID: number;
    Operator_Name: string;
    Inserted_Host?: string;
    Inserted_User_ID?: number;
    Plant_Code?: string;
  }): Observable<any> {
    return this.apiRequest.postForGet('MM_Vehicle_Tracking/SaveRfd', payload);
  }
}
