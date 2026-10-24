import { Injectable, inject } from '@angular/core';
import { ApiClient } from '@core/api/api-client';
import type {
  UnitCreateRequest,
  UnitMemberRequest,
  UnitOrderRequest,
  UnitUpdateRequest,
} from '@core/api/schema';

/** 組織單位與成員的讀寫、從公文系統匯入；使用者的所屬單位也供使用者頁（`identity.users`）讀取。 */
@Injectable({ providedIn: 'root' })
export class OrganizationApi {
  private readonly api = inject(ApiClient);
  units = () => this.api.get('/api/v1/admin/organization-units');
  createUnit = (body: UnitCreateRequest) =>
    this.api.post('/api/v1/admin/organization-units', { body });
  updateUnit = (id: string, body: UnitUpdateRequest) =>
    this.api.put('/api/v1/admin/organization-units/{id}', { path: { id }, body });
  orderUnits = (body: UnitOrderRequest) =>
    this.api.put('/api/v1/admin/organization-units/order', { body });
  deleteUnit = (id: string) =>
    this.api.delete('/api/v1/admin/organization-units/{id}', { path: { id } });
  members = (id: string) =>
    this.api.get('/api/v1/admin/organization-units/{id}/members', { path: { id } });
  saveMember = (id: string, userId: string, body: UnitMemberRequest) =>
    this.api.put('/api/v1/admin/organization-units/{id}/members/{userId}', {
      path: { id, userId },
      body,
    });
  /** 加入成員時可挑選的在職使用者與他們的主要所屬。 */
  candidates = () => this.api.get('/api/v1/admin/organization-units/candidates');
  removeMember = (id: string, userId: string) =>
    this.api.delete('/api/v1/admin/organization-units/{id}/members/{userId}', {
      path: { id, userId },
    });
  /** 從公文系統匯入的預覽（不寫入）。 */
  previewImport = () => this.api.get('/api/v1/admin/organization-units/import');
  /**
   * 套用預覽，`excluded` 的部門（`gdDepNo`）這次不動；`fingerprint` 與伺服器重算的完整預覽不同時回 `organization_import_stale`。
   */
  applyImport = (fingerprint: string, excluded: number[]) =>
    this.api.post('/api/v1/admin/organization-units/import', { body: { fingerprint, excluded } });
  userUnits = (id: string) =>
    this.api.get('/api/v1/admin/users/{id}/organization-units', { path: { id } });
}
