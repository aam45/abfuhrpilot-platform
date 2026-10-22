import { Routes } from '@angular/router';
import { authGuard, roleGuard } from './core/auth/guards';
import { ShellComponent } from './core/layout/shell.component';

const OPS = roleGuard('Admin', 'Dispatcher', 'Viewer');

export const routes: Routes = [
  { path: 'login', loadComponent: () => import('./features/auth/login.component').then(m => m.LoginComponent) },
  { path: 'passwort', loadComponent: () => import('./features/auth/password-reset.component').then(m => m.PasswordResetComponent) },
  {
    path: '',
    component: ShellComponent,
    canActivate: [authGuard],
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      { path: 'dashboard', canActivate: [OPS], loadComponent: () => import('./features/dashboard/dashboard.component').then(m => m.DashboardComponent) },
      { path: 'routen', canActivate: [OPS], loadComponent: () => import('./features/routes/routes-list.component').then(m => m.RoutesListComponent) },
      { path: 'routen/neu', canActivate: [roleGuard('Admin', 'Dispatcher')], loadComponent: () => import('./features/routes/route-create.component').then(m => m.RouteCreateComponent) },
      { path: 'routen/:id', canActivate: [OPS], loadComponent: () => import('./features/routes/route-detail.component').then(m => m.RouteDetailComponent) },
      { path: 'fahrer', loadComponent: () => import('./features/driver/driver.component').then(m => m.DriverComponent) },
      { path: 'auftraege', canActivate: [OPS], data: { config: 'orders' }, loadComponent: () => import('./features/catalog/generic-list.component').then(m => m.GenericListComponent) },
      { path: 'container', canActivate: [OPS], data: { config: 'containers' }, loadComponent: () => import('./features/catalog/generic-list.component').then(m => m.GenericListComponent) },
      { path: 'fahrzeuge', canActivate: [OPS], data: { config: 'vehicles' }, loadComponent: () => import('./features/catalog/generic-list.component').then(m => m.GenericListComponent) },
      { path: 'fahrerinnen', canActivate: [OPS], data: { config: 'drivers' }, loadComponent: () => import('./features/catalog/generic-list.component').then(m => m.GenericListComponent) },
      { path: 'abfallarten', canActivate: [OPS], data: { config: 'wasteTypes' }, loadComponent: () => import('./features/catalog/generic-list.component').then(m => m.GenericListComponent) },
      { path: 'ziele', canActivate: [OPS], data: { config: 'destinations' }, loadComponent: () => import('./features/catalog/generic-list.component').then(m => m.GenericListComponent) },
      { path: 'benutzer', canActivate: [roleGuard('Admin')], loadComponent: () => import('./features/users/users.component').then(m => m.UsersComponent) },
    ],
  },
  { path: '**', redirectTo: '' },
];
