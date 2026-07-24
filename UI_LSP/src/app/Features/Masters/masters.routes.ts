import { Routes } from '@angular/router';

export const MastersRoutes: Routes = [
  {
    path: 'attributionmaster',
    loadComponent: () =>
      import('./attribution/attribution.component').then(
        (m) => m.AttributionMasterComponent,
      ),
  },
  {
    path: 'partmaster',
    loadComponent: () =>
      import('./part/part.component').then((m) => m.PartComponent),
  },
  {
    path: 'defectmaster',
    loadComponent: () =>
      import('./defect/defect.component').then((m) => m.DefectComponent),
  },
  {
    path: 'locationmaster',
    loadComponent: () =>
      import('./location/location.component').then((m) => m.LocationComponent),
  },
  {
    path: 'auditormaster',
    loadComponent: () =>
      import('./auditor/auditor.component').then((m) => m.AuditorComponent),
  },
  {
    path: 'defecttypemaster',
    loadComponent: () =>
      import('./defecttype/defecttype.component').then(
        (m) => m.DefecttypeComponent,
      ),
  },
  {
    path:'modelmaster',
    loadComponent: () =>
      import('./model/model.component').then(
        (m) => m.ModelComponent,
      ),
  },
  {
    path: 'areaimagemaster',
    loadComponent: () =>
      import('./areaimage/areaimage.component').then(
        (m) => m.AreaImageMasterComponent,
      ),
  }
];
