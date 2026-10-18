import { inject, Injectable } from '@angular/core';
import { ApiClient } from '@core/api/api-client';

/** 複製與備份對話；登入、列表與執行在核心的 `WorkspaceApi` 中。 */
@Injectable({ providedIn: 'root' })
export class ChatApi {
  private readonly api = inject(ApiClient);
  duplicate = (id: string) =>
    this.api.post('/api/v1/conversations/{id}/duplicate', { path: { id } });
  export = (id: string) => this.api.get('/api/v1/conversations/{id}/export', { path: { id } });
}
