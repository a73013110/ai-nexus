import { Injectable, inject } from '@angular/core';
import { ApiClient } from '@core/api/api-client';
import type { ResourceAclRequest } from '@core/api/schema';

const accessPaths = {
  collection: '/api/v1/knowledge/collections/{id}/access',
  project: '/api/v1/projects/{id}/access',
  artifact: '/api/v1/artifacts/{id}/access',
  evaluationSet: '/api/v1/quality/sets/{id}/access',
} as const;
/** 成員與群組透過共用存取對話框管理的資源種類。 */
export type SharedResourceKind = keyof typeof accessPaths;

/** 集合、專案、評估集與已保存成品的共用存取管理。 */
@Injectable({ providedIn: 'root' })
export class CollaborationApi {
  private readonly api = inject(ApiClient);
  directory = (search: string) => this.api.get('/api/v1/directory', { query: { search } });
  groups = () => this.api.get('/api/v1/directory/groups');
  access = (kind: SharedResourceKind, id: string) =>
    this.api.get(accessPaths[kind], { path: { id } });
  saveAccess = (kind: SharedResourceKind, id: string, body: ResourceAclRequest) =>
    this.api.put(accessPaths[kind], { path: { id }, body });
}
