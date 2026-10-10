import { Injectable, inject } from '@angular/core';
import { ApiClient } from '../../core/api/api-client';

@Injectable({ providedIn: 'root' })
export class ArtifactsApi {
  private readonly api = inject(ApiClient);
  list = () => this.api.get('/api/v1/artifacts');
  get = (id: string, version?: number) =>
    this.api.get('/api/v1/artifacts/{id}', {
      path: { id },
      query: { version: version || undefined },
    });
  create = (
    title: string,
    content: string,
    sourceMessageId: string | null = null,
    projectId: string | null = null,
  ) => this.api.post('/api/v1/artifacts', { body: { title, content, sourceMessageId, projectId } });
  save = (id: string, title: string, content: string, expectedVersion: number) =>
    this.api.put('/api/v1/artifacts/{id}', {
      path: { id },
      body: { title, content, expectedVersion },
    });
  versions = (id: string) => this.api.get('/api/v1/artifacts/{id}/versions', { path: { id } });
  remove = (id: string) => this.api.delete('/api/v1/artifacts/{id}', { path: { id } });
  transform = (
    text: string,
    action: string,
    modelId: string | null,
    language: string,
    signal: AbortSignal,
  ) =>
    this.api.post('/api/v1/text/transform', { body: { text, action, modelId, language }, signal });
  export = (id: string, format: string, version: number, signal?: AbortSignal) =>
    this.api.open('/api/v1/artifacts/{id}/export/{format}', {
      path: { id, format },
      query: { version },
      signal,
    });
}
