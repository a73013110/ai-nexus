import { inject, Injectable } from '@angular/core';
import { ApiClient } from '@core/api/api-client';
import type { ConversationBackup, ConversationSettingsRequest } from '@core/api/schema';

/** 整理對話：標籤、每個對話的設定與備份匯入。 */
@Injectable({ providedIn: 'root' })
export class ConversationsApi {
  private readonly api = inject(ApiClient);
  labels = () => this.api.get('/api/v1/conversations/labels');
  settings = (id: string, body: ConversationSettingsRequest) =>
    this.api.patch('/api/v1/conversations/{id}/settings', { path: { id }, body });
  import = (body: ConversationBackup) => this.api.post('/api/v1/conversations/import', { body });
}
