import { Injectable, inject } from '@angular/core';
import { ApiClient, type ApiQuery } from '@core/api/api-client';

/** The applied conditions of a read; paging (`before`, `take`) is added per page. */
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
