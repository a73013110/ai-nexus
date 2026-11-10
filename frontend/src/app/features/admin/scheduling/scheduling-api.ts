import { Injectable, inject } from '@angular/core';
import { ApiClient } from '@core/api/api-client';
import type { TableRequestQuery } from '@shared/ui/table-model';

/**
 * 背景作業的端點（`scheduling`）：排程任務看板、各任務的執行紀錄、佇列工作與常駐服務；執行、停止、暫停與恢復另需 `scheduling.manage`。
 * 立即執行只記下要求就回應，結果靠輪詢看板看。
 */
@Injectable({ providedIn: 'root' })
export class SchedulingApi {
  private readonly api = inject(ApiClient);
  board = (signal?: AbortSignal) => this.api.get('/api/v1/scheduled-tasks', { signal });
  runs = (key: string, table: TableRequestQuery, signal?: AbortSignal) =>
    this.api.get('/api/v1/scheduled-tasks/{key}/runs', { path: { key }, query: table, signal });
  run = (key: string) => this.api.post('/api/v1/scheduled-tasks/{key}/run', { path: { key } });
  cancel = (key: string) =>
    this.api.post('/api/v1/scheduled-tasks/{key}/cancel', { path: { key } });
  pause = (key: string) => this.api.post('/api/v1/scheduled-tasks/{key}/pause', { path: { key } });
  resume = (key: string) =>
    this.api.post('/api/v1/scheduled-tasks/{key}/resume', { path: { key } });
  jobs = (table: TableRequestQuery, signal?: AbortSignal) =>
    this.api.get('/api/v1/background-jobs', { query: table, signal });
  cancelJob = (id: string) =>
    this.api.post('/api/v1/background-jobs/{id}/cancel', { path: { id } });
  services = (signal?: AbortSignal) => this.api.get('/api/v1/resident-services', { signal });
}
