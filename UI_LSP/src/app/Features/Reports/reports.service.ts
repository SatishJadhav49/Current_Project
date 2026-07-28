import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiRequestService } from '../../services';

/** Filter payload for the Vehicle Tracking report. Dates are yyyy-MM-dd. */
export interface VehicleTrackingReportRequest {
  Model_ID: number;
  StartDate: string;
  EndDate: string;
}

/** One report row per vehicle — mirrors the backend DTO / xlsx columns. */
export interface VehicleTrackingReportRow {
  VIN_No: string;
  Body_Serial_No: string;
  Model_Name: string;
  Model_Desc: string;
  Variant: string;
  Colour_Desc: string;
  Engine_No: string;
  Build_Phase_Name: string;
  Drive_Name: string;
  Fuel_Name: string;
  Country_Name: string;
  Roll_Down_Date: string | null;
  CAI_OK_Date: string | null;
  RoadTest_Date: string | null;
  RoadTest_Status: string;
  UnderBody_Date: string | null;
  UnderBody_Status: string;
  Dtc_Date: string | null;
  Dtc_Status: string;
  Shower_Date: string | null;
  Shower_Status: string;
  StaticAudit_Date: string | null;
  StaticAudit_Status: string;
  Vms_Date: string | null;
  Vms_Status: string;
  Paint_Date: string | null;
  Paint_Status: string;
  Recheck_Status: string;
  Rework_Status: string;
  Rfd_Date: string | null;
  Rfd_Status: string;
}

/** Filter payload for the Daily report. ReportDate is yyyy-MM-dd. */
export interface DailyReportRequest {
  Model_ID: number;
  ReportDate: string;
}

/** Per-stage vehicle count completed on the report date. */
export interface DailyStageCount {
  Audit_Area_ID: number;
  Stage_Name: string;
  Completed_Count: number;
}

/** One defect entered on the report date. */
export interface DailyDefectRow {
  VIN_No: string;
  Stage_Name: string;
  Vehicle_Area_Name: string;
  Part_Name: string;
  Defect_Name: string;
  Location_Name: string;
  Severity_Code: string;
  Attribution_Name: string;
  Defect_Type_Name: string;
  Auditor_Name: string;
  Remark: string;
  Rework_Status: string;
  Entered_Date: string | null;
}

/** Full Daily report payload — mirrors the backend DTO. */
export interface DailyReportResponse {
  Model_Name: string;
  ReportDate: string;
  RfdDoneCount: number;
  VehiclesActiveCount: number;
  TotalDefectsCount: number;
  StageCounts: DailyStageCount[];
  Defects: DailyDefectRow[];
}

// ─── WIP / Pending report ─────────────────────────────────────────────────
export interface WipReportRequest {
  Model_ID: number;
  StartDate: string;
  EndDate: string;
}
export interface WipStuckCount {
  Stage_Name: string;
  Count: number;
}
export interface WipRow {
  VIN_No: string;
  Model_Name: string;
  Vehicle_Date: string | null;
  Roll_Down_Date: string | null;
  Age_Days: number;
  Last_Stage_Name: string;
  Last_Stage_Date: string | null;
  Stuck_Stage_Name: string;
  Last_Activity_Date: string | null;
  Idle_Days: number;
  Pending_Concerns: number;
}
export interface WipReportResponse {
  Model_Name: string;
  StartDate: string;
  EndDate: string;
  TotalWip: number;
  StuckCounts: WipStuckCount[];
  Rows: WipRow[];
}

// ─── Stage TAT report ─────────────────────────────────────────────────────
export interface TatReportRequest {
  Model_ID: number;
  StartDate: string;
  EndDate: string;
}
export interface TatTransition {
  From_Stage: string;
  To_Stage: string;
  Avg_Hours: number;
  Min_Hours: number;
  Max_Hours: number;
  Vehicle_Count: number;
}
export interface TatReportResponse {
  Model_Name: string;
  StartDate: string;
  EndDate: string;
  VehicleCount: number;
  OverallAvgHours: number | null;
  OverallMinHours: number | null;
  OverallMaxHours: number | null;
  OverallCount: number;
  Transitions: TatTransition[];
}

// ─── Image Defect report (Paint) ──────────────────────────────────────────
export interface ImageHeatmapRequest {
  Model_ID: number;
  Vehicle_Area_ID: number;
  StartDate: string;
  EndDate: string;
}
export interface HeatHotspot {
  Part_ID: number;
  Part_Name: string;
  Shape: string;
  CenterX: number;
  CenterY: number;
}
export interface HeatImage {
  Area_Image_ID: number;
  Image_Name: string;
  Image_FileName: string;
  DefectCount: number;
  Hotspots: HeatHotspot[];
}
export interface ImageDefectRow {
  Defect_Entry_ID: number;
  Area_Image_ID: number;
  Part_ID: number;
  VIN_No: string;
  Part_Name: string;
  Defect_Name: string;
  Location_Name: string;
  Severity_Code: string;
  Attribution_Name: string;
  Defect_Type_Name: string;
  Auditor_Name: string;
  Remark: string;
  Rework_Status: string;
}
export interface ImageHeatmapResponse {
  Model_Name: string;
  Vehicle_Area_Name: string;
  StartDate: string;
  EndDate: string;
  TotalDefects: number;
  UnplacedCount: number;
  Images: HeatImage[];
  Defects: ImageDefectRow[];
}

@Injectable({
  providedIn: 'root',
})
export class ReportsService {
  private readonly apiRequest = inject(ApiRequestService);

  /** postForGet — the response body (the rows) is what we need. */
  getVehicleTrackingReport(
    payload: VehicleTrackingReportRequest,
  ): Observable<VehicleTrackingReportRow[]> {
    return this.apiRequest.postForGet(
      'MM_Reports/GetVehicleTrackingReport',
      payload,
    );
  }

  /** postForGet — response body is the DailyReportResponse object. */
  getDailyReport(payload: DailyReportRequest): Observable<DailyReportResponse> {
    return this.apiRequest.postForGet('MM_Reports/GetDailyReport', payload);
  }

  /** postForGet — response body is the WipReportResponse object. */
  getWipReport(payload: WipReportRequest): Observable<WipReportResponse> {
    return this.apiRequest.postForGet('MM_Reports/GetWipReport', payload);
  }

  /** postForGet — response body is the TatReportResponse object. */
  getTatReport(payload: TatReportRequest): Observable<TatReportResponse> {
    return this.apiRequest.postForGet('MM_Reports/GetTatReport', payload);
  }

  /** postForGet — response body is the ImageHeatmapResponse object. */
  getImageHeatmapReport(
    payload: ImageHeatmapRequest,
  ): Observable<ImageHeatmapResponse> {
    return this.apiRequest.postForGet('MM_Reports/GetImageHeatmapReport', payload);
  }
}
