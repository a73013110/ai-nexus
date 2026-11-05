import { Injectable, inject } from '@angular/core';
import { ApiClient } from '@core/api/api-client';
import type { AssistantQuestion } from '@core/api/schema';

/** 全站助理的端點。可以問什麼由後端各模組登記的工具決定，前端只送提問與所在頁面的條件。 */
@Injectable({ providedIn: 'root' })
export class AssistantApi {
  private readonly api = inject(ApiClient);
  /**
   * 提問，回答以 SSE 送回（`grounding`、`tool`、`delta`…、`done`）。回應尚未開始前的失敗
   * （畫面條件不正確、額度、模型）是一般的錯誤回應。
   */
  ask = (body: AssistantQuestion, signal: AbortSignal) =>
    this.api.stream('/api/v1/assistant/questions', { body, signal });
}
