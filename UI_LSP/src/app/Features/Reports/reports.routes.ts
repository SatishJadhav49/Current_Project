import { Routes } from '@angular/router';

export const ReportsRoutes: Routes = [
  {
    path: 'vehicletracking',
    loadComponent: () =>
      import('./vehicletrackingreport/vehicletrackingreport.component').then(
        (m) => m.VehicletrackingreportComponent,
      ),
  },
  {
    path: 'daily',
    loadComponent: () =>
      import('./dailyreport/dailyreport.component').then(
        (m) => m.DailyreportComponent,
      ),
  },
  {
    path: 'wip',
    loadComponent: () =>
      import('./wipreport/wipreport.component').then(
        (m) => m.WipreportComponent,
      ),
  },
  {
    path: 'tat',
    loadComponent: () =>
      import('./tatreport/tatreport.component').then(
        (m) => m.TatreportComponent,
      ),
  },
  {
    path: 'imageheatmap',
    loadComponent: () =>
      import('./imageheatmapreport/imageheatmapreport.component').then(
        (m) => m.ImageheatmapreportComponent,
      ),
  },

];
