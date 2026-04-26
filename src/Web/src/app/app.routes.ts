import { Routes } from '@angular/router';
import { authGuard } from './core/auth/auth.guard';

export const routes: Routes = [
  { path: '', redirectTo: '/pipeline', pathMatch: 'full' },
  {
    path: 'login',
    loadComponent: () => import('./features/auth/login.component').then(m => m.LoginComponent)
  },
  {
    path: 'pipeline',
    loadComponent: () => import('./features/pipeline/pipeline.component').then(m => m.PipelineComponent),
    canActivate: [authGuard]
  },
  {
    path: 'contacts',
    loadComponent: () => import('./features/contacts/contacts.component').then(m => m.ContactsComponent),
    canActivate: [authGuard]
  },
  {
    path: 'deals/:id',
    loadComponent: () => import('./features/deal-detail/deal-detail.component').then(m => m.DealDetailComponent),
    canActivate: [authGuard]
  },
  {
    path: 'settings',
    loadComponent: () => import('./features/settings/settings.component').then(m => m.SettingsComponent),
    canActivate: [authGuard]
  },
  { path: '**', redirectTo: '/pipeline' }
];
