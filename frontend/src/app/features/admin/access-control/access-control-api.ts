import { Injectable, inject } from '@angular/core';
import { ApiClient } from '@core/api/api-client';
import type {
  FeatureUpdateRequest,
  GroupUpdateRequest,
  NavigationGroupCreateRequest,
  NavigationGroupUpdateRequest,
  RoleUpdateRequest,
} from '@core/api/schema';

/**
 * 存取目錄與角色、功能群組、功能、選單分群的寫入。目錄也供使用者頁（`identity.users`）讀取，
 * 所以使用者頁單向引用這裡；使用者本身的端點在 `IdentityApi`。
 */
@Injectable({ providedIn: 'root' })
export class AccessControlApi {
  private readonly api = inject(ApiClient);
  catalog = () => this.api.get('/api/v1/admin/catalog');
  role = (id: string, body: RoleUpdateRequest) =>
    this.api.put('/api/v1/admin/roles/{id}', { path: { id }, body });
  group = (id: string, body: GroupUpdateRequest) =>
    this.api.put('/api/v1/admin/groups/{id}', { path: { id }, body });
  feature = (id: string, body: FeatureUpdateRequest) =>
    this.api.put('/api/v1/admin/features/{id}', { path: { id }, body });
  createNavigationGroup = (body: NavigationGroupCreateRequest) =>
    this.api.post('/api/v1/admin/navigation-groups', { body });
  updateNavigationGroup = (id: string, body: NavigationGroupUpdateRequest) =>
    this.api.put('/api/v1/admin/navigation-groups/{id}', { path: { id }, body });
  deleteNavigationGroup = (id: string) =>
    this.api.delete('/api/v1/admin/navigation-groups/{id}', { path: { id } });
}
