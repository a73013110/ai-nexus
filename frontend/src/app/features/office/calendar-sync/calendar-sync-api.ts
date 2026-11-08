import { Injectable, inject } from '@angular/core';
import { ApiClient } from '@core/api/api-client';
import type { TableRequestQuery } from '@shared/ui/table-model';

/**
 * 日曆同步的端點（`calendar-sync`）：狀態、日曆清單、事件與執行紀錄；立即同步與連線測試另需 `calendar-sync.manage`。
 * 事件與紀錄的 `calendarId` 空字串表示全部日曆。立即同步與重設只排進背景工作就回應，結果靠輪詢狀態看。
 */
@Injectable({ providedIn: 'root' })
export class CalendarSyncApi {
  private readonly api = inject(ApiClient);
  status = (signal?: AbortSignal) => this.api.get('/api/v1/calendar-sync/status', { signal });
  calendars = (signal?: AbortSignal) => this.api.get('/api/v1/calendar-sync/calendars', { signal });
  /** `history`：連同期間窗外的舊事件與已移除的列一起列出；預設只列同步還在管的事件。 */
  events = (
    { calendarId, history }: { calendarId: string; history: boolean },
    table: TableRequestQuery,
    signal?: AbortSignal,
  ) =>
    this.api.get('/api/v1/calendar-sync/events', {
      query: { ...table, calendarId: calendarId || undefined, history: history || undefined },
      signal,
    });
  runs = (calendarId: string, table: TableRequestQuery, signal?: AbortSignal) =>
    this.api.get('/api/v1/calendar-sync/runs', {
      query: { ...table, calendarId: calendarId || undefined },
      signal,
    });
  runItems = (runId: string, table: TableRequestQuery, signal?: AbortSignal) =>
    this.api.get('/api/v1/calendar-sync/runs/{runId}/items', {
      path: { runId },
      query: { ...table },
      signal,
    });
  /** `confirmRemovals`：確認執行被安全閥擋下的移除。 */
  start = (confirmRemovals: boolean) =>
    this.api.post('/api/v1/calendar-sync/runs', { body: { confirmRemovals } });
  testConnection = () => this.api.post('/api/v1/calendar-sync/connection-test');
  /** 重設本站設定的日曆；`calendarId` 是畫面上確認過的日曆，與伺服器設定不同時拒絕。 */
  reset = (calendarId: string) =>
    this.api.post('/api/v1/calendar-sync/reset', { body: { calendarId } });
}
