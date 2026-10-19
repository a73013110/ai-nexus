import type { Routes } from '@angular/router';
import { moduleRoute, unavailableRoute } from '@core/layout/module-route';

/**
 * AI 工作台區的模組；網址第二段是模組 ID（`FeatureCatalog`），各模組的頁面在自己的 `<module>.routes.ts`。
 * 最後的 `**` 接住不存在的模組；模組內被 `featureGuard` 擋下的頁面由 `moduleRoute` 補上的 `**` 接住。
 */
export const workRoutes: Routes = [
  moduleRoute('dashboard', () =>
    import('@features/work/dashboard/dashboard.routes').then((module) => module.dashboardRoutes),
  ),
  moduleRoute('chat', () =>
    import('@features/work/chat/chat.routes').then((module) => module.chatRoutes),
  ),
  moduleRoute('projects', () =>
    import('@features/work/projects/projects.routes').then((module) => module.projectsRoutes),
  ),
  moduleRoute('files', () =>
    import('@features/work/files/files.routes').then((module) => module.filesRoutes),
  ),
  moduleRoute('knowledge', () =>
    import('@features/work/knowledge/knowledge.routes').then((module) => module.knowledgeRoutes),
  ),
  moduleRoute('artifacts', () =>
    import('@features/work/artifacts/artifacts.routes').then((module) => module.artifactsRoutes),
  ),
  moduleRoute('sharing', () =>
    import('@features/work/sharing/sharing.routes').then((module) => module.sharingRoutes),
  ),
  moduleRoute('jobs', () =>
    import('@features/work/jobs/jobs.routes').then((module) => module.jobsRoutes),
  ),
  moduleRoute('repositories', () =>
    import('@features/work/repositories/repositories.routes').then(
      (module) => module.repositoriesRoutes,
    ),
  ),
  unavailableRoute,
];
