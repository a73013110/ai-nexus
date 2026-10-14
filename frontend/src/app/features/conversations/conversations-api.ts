import { inject, Injectable } from '@angular/core';
import { ApiClient } from '@core/api/api-client';
import type { ConversationBackup, ConversationSettingsRequest } from '@core/api/schema';

/** Organizing conversations: labels, per-conversation settings and backup import. */
@Injectable({ providedIn: 'root' })
export class ConversationsApi {
  private readonly api = inject(ApiClient);
  labels = () => this.api.get('/api/v1/conversations/labels');
  settings = (id: string, body: ConversationSettingsRequest) =>
    this.api.patch('/api/v1/conversations/{id}/settings', { path: { id }, body });
  import = (body: ConversationBackup) => this.api.post('/api/v1/conversations/import', { body });
}
