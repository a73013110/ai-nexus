import { Injectable, inject } from '@angular/core';
import { ApiClient } from '../../core/api/api-client';
import type { CreateRepositoryReviewRequest, RepositoryFileDto } from '../../core/api/schema';

@Injectable({ providedIn: 'root' })
export class RepositoriesApi {
  private readonly api = inject(ApiClient);
  commits = (repository: string) =>
    this.api.get('/api/v1/repositories/commits', { query: { repository } });
  reviews = (repository: string) =>
    this.api.get('/api/v1/repositories/reviews', { query: { repository } });
  review = (id: string) => this.api.get('/api/v1/repositories/reviews/{id}', { path: { id } });
  createReview = (body: CreateRepositoryReviewRequest) =>
    this.api.post('/api/v1/repositories/reviews', { body });
  reviewJob = (id: string, retry: boolean) =>
    retry
      ? this.api.post('/api/v1/repositories/reviews/{id}/retry', { path: { id } })
      : this.api.post('/api/v1/repositories/reviews/{id}/cancel', { path: { id } });
  status = () => this.api.get('/api/v1/repositories/connection');
  connect = (token: string) =>
    this.api.post('/api/v1/repositories/connection', { body: { token } });
  disconnect = () => this.api.delete('/api/v1/repositories/connection');
  list = (page = 1) => this.api.get('/api/v1/repositories', { query: { page } });
  tree = (repository: string, commit = '', path = '') =>
    this.api.get('/api/v1/repositories/tree', { query: { repository, commit, path } });
  file = (repository: string, commit: string, path: string) =>
    this.api.get('/api/v1/repositories/file', { query: { repository, commit, path } });
  issues = (repository: string) =>
    this.api.get('/api/v1/repositories/issues', { query: { repository } });
  import = (file: RepositoryFileDto, collectionId: string) =>
    this.api.post('/api/v1/repositories/import', {
      body: { repository: file.repository, commit: file.commit, path: file.path, collectionId },
    });
}
