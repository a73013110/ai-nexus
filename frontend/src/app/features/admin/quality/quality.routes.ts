import type { Routes } from '@angular/router';
import { catalogRoute } from '@core/auth/feature-guard';
import { optionalId } from '@core/layout/optional-id';

export const qualityRoutes: Routes = [
  // 固定子路徑排在 optionalId 之前，並列為保留字，否則單段的 `retrieval` 會被當成評測集 id。
  catalogRoute('quality.retrieval', {
    path: 'retrieval',
    loadComponent: () =>
      import('./retrieval-evaluation').then((module) => module.RetrievalEvaluation),
  }),
  catalogRoute('quality', {
    matcher: optionalId('retrieval'),
    loadComponent: () => import('./quality-page').then((module) => module.QualityPage),
  }),
];
