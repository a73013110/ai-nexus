import { Injectable, inject } from '@angular/core';
import { ApiTransport } from '../../core/api/api-transport';
import type {
  RepositoryCommit,
  RepositoryReview,
  RepositoryReviewDetail,
  Job,
  RepositoryStatus,
  RepositoryPage,
  RepositoryTree,
  RepositoryFile,
  RepositoryIssue,
  DocumentInfo,
} from '../../core/api/types';

@Injectable({ providedIn: 'root' })
export class RepositoriesApi {
  private readonly http = inject(ApiTransport);
  commits = (repository: string) =>
    this.http.json<RepositoryCommit[]>(
      `/repositories/commits?${new URLSearchParams({ repository })}`,
    );
  reviews = (repository: string) =>
    this.http.json<RepositoryReview[]>(
      `/repositories/reviews?${new URLSearchParams({ repository })}`,
    );
  review = (id: string) =>
    this.http.json<RepositoryReviewDetail>(`/repositories/reviews/${encodeURIComponent(id)}`);
  createReview = (request: {
    repository: string;
    commit: string;
    baseCommit: string | null;
    modelId: string;
    note: string;
    idempotencyKey: string;
  }) => this.http.json<RepositoryReview>('/repositories/reviews', 'POST', request);
  reviewJob = (id: string, retry: boolean) =>
    this.http.json<Job>(
      `/repositories/reviews/${encodeURIComponent(id)}/${retry ? 'retry' : 'cancel'}`,
      'POST',
    );
  status = () => this.http.json<RepositoryStatus>('/repositories/connection');
  connect = (token: string) =>
    this.http.json<RepositoryStatus>('/repositories/connection', 'POST', { token });
  disconnect = () => this.http.json<void>('/repositories/connection', 'DELETE');
  list = (page = 1) => this.http.json<RepositoryPage>(`/repositories?page=${page}`);
  tree = (repository: string, commit = '', path = '') =>
    this.http.json<RepositoryTree>(
      `/repositories/tree?${new URLSearchParams({ repository, commit, path })}`,
    );
  file = (repository: string, commit: string, path: string) =>
    this.http.json<RepositoryFile>(
      `/repositories/file?${new URLSearchParams({ repository, commit, path })}`,
    );
  issues = (repository: string) =>
    this.http.json<RepositoryIssue[]>(
      `/repositories/issues?${new URLSearchParams({ repository })}`,
    );
  import = (file: RepositoryFile, collectionId: string) =>
    this.http.json<DocumentInfo>('/repositories/import', 'POST', {
      repository: file.repository,
      commit: file.commit,
      path: file.path,
      collectionId,
    });
}
