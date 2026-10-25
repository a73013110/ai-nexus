import { Injectable, inject } from '@angular/core';
import { ApiClient } from '@core/api/api-client';
import type {
  DirectoryImportRequest,
  ModelPolicyRequest,
  UserAccountRequest,
} from '@core/api/schema';

/**
 * 使用者頁（`identity.users`）的端點：帳號、通知信箱、角色指派、附件容量、個人模型政策、帳號待辦、洞察與稽核式讀取對話。
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
  /** `null` 清除；AD 提供的信箱由後端拒絕。 */
  email = (id: string, email: string | null) =>
    this.api.put('/api/v1/admin/users/{id}/email', { path: { id }, body: { email } });
  roles = (id: string, roleIds: string[]) =>
    this.api.put('/api/v1/admin/users/{id}/roles', { path: { id }, body: { roleIds } });
  createUser = (body: UserAccountRequest) => this.api.post('/api/v1/admin/users', { body });
  updateUser = (id: string, body: UserAccountRequest) =>
    this.api.put('/api/v1/admin/users/{id}', { path: { id }, body });
  deleteUser = (id: string) => this.api.delete('/api/v1/admin/users/{id}', { path: { id } });
  /**
   * 使用者順序（出勤紀錄等依人分組的列表）：移到整份名單第 `position` 位（從 1 起算，值由伺服器計算），或清除全部。
   */
  moveUser = (id: string, position: number) =>
    this.api.put('/api/v1/admin/users/{id}/position', { path: { id }, body: { position } });
  resetUserOrder = () => this.api.delete('/api/v1/admin/users/order');
  /** 依 AD 短帳號查 SID 後改綁（ADR 0019）；解除後回到預建，下次以同名 AD 帳號驗證或匯入時綁定。 */
  rebindDirectory = (id: string, adAccount: string) =>
    this.api.put('/api/v1/admin/users/{id}/ad-binding', { path: { id }, body: { adAccount } });
  unbindDirectory = (id: string) =>
    this.api.delete('/api/v1/admin/users/{id}/ad-binding', { path: { id } });
  /** 瀏覽 AD 與匯入需要 `identity.users.import`；從 AD 更新需要 `identity.users.sync`。未指定 `parent` 時是搜尋根。 */
  directoryNodes = (parent: string | undefined, signal?: AbortSignal) =>
    this.api.get('/api/v1/admin/directory-nodes', { query: { parent }, signal });
  directoryPeople = (node: string, subtree: boolean, q: string, signal?: AbortSignal) =>
    this.api.get('/api/v1/admin/directory-people', {
      query: { node, subtree, q: q || undefined },
      signal,
    });
  directoryPerson = (sid: string, signal?: AbortSignal) =>
    this.api.get('/api/v1/admin/directory-people/{sid}', { path: { sid }, signal });
  previewDirectoryImport = (body: DirectoryImportRequest) =>
    this.api.post('/api/v1/admin/directory-imports/preview', { body });
  importDirectoryUsers = (body: DirectoryImportRequest) =>
    this.api.post('/api/v1/admin/directory-imports', { body });
  /** 各模組列出的帳號待辦（ADR 0019）；每次重新查詢，處理完的項目自然消失。 */
  accountReviews = () => this.api.get('/api/v1/admin/account-reviews');
  syncDirectoryUsers = (userIds: string[]) =>
    this.api.post('/api/v1/admin/directory-syncs', { body: { userIds } });
}
