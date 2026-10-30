import { Injectable, inject } from '@angular/core';
import { ApiClient, apiHref, type ApiQuery } from '@core/api/api-client';
import type { AttachmentPolicyDto } from '@core/api/schema';
import { ClientValidationError } from '@core/errors/safe-errors';
import { scopedFileRoute, type FileScope } from '@shared/browser/reader-overlay';

type FileFilters = ApiQuery<'/api/v1/files', 'get'>;

/** 原檔內容的網址，給縮圖與下載連結由瀏覽器直接載入。 */
export const attachmentContent = (id: string) =>
  apiHref('/api/v1/attachments/{id}/content', { path: { id } });

/** 單一檔案的副檔名與大小是否符合附件政策；份數與總量上限由呼叫端依情境檢查。 */
export const acceptsFile = (policy: AttachmentPolicyDto, file: File) =>
  file.size <= policy.maxFileBytes &&
  policy.extensions.includes('.' + file.name.split('.').pop()?.toLowerCase());

/** 已上傳的原始檔：草稿附件、上傳配額與檔案庫。 */
@Injectable({ providedIn: 'root' })
export class FilesApi {
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
  /** 容器（分享、稽核檢視的對話）內檔案的預覽：描述與逐頁擷取文字。 */
  scopedPreview(scope: FileScope, file: string, signal?: AbortSignal) {
    const route = scopedFileRoute(scope, file, 'preview');
    return this.api.get(route.path, { ...route.params, signal });
  }
  /** 容器內檔案的原檔（PDF 繪製、Markdown 與 HTML 的原始文字）。 */
  scopedOriginal(scope: FileScope, file: string, signal?: AbortSignal) {
    const route = scopedFileRoute(scope, file, 'content');
    return this.api.open(route.path, { ...route.params, signal });
  }
}
