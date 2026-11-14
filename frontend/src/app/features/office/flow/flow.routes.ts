import type { Routes } from '@angular/router';
import { catalogRoute } from '@core/auth/feature-guard';

/** 簽核：審核與申請清單（`/office/flow/approvals`）、各種申請的流程設定（`/office/flow/definitions`）。 */
export const flowRoutes: Routes = [
  catalogRoute('flow.approvals', {
    path: 'approvals',
    loadComponent: () =>
      import('./approvals/flow-approvals-page').then((module) => module.FlowApprovalsPage),
  }),
  catalogRoute('flow.definitions', {
    path: 'definitions',
    loadComponent: () =>
      import('./definitions/flow-definitions-page').then((module) => module.FlowDefinitionsPage),
  }),
];
