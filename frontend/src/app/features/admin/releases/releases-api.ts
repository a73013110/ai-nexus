import { inject, Injectable } from '@angular/core';
import { ApiClient } from '@core/api/api-client';

/** 版本頁（`releases`）：版本對照、部署歷史與正式機的結構異動。 */
@Injectable({ providedIn: 'root' })
export class ReleasesApi {
  private readonly api = inject(ApiClient);
  status = (signal?: AbortSignal) => this.api.get('/api/v1/admin/release-status', { signal });
  deployments = (signal?: AbortSignal) => this.api.get('/api/v1/admin/deployments', { signal });
  changes = (signal?: AbortSignal) => this.api.get('/api/v1/admin/schema-changes', { signal });
  /** 這一版應有的定義與目前的定義；打開差異時才讀。 */
  change = (schemaName: string, name: string, signal?: AbortSignal) =>
    this.api.get('/api/v1/admin/schema-changes/{schemaName}/{name}', {
      path: { schemaName, name },
      signal,
    });
  /** 異動物件目前定義的 .sql，套用到開發庫後匯出提交。 */
  export = () => this.api.open('/api/v1/admin/schema-changes/export');
}
