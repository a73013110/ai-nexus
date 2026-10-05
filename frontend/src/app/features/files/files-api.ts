import { Injectable, inject } from '@angular/core';
import { ApiTransport } from '../../core/api/api-transport';
import type { FileLibraryPage } from '../../core/api/types';

export interface FileFilters {
  search: string;
  type: string;
  source: string;
  offset: number;
  limit?: number;
}
@Injectable({ providedIn: 'root' })
export class FilesApi {
  private readonly http = inject(ApiTransport);
  list(filters: FileFilters, signal?: AbortSignal) {
    const query = new URLSearchParams({
      search: filters.search,
      type: filters.type,
      source: filters.source,
      offset: String(filters.offset),
      limit: String(filters.limit ?? 40),
    });
    return this.http.json<FileLibraryPage>(`/files?${query}`, 'GET', undefined, undefined, signal);
  }
  retain = (id: string) => this.http.json<void>(`/files/${encodeURIComponent(id)}/retain`, 'POST');
  remove = (id: string) => this.http.json<void>(`/files/${encodeURIComponent(id)}`, 'DELETE');
}
