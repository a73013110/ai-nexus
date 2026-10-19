import { Injectable, inject } from '@angular/core';
import { ApiClient } from '@core/api/api-client';
import type { AdminRetrievalSearchRequest } from '@core/api/schema';

@Injectable({ providedIn: 'root' })
export class KnowledgeApi {
  private readonly api = inject(ApiClient);
  collections = () => this.api.get('/api/v1/knowledge/collections');
  create = (name: string, description: string) =>
    this.api.post('/api/v1/knowledge/collections', { body: { name, description } });
  update = (id: string, name: string, description: string) =>
    this.api.put('/api/v1/knowledge/collections/{id}', {
      path: { id },
      body: { name, description },
    });
  removeCollection = (id: string) =>
    this.api.delete('/api/v1/knowledge/collections/{id}', { path: { id } });
  documents = (id: string) =>
    this.api.get('/api/v1/knowledge/collections/{id}/documents', { path: { id } });
  add = (id: string, attachmentId: string) =>
    this.api.post('/api/v1/knowledge/collections/{id}/documents', {
      path: { id },
      body: { attachmentId },
    });
  text = (id: string) => this.api.get('/api/v1/documents/{id}/text', { path: { id } });
  createText = (collection: string, title: string, text: string) =>
    this.api.post('/api/v1/knowledge/collections/{id}/text', {
      path: { id: collection },
      body: { title, text },
    });
  updateText = (id: string, title: string, text: string, expectedVersion: number) =>
    this.api.put('/api/v1/documents/{id}/text', {
      path: { id },
      body: { title, text, expectedVersion },
    });
  readAttachment = (id: string, signal?: AbortSignal) =>
    this.api.post('/api/v1/attachments/{id}/document', { path: { id }, signal });
  document = (id: string, signal?: AbortSignal) =>
    this.api.get('/api/v1/documents/{id}', { path: { id }, signal });
  pages = (id: string) => this.api.get('/api/v1/documents/{id}/pages', { path: { id } });
  job = (id: string) => this.api.get('/api/v1/documents/{id}/job', { path: { id } });
  original = (id: string, signal?: AbortSignal) =>
    this.api.open('/api/v1/documents/{id}/content', { path: { id }, signal });
  remove = (id: string) => this.api.delete('/api/v1/documents/{id}', { path: { id } });
  reindex = (id: string) => this.api.post('/api/v1/documents/{id}/reindex', { path: { id } });
  search = (query: string, collectionIds: string[]) =>
    this.api.post('/api/v1/knowledge/search', { body: { query, collectionIds } });
  selection = (id: string) =>
    this.api.get('/api/v1/conversations/{id}/knowledge', { path: { id } });
  select = (id: string, collectionIds: string[]) =>
    this.api.put('/api/v1/conversations/{id}/knowledge', {
      path: { id },
      body: { collectionIds },
    });
  // 檢索管理頁（`knowledge.retrieval`）：嵌入設定檔、檢索能力與管理端試查。
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
}
