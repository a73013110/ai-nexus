import type { Routes } from '@angular/router';
import { catalogRoute } from '@core/auth/feature-guard';
import { optionalId } from '@core/layout/optional-id';

export const projectsRoutes: Routes = [
  catalogRoute('projects', {
    matcher: optionalId(),
    loadComponent: () => import('./projects-page').then((module) => module.ProjectsPage),
  }),
];
