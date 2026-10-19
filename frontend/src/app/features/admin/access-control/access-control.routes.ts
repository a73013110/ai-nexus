import type { Routes } from '@angular/router';
import { catalogRoute } from '@core/auth/feature-guard';

export const accessControlRoutes: Routes = [
  catalogRoute('access-control.roles', {
    path: 'roles',
    loadComponent: () => import('./roles-page').then((module) => module.RolesPage),
  }),
  catalogRoute('access-control.groups', {
    path: 'groups',
    loadComponent: () => import('./groups-page').then((module) => module.GroupsPage),
  }),
  catalogRoute('access-control.features', {
    path: 'features',
    loadComponent: () => import('./features-page').then((module) => module.FeaturesPage),
  }),
  catalogRoute('access-control.navigation', {
    path: 'navigation',
    loadComponent: () => import('./navigation-page').then((module) => module.NavigationPage),
  }),
];
