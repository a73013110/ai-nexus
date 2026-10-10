import { Injectable, inject } from '@angular/core';
import { ApiClient } from '../../core/api/api-client';

@Injectable({ providedIn: 'root' })
export class IntegrationsApi {
  private readonly api = inject(ApiClient);
  list = () => this.api.get('/api/v1/integrations');
  search = (source: string, query: string, kind: string) =>
    this.api.get('/api/v1/integrations/{source}/records', {
      path: { source },
      query: { query, kind },
    });
  get = (source: string, id: string) =>
    this.api.get('/api/v1/integrations/{source}/record', { path: { source }, query: { id } });
  import = (source: string, recordId: string, expectedRevision: string) =>
    this.api.post('/api/v1/integrations/{source}/import', {
      path: { source },
      body: { recordId, expectedRevision },
    });
  chat = (source: string, recordId: string, expectedRevision: string) =>
    this.api.post('/api/v1/integrations/{source}/chat', {
      path: { source },
      body: { recordId, expectedRevision },
    });
}
