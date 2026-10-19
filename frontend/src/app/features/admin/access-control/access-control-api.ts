import { Injectable, inject } from '@angular/core';
import { ApiClient } from '@core/api/api-client';
import type {
  FeatureUpdateRequest,
  GroupUpdateRequest,
  NavigationItemCreateRequest,
  NavigationItemUpdateRequest,
  NavigationOrderRequest,
  RoleUpdateRequest,
} from '@core/api/schema';

/**
 * 存取目錄與角色、功能群組、功能、選單項目的讀寫。目錄也供使用者頁（`identity.users`）讀取，
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
  navigationItems = () => this.api.get('/api/v1/admin/navigation-items');
  createNavigationItem = (body: NavigationItemCreateRequest) =>
    this.api.post('/api/v1/admin/navigation-items', { body });
  updateNavigationItem = (id: string, body: NavigationItemUpdateRequest) =>
    this.api.put('/api/v1/admin/navigation-items/{id}', { path: { id }, body });
  orderNavigationItems = (body: NavigationOrderRequest) =>
    this.api.put('/api/v1/admin/navigation-items/order', { body });
  deleteNavigationItem = (id: string) =>
    this.api.delete('/api/v1/admin/navigation-items/{id}', { path: { id } });
}
