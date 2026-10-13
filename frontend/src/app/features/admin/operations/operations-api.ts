import { Injectable, inject } from '@angular/core';
import { ApiClient } from '../../../core/api/api-client';
import type { AdminRetrievalSearchRequest } from '../../../core/api/schema';

/** Platform operations: platform usage and retrieval administration. Prices are in `BillingApi`. */
@Injectable({ providedIn: 'root' })
export class OperationsApi {
  private readonly api = inject(ApiClient);
  usage = () => this.api.get('/api/v1/admin/usage');
  embeddingProfiles = () => this.api.get('/api/v1/admin/knowledge/profiles');
  rebuildProfile = (id: number) =>
    this.api.post('/api/v1/admin/knowledge/profiles/{id}/rebuild', { path: { id } });
  activateProfile = (id: number) =>
    this.api.post('/api/v1/admin/knowledge/profiles/{id}/activate', { path: { id } });
  clearProfileVectors = (id: number) =>
    this.api.delete('/api/v1/admin/knowledge/profiles/{id}/vectors', { path: { id } });
  retrievalCapabilities = () => this.api.get('/api/v1/admin/knowledge/capabilities');
  probeRetrieval = () => this.api.post('/api/v1/admin/knowledge/capabilities/probe');
  searchRetrieval = (body: AdminRetrievalSearchRequest) =>
    this.api.post('/api/v1/admin/knowledge/search', { body });
}
