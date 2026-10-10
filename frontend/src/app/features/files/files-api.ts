import { Injectable, inject } from '@angular/core';
import { ApiClient, type ApiQuery } from '../../core/api/api-client';

export type FileFilters = ApiQuery<'/api/v1/files', 'get'>;

@Injectable({ providedIn: 'root' })
export class FilesApi {
  private readonly api = inject(ApiClient);
  list = (filters: FileFilters, signal?: AbortSignal) =>
    this.api.get('/api/v1/files', { query: { limit: 40, ...filters }, signal });
  rename = (id: string, fileName: string, expectedFileName: string) =>
    this.api.put('/api/v1/files/{id}/name', { path: { id }, body: { fileName, expectedFileName } });
  retain = (id: string) => this.api.post('/api/v1/files/{id}/retain', { path: { id } });
  remove = (id: string) => this.api.delete('/api/v1/files/{id}', { path: { id } });
}
