import { Injectable, inject } from '@angular/core';
import { ApiClient, type ApiQuery } from '../../core/api/api-client';

export type AuditFilters = Omit<ApiQuery<'/api/v1/admin/audit', 'get'>, 'before'>;

@Injectable({ providedIn: 'root' })
export class ActivityAuditApi {
  private readonly api = inject(ApiClient);
  catalog = () => this.api.get('/api/v1/admin/audit/catalog');
  query = (before: number | undefined, filters: AuditFilters) =>
    this.api.get('/api/v1/admin/audit', { query: { ...filters, before } });
}
