import { Injectable, inject } from '@angular/core';
import { ApiTransport } from '../../core/api/api-transport';
import type { AuditCatalog, AuditEntry } from '../../core/api/types';

@Injectable({ providedIn: 'root' })
export class ActivityAuditApi {
  private readonly http = inject(ApiTransport);
  catalog = () => this.http.json<AuditCatalog>('/admin/audit/catalog');
  query(before?: number, filters: Record<string, string> = {}) {
    const query = new URLSearchParams(Object.entries(filters).filter(([, value]) => value));
    if (before) query.set('before', String(before));
    return this.http.json<AuditEntry[]>(`/admin/audit?${query}`);
  }
}
