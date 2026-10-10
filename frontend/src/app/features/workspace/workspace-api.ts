import { ClientValidationError } from '../../core/api/safe-errors';
import { inject, Injectable } from '@angular/core';
import { ApiClient } from '../../core/api/api-client';
import type { ConversationBackup, ConversationSettingsRequest } from '../../core/api/schema';

@Injectable({ providedIn: 'root' })
export class WorkspaceApi {
  private readonly api = inject(ApiClient);
  labels = () => this.api.get('/api/v1/conversations/labels');
  settings = (id: string, body: ConversationSettingsRequest) =>
    this.api.patch('/api/v1/conversations/{id}/settings', { path: { id }, body });
  duplicate = (id: string) =>
    this.api.post('/api/v1/conversations/{id}/duplicate', { path: { id } });
  export = (id: string) => this.api.get('/api/v1/conversations/{id}/export', { path: { id } });
  import = (body: ConversationBackup) => this.api.post('/api/v1/conversations/import', { body });
  prompts = () => this.api.get('/api/v1/prompt-templates');
  savePrompt = (id: string | null, title: string, content: string) =>
    id
      ? this.api.put('/api/v1/prompt-templates/{id}', { path: { id }, body: { title, content } })
      : this.api.post('/api/v1/prompt-templates', { body: { title, content } });
  deletePrompt = (id: string) => this.api.delete('/api/v1/prompt-templates/{id}', { path: { id } });
  attachmentPolicy = () => this.api.reference('/api/v1/attachments/policy');
  attachmentStorage = (signal?: AbortSignal) =>
    this.api.get('/api/v1/attachments/storage', { signal });
  attachment = (id: string) => this.api.get('/api/v1/attachments/{id}', { path: { id } });
  async upload(file: File, signal: AbortSignal) {
    const storage = await this.attachmentStorage(signal);
    if (file.size > storage.remainingBytes) throw new ClientValidationError('attachmentQuota');
    const body = new FormData();
    body.append('file', file);
    return this.api.upload('/api/v1/attachments', body, { signal });
  }
  removeAttachment = (id: string) => this.api.delete('/api/v1/attachments/{id}', { path: { id } });
}
