import { Injectable, inject } from '@angular/core';
import { ApiClient, type ApiQuery } from '@core/api/api-client';

/** 已套用的查詢條件；分頁參數（`before`、`take`）每頁另外加上。 */
export type MailFilters = Omit<ApiQuery<'/api/v1/admin/mail-messages', 'get'>, 'before' | 'take'>;

/**
 * 郵件寄送紀錄的讀取、寄信測試與連線診斷（`mail.messages`）；重寄與立即重試另需 `mail.messages.resend`。
 */
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
  /** 等待中的信改成現在寄（同一封、同一個 Message-ID）；重複按只是再提前一次。 */
  retry = (id: string) => this.api.post('/api/v1/admin/mail-messages/{id}/retry', { path: { id } });
  /** 自動重試開關；下一輪寄送就生效，不必重啟。 */
  updateSettings = (autoRetry: boolean) =>
    this.api.put('/api/v1/admin/mail-settings', { body: { autoRetry } });
  /** 寄信測試可選的系統信，含參數說明與範例值。 */
  samples = (signal?: AbortSignal) => this.api.get('/api/v1/admin/mail-samples', { signal });
  /** `parameters` 只放和範例不同的值；沒填的由伺服器補範例值。 */
  test = (to: string, category: string, parameters: Record<string, string>) =>
    this.api.post('/api/v1/admin/mail-tests', { body: { to, category, parameters } });
  /** 同步跑一次 SMTP 對話（不寄出）；`to` 空白時伺服器用寄件地址檢查 RCPT。 */
  diagnose = (to: string) =>
    this.api.post('/api/v1/admin/mail-diagnoses', { body: { to: to || null } });
}
