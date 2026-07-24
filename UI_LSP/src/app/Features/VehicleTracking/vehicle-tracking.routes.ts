import { Routes } from '@angular/router';

export const VehicleTrackingRoutes: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./vehicle-tracking-home/vehicle-tracking-home.component').then(
        (m) => m.VehicleTrackingHomeComponent,
      ),
  },
  {
    path: 'vehicletracking',
    loadComponent: () =>
      import('./vehicle-tracking-home/vehicle-tracking-home.component').then(
        (m) => m.VehicleTrackingHomeComponent,
      ),
  },
];
