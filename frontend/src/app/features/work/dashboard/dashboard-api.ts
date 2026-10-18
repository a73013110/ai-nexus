import { Injectable, inject } from '@angular/core';
import { ApiClient } from '@core/api/api-client';
import type { SpendPeriod } from '@features/admin/billing/billing-api';

@Injectable({ providedIn: 'root' })
export class DashboardApi {
  private readonly api = inject(ApiClient);
  read = (period: SpendPeriod, scope: string, ownerId = '') =>
    this.api.get('/api/v1/dashboard', {
      query: { ...period, scope, ownerId: ownerId || undefined },
    });
}
