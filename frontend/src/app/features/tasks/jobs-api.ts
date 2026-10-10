import { Injectable, inject } from '@angular/core';
import { ApiClient } from '../../core/api/api-client';

@Injectable({ providedIn: 'root' })
export class JobsApi {
  private readonly api = inject(ApiClient);
  list = () => this.api.get('/api/v1/jobs');
  get = (id: string, signal?: AbortSignal) =>
    this.api.get('/api/v1/jobs/{id}', { path: { id }, signal });
  cancel = (id: string) => this.api.post('/api/v1/jobs/{id}/cancel', { path: { id } });
  retry = (id: string) => this.api.post('/api/v1/jobs/{id}/retry', { path: { id } });
}
