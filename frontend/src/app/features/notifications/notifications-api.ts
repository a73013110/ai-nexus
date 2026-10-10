import { inject, Injectable } from '@angular/core';
import { ApiClient } from '../../core/api/api-client';

/** The signed-in user's notification inbox. */
@Injectable({ providedIn: 'root' })
export class NotificationsApi {
  private readonly api = inject(ApiClient);
  list = (unread: boolean, before: string | undefined, signal: AbortSignal) =>
    this.api.get('/api/v1/notifications', { query: { unread, before }, signal });
  read = (id: string) => this.api.post('/api/v1/notifications/{id}/read', { path: { id } });
  readThrough = (through: string) =>
    this.api.post('/api/v1/notifications/read', { body: { through } });
  dismiss = (id: string) => this.api.delete('/api/v1/notifications/{id}', { path: { id } });
}
