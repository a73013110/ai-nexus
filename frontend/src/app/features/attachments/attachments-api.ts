import { Injectable, inject } from '@angular/core';
import { ApiClient, type ApiQuery } from '@core/api/api-client';
import { ClientValidationError } from '@core/errors/safe-errors';

type FileFilters = ApiQuery<'/api/v1/files', 'get'>;

/** Uploaded originals: draft attachments, the upload quota and the file library. */
@Injectable({ providedIn: 'root' })
export class AttachmentsApi {
  private readonly api = inject(ApiClient);
  policy = () => this.api.reference('/api/v1/attachments/policy');
  attachment = (id: string) => this.api.get('/api/v1/attachments/{id}', { path: { id } });
  async upload(file: File, signal: AbortSignal) {
    const storage = await this.api.get('/api/v1/attachments/storage', { signal });
    if (file.size > storage.remainingBytes) throw new ClientValidationError('attachmentQuota');
    const body = new FormData();
    body.append('file', file);
    return this.api.upload('/api/v1/attachments', body, { signal });
  }
  removeAttachment = (id: string) => this.api.delete('/api/v1/attachments/{id}', { path: { id } });
  files = (filters: FileFilters, signal?: AbortSignal) =>
    this.api.get('/api/v1/files', { query: filters, signal });
  renameFile = (id: string, fileName: string, expectedFileName: string) =>
    this.api.put('/api/v1/files/{id}/name', { path: { id }, body: { fileName, expectedFileName } });
  retainFile = (id: string) => this.api.post('/api/v1/files/{id}/retain', { path: { id } });
  removeFile = (id: string) => this.api.delete('/api/v1/files/{id}', { path: { id } });
}
