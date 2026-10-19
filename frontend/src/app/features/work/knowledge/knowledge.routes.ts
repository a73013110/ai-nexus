import type { Routes } from '@angular/router';
import { catalogRoute } from '@core/auth/feature-guard';
import { optionalId } from '@core/layout/optional-id';

export const knowledgeRoutes: Routes = [
  // 固定子路徑排在 optionalId 之前，並列為保留字，否則單段的 `retrieval` 會被當成集合 id。
  catalogRoute('knowledge.retrieval', {
    path: 'retrieval',
    loadComponent: () => import('./retrieval-page').then((module) => module.RetrievalPage),
  }),
  catalogRoute('knowledge.reader', {
    path: 'reader/:id',
    loadComponent: () =>
      import('./documents/document-reader').then((module) => module.DocumentReader),
  }),
  catalogRoute('knowledge', {
    matcher: optionalId('retrieval', 'reader'),
    loadComponent: () => import('./knowledge-page').then((module) => module.KnowledgePage),
  }),
];
