import { Injectable, inject } from '@angular/core';
import { ApiTransport } from '../../core/api/api-transport';
import type {
  Collection,
  DocumentInfo,
  DocumentPage,
  DocumentJob,
  KnowledgeSearch,
} from '../../core/api/types';

@Injectable({ providedIn: 'root' })
export class KnowledgeApi {
  readonly http = inject(ApiTransport);
  collections = () => this.http.json<Collection[]>('/knowledge/collections');
  create = (name: string, description: string) =>
    this.http.json<Collection>('/knowledge/collections', 'POST', { name, description });
  update = (id: string, name: string, description: string) =>
    this.http.json<void>(`/knowledge/collections/${encodeURIComponent(id)}`, 'PUT', {
      name,
      description,
    });
  removeCollection = (id: string) =>
    this.http.json<void>(`/knowledge/collections/${encodeURIComponent(id)}`, 'DELETE');
  documents = (id: string) =>
    this.http.json<DocumentInfo[]>(`/knowledge/collections/${encodeURIComponent(id)}/documents`);
  add = (id: string, attachmentId: string) =>
    this.http.json<DocumentInfo>(
      `/knowledge/collections/${encodeURIComponent(id)}/documents`,
      'POST',
      { attachmentId },
    );
  readAttachment = (id: string, signal?: AbortSignal) =>
    this.http.json<DocumentInfo>(
      `/attachments/${encodeURIComponent(id)}/document`,
      'POST',
      undefined,
      undefined,
      signal,
    );
  document = (id: string, signal?: AbortSignal) =>
    this.http.json<DocumentInfo>(
      `/documents/${encodeURIComponent(id)}`,
      'GET',
      undefined,
      undefined,
      signal,
    );
  pages = (id: string) =>
    this.http.json<DocumentPage[]>(`/documents/${encodeURIComponent(id)}/pages`);
  job = (id: string) => this.http.json<DocumentJob>(`/documents/${encodeURIComponent(id)}/job`);
  original = (id: string, signal?: AbortSignal) =>
    this.http.response(
      `/documents/${encodeURIComponent(id)}/content`,
      'GET',
      undefined,
      undefined,
      signal,
    );
  remove = (id: string) => this.http.json<void>(`/documents/${encodeURIComponent(id)}`, 'DELETE');
  reindex = (id: string) =>
    this.http.json<DocumentInfo>(`/documents/${encodeURIComponent(id)}/reindex`, 'POST');
  search = (query: string, collectionIds: string[]) =>
    this.http.json<KnowledgeSearch>('/knowledge/search', 'POST', { query, collectionIds });
  selection = (id: string) =>
    this.http.json<{ collectionIds: string[] }>(
      `/conversations/${encodeURIComponent(id)}/knowledge`,
    );
  select = (id: string, collectionIds: string[]) =>
    this.http.json<void>(`/conversations/${encodeURIComponent(id)}/knowledge`, 'PUT', {
      collectionIds,
    });
}
