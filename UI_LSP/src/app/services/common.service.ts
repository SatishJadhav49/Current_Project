import { inject, Injectable } from '@angular/core';
import { ApiRequestService } from './api-request.service';
import { map, Observable } from 'rxjs';
import {
  Audit_Area,
  Model,
  Shop,
} from '../Shared/models/common.model';

@Injectable({
  providedIn: 'root',
})
export class CommonService {
  readonly apiRequest = inject(ApiRequestService);

  getShops(): Observable<Shop[]> {
    return this.apiRequest.get('MM_Common/GetShopList');
  }

  getModels(shopids: number[]): Observable<Model[]> {
    return this.apiRequest.postForGet('MM_Common/GetModelList', shopids);
  }

  getAuditAreas(): Observable<Audit_Area[]> {
    return this.apiRequest.get('MM_Common/GetAuditAreaList');
  }
}
