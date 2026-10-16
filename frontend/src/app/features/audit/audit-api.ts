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
}
