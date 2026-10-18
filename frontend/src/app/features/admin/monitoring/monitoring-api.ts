import { inject, Injectable } from '@angular/core';
import { ApiClient } from '@core/api/api-client';

/** 監控主控台的即時執行環境快照與其匯出。 */
@Injectable({ providedIn: 'root' })
export class MonitoringApi {
  private readonly api = inject(ApiClient);
  events = (minutes: number, signal: AbortSignal) =>
    this.api.open('/api/v1/admin/monitoring/events', {
      query: { minutes },
      headers: { Accept: 'text/event-stream' },
      signal,
    });
  export = (minutes: number) =>
    this.api.open('/api/v1/admin/monitoring/export', { query: { minutes } });
}
