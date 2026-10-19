import type { Routes } from '@angular/router';
import { catalogRoute } from '@core/auth/feature-guard';

const reader = () =>
  import('@features/work/knowledge/documents/document-reader').then(
    (module) => module.DocumentReader,
  );

export const filesRoutes: Routes = [
  catalogRoute('files.reader', { path: 'reader/:id', loadComponent: reader }),
  catalogRoute('files', {
    path: '',
    loadComponent: () => import('./files-page').then((module) => module.FilesPage),
  }),
];
