import { inject, Injectable } from '@angular/core';
import { ApiClient } from '@core/api/api-client';
import type { NotificationTargetDto } from '@core/api/schema';

/** 已登入使用者的通知收件匣。 */
@Injectable({ providedIn: 'root' })
export class NotificationsApi {
  private readonly api = inject(ApiClient);
  list = (unread: boolean, before: string | undefined, signal: AbortSignal) =>
    this.api.get('/api/v1/notifications', { query: { unread, before }, signal });
  read = (id: string) => this.api.post('/api/v1/notifications/{id}/read', { path: { id } });
  /** 指向這個目的地的通知全部標為已讀，含未載入的較舊通知。 */
  readTarget = ({ kind, id }: NotificationTargetDto) =>
    this.api.post('/api/v1/notifications/targets/{kind}/{id}/read', { path: { kind, id } });
  readThrough = (through: string) =>
    this.api.post('/api/v1/notifications/read', { body: { through } });
  dismiss = (id: string) => this.api.delete('/api/v1/notifications/{id}', { path: { id } });
}
