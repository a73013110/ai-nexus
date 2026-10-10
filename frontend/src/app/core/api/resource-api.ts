import { Injectable, inject } from '@angular/core';
import { ApiClient } from './api-client';
import type { ResourceAclRequest } from './schema';

const accessPaths = {
  collection: '/api/v1/knowledge/collections/{id}/access',
  project: '/api/v1/projects/{id}/access',
  artifact: '/api/v1/artifacts/{id}/access',
  evaluationSet: '/api/v1/quality/sets/{id}/access',
} as const;
/** Resource kinds whose members and groups are managed through the shared access dialog. */
export type SharedResourceKind = keyof typeof accessPaths;

/** Shared access management for collections, projects, evaluation sets and saved artifacts. */
@Injectable({ providedIn: 'root' })
export class ResourceApi {
  private readonly api = inject(ApiClient);
  directory = (search: string) => this.api.get('/api/v1/directory', { query: { search } });
  groups = () => this.api.get('/api/v1/directory/groups');
  access = (kind: SharedResourceKind, id: string) =>
    this.api.get(accessPaths[kind], { path: { id } });
  saveAccess = (kind: SharedResourceKind, id: string, body: ResourceAclRequest) =>
    this.api.put(accessPaths[kind], { path: { id }, body });
}
