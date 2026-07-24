import { Injectable } from '@angular/core';
import { ApiRequestService } from '../../services';
import { Observable } from 'rxjs';
import {
  AttributionWiseDestribution,
  Cards_Data,
  ProcessStagesWiseDistribution,
  SeverityWiseDistribution,
  SourceWiseDestribution,
} from './dashboard.model';

@Injectable({
  providedIn: 'root',
})
export class DashboardService {
  constructor(private apiRequest: ApiRequestService) {}

  getCardsData(UserData: any): Observable<Cards_Data> {
    return this.apiRequest.get('MM_Dashboard/GetDashboardCounts', UserData);
  }

  // MM_Dashboard/GetSourceWiseDistribution
  getSourceWiseDistribution(
    UserData: any
  ): Observable<SourceWiseDestribution[]> {
    return this.apiRequest.get(
      'MM_Dashboard/GetSourceWiseDistribution',
      UserData
    );
  }

  getAttributionWiseDistribution(
    UserData: any
  ): Observable<AttributionWiseDestribution[]> {
    return this.apiRequest.get(
      'MM_Dashboard/GetAttributionWiseDistribution',
      UserData
    );
  }

  GetProcessStagesWiseDistribution(
    UserData: any
  ): Observable<ProcessStagesWiseDistribution[]> {
    return this.apiRequest.get(
      'MM_Dashboard/GetProcessStagesWiseDistribution',
      UserData
    );
  }

  getSeverityWiseDistribution(
    UserData: any
  ): Observable<SeverityWiseDistribution[]> {
    return this.apiRequest.get(
      'MM_Dashboard/GetSeverityWiseDistribution',
      UserData
    );
  }


  getResponsibleUserData(Employee_ID:number): Observable<any> {
    return this.apiRequest.get(
      'MM_Dashboard/getResponsibleUserData/'+Employee_ID
    );
  }
}
