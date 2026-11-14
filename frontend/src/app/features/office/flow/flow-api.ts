import { Injectable, inject } from '@angular/core';
import { ApiClient } from '@core/api/api-client';
import type { FlowDecisionRequest, FlowDefinitionRequest } from '@core/api/schema';

/** 申請清單的檢視：`tasks` 待我審核、`mine` 我送出的、`decided` 我審過的、`all` 全部（需要 `flow.approvals.manage`）。 */
export type FlowView = 'tasks' | 'mine' | 'decided' | 'all';

/**
 * 簽核的端點：待我審核、申請清單與詳情、核准退回與撤回（`flow.approvals`）；流程設定另需 `flow.definitions`。
 * 決定要帶回詳情的 `currentStep`：關卡已推進時伺服器拒絕，不會把別人剛處理的一關再決定一次。
 */
@Injectable({ providedIn: 'root' })
export class FlowApi {
  private readonly api = inject(ApiClient);
  tasks = (signal?: AbortSignal) => this.api.get('/api/v1/flow/tasks', { signal });
  /** 新的在前；`before` 是上一頁最後一筆的 ID。 */
  instances = (
    view: Exclude<FlowView, 'tasks'>,
    status: string,
    before: string | undefined,
    signal?: AbortSignal,
  ) =>
    this.api.get('/api/v1/flow/instances', {
      query: { view, status: status || undefined, before },
      signal,
    });
  instance = (id: string, signal?: AbortSignal) =>
    this.api.get('/api/v1/flow/instances/{id}', { path: { id }, signal });
  decide = (id: string, body: FlowDecisionRequest) =>
    this.api.post('/api/v1/flow/instances/{id}/decision', { path: { id }, body });
  cancel = (id: string) => this.api.post('/api/v1/flow/instances/{id}/cancel', { path: { id } });
  definitions = (signal?: AbortSignal) =>
    this.api.get('/api/v1/admin/flow-definitions', { signal });
  /** `rowVersion` 為 null 表示建立（還沒有流程設定的種類）。 */
  saveDefinition = (kind: string, body: FlowDefinitionRequest) =>
    this.api.put('/api/v1/admin/flow-definitions/{kind}', { path: { kind }, body });
}
