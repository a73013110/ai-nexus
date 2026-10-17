import { Injectable, inject } from '@angular/core';
import { ApiClient } from '@core/api/api-client';
import type {
  CreateRepositoryReviewRequest,
  ImportRepositoryFolderRequest,
  StartRepositoryChatRequest,
} from '@core/api/schema';

/** 路由所用的儲存庫形式：`owner/name` 拆成兩部分。 */
export interface RepositoryKey {
  owner: string;
  name: string;
}

@Injectable({ providedIn: 'root' })
export class RepositoriesApi {
  private readonly api = inject(ApiClient);
  status = () => this.api.get('/api/v1/repositories/connection');
  connect = (token: string) =>
    this.api.post('/api/v1/repositories/connection', { body: { token } });
  disconnect = () => this.api.delete('/api/v1/repositories/connection');
  list = (text: string, page: number) =>
    this.api.get('/api/v1/repositories', { query: { q: text, page } });
  review = (id: string) => this.api.get('/api/v1/repositories/reviews/{id}', { path: { id } });
  reviewJob = (id: string, retry: boolean) =>
    retry
      ? this.api.post('/api/v1/repositories/reviews/{id}/retry', { path: { id } })
      : this.api.post('/api/v1/repositories/reviews/{id}/cancel', { path: { id } });
  comment = (id: string, body: string) =>
    this.api.post('/api/v1/repositories/reviews/{id}/comment', { path: { id }, body: { body } });
  read = (path: RepositoryKey) => this.api.get('/api/v1/repositories/{owner}/{name}', { path });
  tree = (path: RepositoryKey, commit: string, folder: string) =>
    this.api.get('/api/v1/repositories/{owner}/{name}/tree', {
      path,
      query: { commit, path: folder },
    });
  file = (path: RepositoryKey, commit: string, file: string) =>
    this.api.get('/api/v1/repositories/{owner}/{name}/file', {
      path,
      query: { commit, path: file },
    });
  pin = (path: RepositoryKey) => this.api.put('/api/v1/repositories/{owner}/{name}/pin', { path });
  unpin = (path: RepositoryKey) =>
    this.api.delete('/api/v1/repositories/{owner}/{name}/pin', { path });
  commits = (path: RepositoryKey, ref: string, page: number) =>
    this.api.get('/api/v1/repositories/{owner}/{name}/commits', { path, query: { ref, page } });
  diff = (path: RepositoryKey, head: string, base: string | null) =>
    this.api.get('/api/v1/repositories/{owner}/{name}/diff', {
      path,
      query: base ? { head, base } : { head },
    });
  issues = (path: RepositoryKey, state: string, page: number, text2 = '') =>
    this.api.get('/api/v1/repositories/{owner}/{name}/issues', {
      path,
      query: text2 ? { state, page, q: text2 } : { state, page },
    });
  issue = (path: RepositoryKey, number: number) =>
    this.api.get('/api/v1/repositories/{owner}/{name}/issues/{number}', {
      path: { ...path, number },
    });
  pulls = (path: RepositoryKey, state: string, page: number) =>
    this.api.get('/api/v1/repositories/{owner}/{name}/pulls', { path, query: { state, page } });
  pull = (path: RepositoryKey, number: number) =>
    this.api.get('/api/v1/repositories/{owner}/{name}/pulls/{number}', {
      path: { ...path, number },
    });
  chat = (path: RepositoryKey, body: StartRepositoryChatRequest) =>
    this.api.post('/api/v1/repositories/{owner}/{name}/chat', { path, body });
  import = (path: RepositoryKey, body: ImportRepositoryFolderRequest) =>
    this.api.post('/api/v1/repositories/{owner}/{name}/imports', { path, body });
  reviews = (path: RepositoryKey) =>
    this.api.get('/api/v1/repositories/{owner}/{name}/reviews', { path });
  createReview = (path: RepositoryKey, body: CreateRepositoryReviewRequest) =>
    this.api.post('/api/v1/repositories/{owner}/{name}/reviews', { path, body });
}
