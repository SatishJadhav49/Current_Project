import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiRequestService } from '../../services';

export interface ActionPlanProblem {
  Model_ID: number;
  Part_ID: number;
  Part_Name: string;
  Defect_ID: number;
  Defect_Name: string;
  Attribution_ID: number;
  Attribution_Name: string;
  Severity_ID: number;
  Severity_Code: string;
  Frequency: number;
  Cutoff_Date: string | null;
  Concerns_After_Cutoff: number;
  Status: string; // R / Y / G
  Has_Plan: boolean;
  Is_Submitted: boolean;
  Action_Plan_ID: number;
}

export interface ActionPlanRootCause {
  Root_Cause_ID: number;
  Root_Cause: string;
  Added_By: string;
  Added_On: string | null;
}
export interface ActionPlanIca {
  ICA_ID: number;
  Action: string;
  Responsibility: string;
  ICA_Date: string | null;
  Added_By: string;
  Added_On: string | null;
}
export interface ActionPlanPca {
  PCA_ID: number;
  Action: string;
  Responsibility: string;
  Target_Date: string | null;
  Status: string;
  Added_By: string;
  Added_On: string | null;
}
/** One occurrence of a problem — vehicle, when/where logged, other defects. */
export interface ProblemHistoryRow {
  VIN_No: string;
  Audit_Date: string | null;
  Audit_Area_Name: string;
  Other_Defects: string;
}

export interface ActionPlanDetail {
  Action_Plan_ID: number;
  Part_ID: number;
  Defect_ID: number;
  Attribution_ID: number;
  Severity_ID: number;
  Is_Submitted: boolean;
  RootCauses: ActionPlanRootCause[];
  Icas: ActionPlanIca[];
  Pcas: ActionPlanPca[];
}

export interface ActionPlanSubmit {
  Model_ID: number;
  Part_ID: number;
  Defect_ID: number;
  Attribution_ID: number;
  Severity_ID: number;
  RootCauses: { Root_Cause: string }[];
  Icas: { Action: string; Responsibility: string; ICA_Date: string | null }[];
  Pcas: { Action: string; Responsibility: string; Target_Date: string | null; Status: string }[];
  Inserted_Host?: string;
  Inserted_User_ID?: number;
  Plant_Code?: string;
}

@Injectable({ providedIn: 'root' })
export class ActionPlanService {
  private readonly apiRequest = inject(ApiRequestService);

  getProblems(modelId: number): Observable<ActionPlanProblem[]> {
    return this.apiRequest.get(`MM_Action_Plan/GetProblems/${modelId}`);
  }

  getPlan(
    modelId: number,
    partId: number,
    defectId: number,
    attributionId: number,
    severityId: number,
  ): Observable<ActionPlanDetail> {
    return this.apiRequest.get(
      `MM_Action_Plan/GetPlan/${modelId}/${partId}/${defectId}/${attributionId}/${severityId}`,
    );
  }

  getProblemHistory(
    modelId: number,
    partId: number,
    defectId: number,
    attributionId: number,
    severityId: number,
  ): Observable<ProblemHistoryRow[]> {
    return this.apiRequest.get(
      `MM_Action_Plan/GetProblemHistory/${modelId}/${partId}/${defectId}/${attributionId}/${severityId}`,
    );
  }

  /** postForGet — response body carries the saved plan id. */
  submitPlan(payload: ActionPlanSubmit): Observable<any> {
    return this.apiRequest.postForGet('MM_Action_Plan/SubmitPlan', payload);
  }
}
