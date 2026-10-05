import { Injectable, inject } from '@angular/core';
import { ApiTransport } from '../../core/api/api-transport';
import type {
  AdminCatalog,
  AdminUsers,
  AdminUsage,
  Access,
  AuditEntry,
  AdminUserDetail,
  AdminConversationPage,
  AdminConversationDetail,
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
  audit = (before?: number, filters: Record<string, string> = {}) => {
    const query = new URLSearchParams(Object.entries(filters).filter(([, value]) => value));
    if (before) query.set('before', String(before));
    return this.http.json<AuditEntry[]>(`/admin/audit?${query}`);
  };
  insights = (id: string) =>
    this.http.json<AdminUserDetail>(`/admin/users/${encodeURIComponent(id)}/insights`);
  conversations = (id: string, search: string, includeDeleted: boolean, offset = 0) =>
    this.http.json<AdminConversationPage>(
      `/admin/users/${encodeURIComponent(id)}/conversations?${new URLSearchParams({ search, includeDeleted: String(includeDeleted), offset: String(offset) })}`,
    );
  conversation = (id: string, offset = 0) =>
    this.http.json<AdminConversationDetail>(
      `/admin/conversations/${encodeURIComponent(id)}?offset=${offset}`,
    );
  usage = () => this.http.json<AdminUsage>('/admin/usage');
  storage = (id: string, limitBytes: number | null) =>
    this.http.json<void>(`/admin/users/${encodeURIComponent(id)}/storage`, 'PUT', { limitBytes });
  roles = (id: string, roleIds: string[]) =>
    this.http.json<void>(`/admin/users/${encodeURIComponent(id)}/roles`, 'PUT', { roleIds });
  saveUser = (id: string | null, body: components['schemas']['UserAccountRequest']) =>
    this.http.json<void | { id: string }>(
      id ? `/admin/users/${encodeURIComponent(id)}` : '/admin/users',
      id ? 'PUT' : 'POST',
      body,
    );
  deleteUser = (id: string) =>
    this.http.json<void>(`/admin/users/${encodeURIComponent(id)}`, 'DELETE');
  role = (id: string, body: components['schemas']['RoleUpdateRequest']) =>
    this.http.json<void>(`/admin/roles/${encodeURIComponent(id)}`, 'PUT', body);
  group = (id: string, body: components['schemas']['GroupUpdateRequest']) =>
    this.http.json<void>(`/admin/groups/${encodeURIComponent(id)}`, 'PUT', body);
  feature = (id: string, body: components['schemas']['FeatureUpdateRequest']) =>
    this.http.json<void>(`/admin/features/${encodeURIComponent(id)}`, 'PUT', body);
}
