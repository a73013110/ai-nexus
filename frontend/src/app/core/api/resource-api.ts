import { Injectable, inject } from '@angular/core';
import { ApiTransport } from './api-transport';
import type { DirectoryUser, DirectoryGroup, ResourceAcl, ResourceAclRequest } from './types';

/** Shared access management for collections, projects and saved artifacts. */
@Injectable({ providedIn: 'root' })
export class ResourceApi {
  private readonly http = inject(ApiTransport);
  directory = (search: string) =>
    this.http.json<DirectoryUser[]>(`/directory?${new URLSearchParams({ search })}`);
  groups = () => this.http.json<DirectoryGroup[]>('/directory/groups');
  access = (path: string) => this.http.json<ResourceAcl>(path);
  saveAccess = (path: string, value: ResourceAclRequest) =>
    this.http.json<void>(path, 'PUT', value);
}
