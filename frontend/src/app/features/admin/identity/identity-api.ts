import { Injectable, inject } from '@angular/core';
import { ApiClient } from '@core/api/api-client';
import type { ModelPolicyRequest, UserAccountRequest } from '@core/api/schema';

/**
 * 使用者頁（`identity.users`）的端點：帳號、角色指派、附件容量、個人模型政策、洞察與稽核式讀取對話。
 * 後端分散在 Identity、Operations、Inference、Files、Conversations，授權都是使用者頁，所以集中在這裡。
 */
@Injectable({ providedIn: 'root' })
export class IdentityApi {
  private readonly api = inject(ApiClient);
  users = (search: string, offset = 0) =>
    this.api.get('/api/v1/admin/users', { query: { search, offset } });
  insights = (id: string) => this.api.get('/api/v1/admin/users/{id}/insights', { path: { id } });
  conversations = (id: string, search: string, includeDeleted: boolean, offset = 0) =>
    this.api.get('/api/v1/admin/users/{id}/conversations', {
      path: { id },
      query: { search, includeDeleted, offset },
    });
  conversation = (id: string, offset = 0) =>
    this.api.get('/api/v1/admin/conversations/{id}', { path: { id }, query: { offset } });
  modelPolicy = (id: string) =>
    this.api.get('/api/v1/admin/users/{id}/model-policy', { path: { id } });
  saveModelPolicy = (id: string, body: ModelPolicyRequest) =>
    this.api.put('/api/v1/admin/users/{id}/model-policy', { path: { id }, body });
  storage = (id: string, limitBytes: number | null) =>
    this.api.put('/api/v1/admin/users/{id}/storage', { path: { id }, body: { limitBytes } });
  roles = (id: string, roleIds: string[]) =>
    this.api.put('/api/v1/admin/users/{id}/roles', { path: { id }, body: { roleIds } });
  createUser = (body: UserAccountRequest) => this.api.post('/api/v1/admin/users', { body });
  updateUser = (id: string, body: UserAccountRequest) =>
    this.api.put('/api/v1/admin/users/{id}', { path: { id }, body });
  deleteUser = (id: string) => this.api.delete('/api/v1/admin/users/{id}', { path: { id } });
}
