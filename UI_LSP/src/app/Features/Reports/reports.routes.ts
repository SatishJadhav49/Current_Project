import { Routes } from '@angular/router';

export const ReportsRoutes: Routes = [
  {
    path: 'vehicletracking',
    loadComponent: () =>
      import('./vehicletrackingreport/vehicletrackingreport.component').then(
        (m) => m.VehicletrackingreportComponent,
      ),
  },

];
