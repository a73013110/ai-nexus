import { Injectable, inject } from '@angular/core';
import { ApiClient, type ApiQuery } from '@core/api/api-client';

/** 已套用的查詢條件；分頁參數（`before`、`take`）每頁另外加上。 */
export type AuditFilters = ApiQuery<'/api/v1/admin/audit/export', 'get'>;

@Injectable({ providedIn: 'root' })
export class AuditApi {
  private readonly api = inject(ApiClient);
  catalog = () => this.api.get('/api/v1/admin/audit/catalog');
  query = (filters: AuditFilters, take: number, before?: number, signal?: AbortSignal) =>
    this.api.get('/api/v1/admin/audit', { query: { ...filters, take, before }, signal });
  export = (filters: AuditFilters) =>
    this.api.open('/api/v1/admin/audit/export', { query: filters });
  /** 期間內各頁面功能的用量；動作功能已併入所屬頁面。 */
  usage = (from: string, until: string) =>
    this.api.get('/api/v1/admin/audit/usage', { query: { from, until } });
  /** 單一頁面的每日人數與次數；`offsetMinutes` 決定以哪個時區分日。 */
  usageDetail = (featureId: string, from: string, until: string, offsetMinutes: number) =>
    this.api.get('/api/v1/admin/audit/usage/{featureId}', {
      path: { featureId },
      query: { from, until, offsetMinutes },
    });
}
