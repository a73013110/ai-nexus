import type { Routes } from '@angular/router';
import { catalogRoute } from '@core/auth/feature-guard';
import { optionalId } from '@core/layout/optional-id';
import { pendingChanges } from '@shared/browser/pending-changes';

export const artifactsRoutes: Routes = [
  catalogRoute('artifacts', {
    matcher: optionalId(),
    canDeactivate: [pendingChanges],
    loadComponent: () => import('./artifacts-page').then((module) => module.ArtifactsPage),
  }),
];
