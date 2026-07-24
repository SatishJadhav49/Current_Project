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
}
