import { inject, Injectable } from '@angular/core';
import { ApiClient } from '@core/api/api-client';
import type { CreateShareRequest } from '@core/api/schema';
import type { TableRequestQuery } from '@shared/ui/table-model';

@Injectable({ providedIn: 'root' })
export class SharingApi {
  private readonly api = inject(ApiClient);
  list = (sent: boolean, table: TableRequestQuery, signal?: AbortSignal) =>
    this.api.get('/api/v1/shares', { query: { sent, ...table }, signal });
  get = (id: string) => this.api.get('/api/v1/shares/{id}', { path: { id } });
  create = (body: CreateShareRequest) => this.api.post('/api/v1/shares', { body });
  revoke = (id: string) => this.api.delete('/api/v1/shares/{id}', { path: { id } });
}
