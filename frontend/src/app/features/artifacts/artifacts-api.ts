import { Injectable, inject } from '@angular/core';
import { ApiTransport } from '../../core/api/api-transport';
import type {
  ArtifactDocument,
  ArtifactSummary,
  ArtifactRevision,
  TransformResult,
} from '../../core/api/types';

@Injectable({ providedIn: 'root' })
export class ArtifactsApi {
  private readonly http = inject(ApiTransport);
  list = () => this.http.json<ArtifactSummary[]>('/artifacts');
  get = (id: string, version?: number) =>
    this.http.json<ArtifactDocument>(
      `/artifacts/${encodeURIComponent(id)}${version ? '?version=' + version : ''}`,
    );
  create = (title: string, content: string, sourceMessageId: string | null = null, projectId: string | null = null) =>
    this.http.json<ArtifactDocument>('/artifacts', 'POST', { title, content, sourceMessageId, projectId });
  save = (id: string, title: string, content: string, expectedVersion: number) =>
    this.http.json<ArtifactDocument>(`/artifacts/${encodeURIComponent(id)}`, 'PUT', {
      title,
      content,
      expectedVersion,
    });
  versions = (id: string) =>
    this.http.json<ArtifactRevision[]>(`/artifacts/${encodeURIComponent(id)}/versions`);
  remove = (id: string) => this.http.json<void>(`/artifacts/${encodeURIComponent(id)}`, 'DELETE');
  transform = (
    text: string,
    action: string,
    modelId: string | null,
    language: string,
    signal: AbortSignal,
  ) =>
    this.http.json<TransformResult>(
      '/text/transform',
      'POST',
      { text, action, modelId, language },
      undefined,
      signal,
    );
  export = (id: string, format: string, version: number, signal?: AbortSignal) =>
    this.http.response(
      `/artifacts/${encodeURIComponent(id)}/export/${encodeURIComponent(format)}?version=${version}`,
      'GET',
      undefined,
      undefined,
      signal,
    );
}
