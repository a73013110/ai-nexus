import { Injectable, inject } from '@angular/core';
import { ApiClient } from '../../core/api/api-client';
import type {
  AdminRetrievalSearchRequest,
  FeatureUpdateRequest,
  GroupUpdateRequest,
  ModelPolicyRequest,
  RoleUpdateRequest,
  UserAccountRequest,
} from '../../core/api/schema';

@Injectable({ providedIn: 'root' })
export class AdminApi {
  private readonly api = inject(ApiClient);
  catalog = () => this.api.get('/api/v1/admin/catalog');
  users = (search: string, offset = 0) =>
    this.api.get('/api/v1/admin/users', { query: { search, offset } });
  access = (id: string) => this.api.get('/api/v1/admin/users/{id}/access', { path: { id } });
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
  embeddingProfiles = () => this.api.get('/api/v1/admin/knowledge/profiles');
  rebuildProfile = (id: number) =>
    this.api.post('/api/v1/admin/knowledge/profiles/{id}/rebuild', { path: { id } });
  activateProfile = (id: number) =>
    this.api.post('/api/v1/admin/knowledge/profiles/{id}/activate', { path: { id } });
  clearProfileVectors = (id: number) =>
    this.api.delete('/api/v1/admin/knowledge/profiles/{id}/vectors', { path: { id } });
  retrievalCapabilities = () => this.api.get('/api/v1/admin/knowledge/capabilities');
  probeRetrieval = () => this.api.post('/api/v1/admin/knowledge/capabilities/probe');
  searchRetrieval = (body: AdminRetrievalSearchRequest) =>
    this.api.post('/api/v1/admin/knowledge/search', { body });
  usage = () => this.api.get('/api/v1/admin/usage');
  storage = (id: string, limitBytes: number | null) =>
    this.api.put('/api/v1/admin/users/{id}/storage', { path: { id }, body: { limitBytes } });
  roles = (id: string, roleIds: string[]) =>
    this.api.put('/api/v1/admin/users/{id}/roles', { path: { id }, body: { roleIds } });
  createUser = (body: UserAccountRequest) => this.api.post('/api/v1/admin/users', { body });
  updateUser = (id: string, body: UserAccountRequest) =>
    this.api.put('/api/v1/admin/users/{id}', { path: { id }, body });
  deleteUser = (id: string) => this.api.delete('/api/v1/admin/users/{id}', { path: { id } });
  role = (id: string, body: RoleUpdateRequest) =>
    this.api.put('/api/v1/admin/roles/{id}', { path: { id }, body });
  group = (id: string, body: GroupUpdateRequest) =>
    this.api.put('/api/v1/admin/groups/{id}', { path: { id }, body });
  feature = (id: string, body: FeatureUpdateRequest) =>
    this.api.put('/api/v1/admin/features/{id}', { path: { id }, body });
}
