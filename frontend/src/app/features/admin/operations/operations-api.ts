import { Injectable, inject } from '@angular/core';
import { ApiClient } from '@core/api/api-client';

/** 平台用量頁（`operations.usage`）；費用報表與匯出在 `BillingApi`。 */
@Injectable({ providedIn: 'root' })
export class OperationsApi {
  private readonly api = inject(ApiClient);
  usage = () => this.api.get('/api/v1/admin/usage');
}
