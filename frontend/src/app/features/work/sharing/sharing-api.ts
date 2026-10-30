import { inject, Injectable } from '@angular/core';
import { ApiClient } from '@core/api/api-client';
import type { CreateShareRequest } from '@core/api/schema';

@Injectable({ providedIn: 'root' })
export class SharingApi {
  private readonly api = inject(ApiClient);
  list = (sent = false) => this.api.get('/api/v1/shares', { query: { sent } });
  get = (id: string) => this.api.get('/api/v1/shares/{id}', { path: { id } });
  create = (body: CreateShareRequest) => this.api.post('/api/v1/shares', { body });
  revoke = (id: string) => this.api.delete('/api/v1/shares/{id}', { path: { id } });
}
