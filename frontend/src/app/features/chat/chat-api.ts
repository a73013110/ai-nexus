import { inject, Injectable } from '@angular/core';
import { ApiClient } from '@core/api/api-client';

/** Copying and backing up a conversation; sign-in, the list and runs are in the core `NexusApi`. */
@Injectable({ providedIn: 'root' })
export class ChatApi {
  private readonly api = inject(ApiClient);
  duplicate = (id: string) =>
    this.api.post('/api/v1/conversations/{id}/duplicate', { path: { id } });
  export = (id: string) => this.api.get('/api/v1/conversations/{id}/export', { path: { id } });
}
