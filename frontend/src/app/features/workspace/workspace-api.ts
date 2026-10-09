import { ClientValidationError } from '../../core/api/safe-errors';
import { inject, Injectable } from '@angular/core';
import { ApiTransport } from '../../core/api/api-transport';
import type {
  Attachment,
  AttachmentPolicy,
  AttachmentStorage,
  Conversation,
  ConversationBackup,
  ConversationSettings,
  PromptTemplate,
} from '../../core/api/types';

@Injectable({ providedIn: 'root' })
export class WorkspaceApi {
  private readonly http = inject(ApiTransport);
  labels = () => this.http.json<string[]>('/conversations/labels');
  settings = (id: string, body: ConversationSettings) =>
    this.http.json<Conversation>(
      `/conversations/${encodeURIComponent(id)}/settings`,
      'PATCH',
      body,
    );
  duplicate = (id: string) =>
    this.http.json<Conversation>(`/conversations/${encodeURIComponent(id)}/duplicate`, 'POST');
  export = (id: string) =>
    this.http.json<ConversationBackup>(`/conversations/${encodeURIComponent(id)}/export`);
  import = (body: ConversationBackup) =>
    this.http.json<Conversation>('/conversations/import', 'POST', body);
  prompts = () => this.http.json<PromptTemplate[]>('/prompt-templates');
  savePrompt = (id: string | null, title: string, content: string) =>
    this.http.json<PromptTemplate>(
      `/prompt-templates${id ? '/' + encodeURIComponent(id) : ''}`,
      id ? 'PUT' : 'POST',
      { title, content },
    );
  deletePrompt = (id: string) =>
    this.http.json<void>(`/prompt-templates/${encodeURIComponent(id)}`, 'DELETE');
  attachmentPolicy = () => this.http.reference<AttachmentPolicy>('/attachments/policy');
  attachmentStorage = (signal?: AbortSignal) =>
    this.http.json<AttachmentStorage>('/attachments/storage', 'GET', undefined, undefined, signal);
  attachment = (id: string) => this.http.json<Attachment>(`/attachments/${encodeURIComponent(id)}`);
  async upload(file: File, signal: AbortSignal) {
    const storage = await this.attachmentStorage(signal);
    if (file.size > storage.remainingBytes) throw new ClientValidationError('attachmentQuota');
    const body = new FormData();
    body.append('file', file);
    return this.http.json<Attachment>('/attachments', 'POST', body, undefined, signal);
  }
  removeAttachment = (id: string) =>
    this.http.json<void>(`/attachments/${encodeURIComponent(id)}`, 'DELETE');
}
