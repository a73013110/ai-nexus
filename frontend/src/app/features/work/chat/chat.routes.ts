import type { Routes } from '@angular/router';
import { catalogRoute } from '@core/auth/feature-guard';
import { optionalId } from '@core/layout/optional-id';

export const chatRoutes: Routes = [
  // 對話列表與 `/:id` 共用同一條路由，因此開始新對話時工作區不會改變。
  catalogRoute('chat', {
    matcher: optionalId(),
    loadComponent: () => import('./chat-workspace').then((module) => module.ChatWorkspace),
  }),
];
