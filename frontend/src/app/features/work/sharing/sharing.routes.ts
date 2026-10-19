import type { Routes } from '@angular/router';
import { catalogRoute } from '@core/auth/feature-guard';
import { optionalId } from '@core/layout/optional-id';

export const sharingRoutes: Routes = [
  catalogRoute('sharing.reader', {
    path: 'reader/:shareId/:id',
    loadComponent: () =>
      import('@features/work/knowledge/documents/document-reader').then(
        (module) => module.DocumentReader,
      ),
  }),
  catalogRoute('sharing', {
    matcher: optionalId('reader'),
    loadComponent: () => import('./shared-page').then((module) => module.SharedPage),
  }),
];
