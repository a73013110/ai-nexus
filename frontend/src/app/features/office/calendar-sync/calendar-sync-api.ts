import { Injectable, inject } from '@angular/core';
import { ApiClient } from '@core/api/api-client';
import type { TableRequestQuery } from '@shared/ui/table-model';

/**
 * 日曆同步的端點（`calendar-sync`）：狀態、日曆清單、事件與執行紀錄；立即同步與連線測試另需 `calendar-sync.manage`。
 * 事件與紀錄的 `calendarId` 空字串表示全部日曆。立即同步只排進背景工作就回應，結果靠輪詢狀態看。
 */
@Injectable({ providedIn: 'root' })
export class CalendarSyncApi {
  private readonly api = inject(ApiClient);
  status = (signal?: AbortSignal) => this.api.get('/api/v1/calendar-sync/status', { signal });
  calendars = (signal?: AbortSignal) => this.api.get('/api/v1/calendar-sync/calendars', { signal });
  events = (calendarId: string, table: TableRequestQuery, signal?: AbortSignal) =>
    this.api.get('/api/v1/calendar-sync/events', {
      query: { ...table, calendarId: calendarId || undefined },
      signal,
    });
  runs = (calendarId: string, table: TableRequestQuery, signal?: AbortSignal) =>
    this.api.get('/api/v1/calendar-sync/runs', {
      query: { ...table, calendarId: calendarId || undefined },
      signal,
    });
  /** `confirmRemovals`：確認執行被安全閥擋下的移除。 */
  start = (confirmRemovals: boolean) =>
    this.api.post('/api/v1/calendar-sync/runs', { body: { confirmRemovals } });
  testConnection = () => this.api.post('/api/v1/calendar-sync/connection-test');
}
