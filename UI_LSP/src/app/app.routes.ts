import { Routes } from '@angular/router';
import { PlaceholderComponent } from './placeholder.component';
import { AuthLoadingComponent } from './auth/components/auth-loading.component';
import { NoAccessComponent } from './auth/components/no-access.component';
import { NoMenuAccessComponent } from './auth/components/no-menu-access.component';
import { AuthGuard, MenuAccessGuard } from './auth/guards';

export const routes: Routes = [
  { path: '', component: AuthLoadingComponent }, // Start with authentication
  { path: 'auth', component: AuthLoadingComponent },
  { path: 'no-access', component: NoAccessComponent },
  { path: 'no-menu-access', component: NoMenuAccessComponent },

  {
    path: 'dashboard',
    loadComponent: () =>
      import('./Features/Dashboard/dashboard.component').then(
        (m) => m.DashboardComponent
      ),
    canActivate: [AuthGuard], // Only require authentication, no menu check
  },
  {
    path: 'usermanagement',
    loadChildren: () =>
      import('./Features/UserManagement/shared/user.routes').then(
        (m) => m.UserRoutes
      ),
    canActivate: [AuthGuard], // Require both auth and menu access
  },
  {
    path: 'master',
    loadChildren: () =>
      import('./Features/Masters/masters.routes').then((m) => m.MastersRoutes),
  },
  {
    path: 'vehicle',
    loadChildren: () =>
      import('./Features/VehicleTracking/vehicle-tracking.routes').then(
        (m) => m.VehicleTrackingRoutes
      ),
  },
  {
    path: 'rework/rework',
    loadComponent: () =>
      import('./Features/Rework/rework.component').then(
        (m) => m.ReworkComponent
      ),
  },
  {
    path: 'action',
    loadComponent: () =>
      import('./Features/ActionPlan/action-plan.component').then(
        (m) => m.ActionPlanComponent
      ),
  },
  {
    path: 'documents',
    loadChildren: () =>
      import('./Features/Documents/documents.routes').then((m) => m.DocumentsRoutes),
  },
  {
    path: 'reports',
    loadChildren: () =>
      import('./Features/Reports/reports.routes').then((m) => m.ReportsRoutes),
  },
  //Wildcard route - must be last
  { path: '**', component: PlaceholderComponent },
];
