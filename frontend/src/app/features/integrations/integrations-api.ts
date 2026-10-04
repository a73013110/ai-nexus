import { Injectable, inject } from '@angular/core';
import { ApiTransport } from '../../core/api/api-transport';
import type {
  ArtifactDocument,
  ExternalSource,
  SourceRecord,
  SourceDetail,
  SourceChat,
} from '../../core/api/types';

@Injectable({ providedIn: 'root' })
export class IntegrationsApi {
  private readonly http = inject(ApiTransport);
  list = () => this.http.json<ExternalSource[]>('/integrations');
  search = (source: string, query: string, kind: string) =>
    this.http.json<SourceRecord[]>(
      `/integrations/${encodeURIComponent(source)}/records?${new URLSearchParams({ query, kind })}`,
    );
  get = (source: string, id: string) =>
    this.http.json<SourceDetail>(
      `/integrations/${encodeURIComponent(source)}/record?${new URLSearchParams({ id })}`,
    );
  import = (source: string, recordId: string, expectedRevision: string) =>
    this.http.json<ArtifactDocument>(`/integrations/${encodeURIComponent(source)}/import`, 'POST', {
      recordId,
      expectedRevision,
    });
  chat = (source: string, recordId: string, expectedRevision: string) =>
    this.http.json<SourceChat>(`/integrations/${encodeURIComponent(source)}/chat`, 'POST', {
      recordId,
      expectedRevision,
    });
}
