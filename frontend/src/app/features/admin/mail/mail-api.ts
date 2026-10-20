import { Injectable, inject } from '@angular/core';
import { ApiClient, type ApiQuery } from '@core/api/api-client';

/** 已套用的查詢條件；分頁參數（`before`、`take`）每頁另外加上。 */
export type MailFilters = Omit<ApiQuery<'/api/v1/admin/mail-messages', 'get'>, 'before' | 'take'>;

/** 郵件寄送紀錄的讀取、重寄與測試信（`mail.messages`；重寄另需 `mail.messages.resend`）。 */
@Injectable({ providedIn: 'root' })
export class MailApi {
  private readonly api = inject(ApiClient);
  list = (filters: MailFilters, take: number, before?: string, signal?: AbortSignal) =>
    this.api.get('/api/v1/admin/mail-messages', { query: { ...filters, take, before }, signal });
  get = (id: string, signal?: AbortSignal) =>
    this.api.get('/api/v1/admin/mail-messages/{id}', { path: { id }, signal });
  status = (signal?: AbortSignal) => this.api.get('/api/v1/admin/mail-status', { signal });
  /** `key` 讓連點或逾時重試只建立一封；同一次開啟的詳情沿用同一個。 */
  resend = (id: string, key: string) =>
    this.api.post('/api/v1/admin/mail-messages/{id}/resend', {
      path: { id },
      headers: { 'Idempotency-Key': key },
    });
  test = (to: string) => this.api.post('/api/v1/admin/mail-tests', { body: { to } });
}
