import { Routes } from '@angular/router';
import {
  adminGuard,
  authGuard,
  guestGuard,
} from './core/auth.guard';
import { ShellComponent } from './layout/shell.component';

export const routes: Routes = [
  {
    path: 'login',
    canActivate: [guestGuard],
    loadComponent: () =>
      import('./features/login.component').then(
        (module) => module.LoginComponent,
      ),
  },
  {
    path: '',
    component: ShellComponent,
    canActivate: [authGuard],
    children: [
      {
        path: '',
        loadComponent: () =>
          import('./features/dashboard.component').then(
            (module) => module.DashboardComponent,
          ),
      },
      {
        path: 'students',
        loadComponent: () =>
          import('./features/students.component').then(
            (module) => module.StudentsComponent,
          ),
      },
      {
        path: 'students/:studentId/face-enrollment',
        loadComponent: () =>
          import(
            './features/face-enrollment.component'
          ).then(
            (module) =>
              module.FaceEnrollmentComponent,
          ),
      },
      {
        path: 'management',
        loadComponent: () =>
          import('./features/management.component').then(
            (module) => module.ManagementComponent,
          ),
      },
      {
        path: 'organization',
        loadComponent: () => import('./features/organization.component').then((module) => module.OrganizationComponent),
      },
      {
        path: 'videos',
        loadComponent: () =>
          import('./features/videos.component').then(
            (module) => module.VideosComponent,
          ),
      },
      {
        path: 'sessions',
        loadComponent: () =>
          import('./features/sessions.component').then(
            (module) => module.SessionsComponent,
          ),
      },
      {
        path: 'sessions/:id',
        loadComponent: () =>
          import(
            './features/session-detail.component'
          ).then(
            (module) =>
              module.SessionDetailComponent,
          ),
      },
      {
        path: 'alerts',
        loadComponent: () =>
          import('./features/alerts.component').then(
            (module) => module.AlertsComponent,
          ),
      },
      {
        path: 'history',
        loadComponent: () =>
          import('./features/history.component').then(
            (module) => module.HistoryComponent,
          ),
      },
      {
        path: 'reports',
        loadComponent: () =>
          import('./features/reports.component').then(
            (module) => module.ReportsComponent,
          ),
      },
      {
        path: 'search',
        loadComponent: () =>
          import('./features/search.component').then(
            (module) => module.SearchComponent,
          ),
      },
      {
        path: 'attendance',
        loadComponent: () =>
          import('./features/policies.component').then(
            (module) => module.PoliciesComponent,
          ),
      },
      {
        path: 'policies',
        redirectTo: 'attendance',
        pathMatch: 'full',
      },
      {
        path: 'system',
        loadComponent: () =>
          import('./features/system.component').then(
            (module) => module.SystemComponent,
          ),
      },
      {
        path: 'account',
        loadComponent: () =>
          import('./features/account.component').then(
            (module) => module.AccountComponent,
          ),
      },
      {
        path: 'admin/users',
        canActivate: [adminGuard],
        loadComponent: () =>
          import(
            './features/admin-users.component'
          ).then(
            (module) =>
              module.AdminUsersComponent,
          ),
      },
      {
        path: 'admin/audit-logs',
        canActivate: [adminGuard],
        loadComponent: () =>
          import('./features/audit.component').then(
            (module) => module.AuditComponent,
          ),
      },
      {
        path: 'admin/audit',
        redirectTo: 'admin/audit-logs',
        pathMatch: 'full',
      },
    ],
  },
  {
    path: '**',
    redirectTo: '',
  },
];
