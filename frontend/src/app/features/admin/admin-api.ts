import { Injectable, inject } from '@angular/core';
import { ApiTransport } from '../../core/api/api-transport';
import type {
  AdminCatalog,
  AdminUsers,
  AdminUsage,
  Access,
  AuditEntry,
} from '../../core/api/types';
import type { components } from '../../core/api/schema';

@Injectable({ providedIn: 'root' })
export class AdminApi {
  private readonly http = inject(ApiTransport);
  catalog = () => this.http.json<AdminCatalog>('/admin/catalog');
  users = (search: string, offset = 0) =>
    this.http.json<AdminUsers>(
      `/admin/users?${new URLSearchParams({ search, offset: String(offset) })}`,
    );
  access = (id: string) => this.http.json<Access>(`/admin/users/${encodeURIComponent(id)}/access`);
  audit = (before?: number) =>
    this.http.json<AuditEntry[]>(`/admin/audit${before ? '?before=' + before : ''}`);
  usage = () => this.http.json<AdminUsage>('/admin/usage');
  roles = (id: string, roleIds: string[]) =>
    this.http.json<void>(`/admin/users/${encodeURIComponent(id)}/roles`, 'PUT', { roleIds });
  role = (id: string, body: components['schemas']['RoleUpdateRequest']) =>
    this.http.json<void>(`/admin/roles/${encodeURIComponent(id)}`, 'PUT', body);
  group = (id: string, body: components['schemas']['GroupUpdateRequest']) =>
    this.http.json<void>(`/admin/groups/${encodeURIComponent(id)}`, 'PUT', body);
  feature = (id: string, body: components['schemas']['FeatureUpdateRequest']) =>
    this.http.json<void>(`/admin/features/${encodeURIComponent(id)}`, 'PUT', body);
}
