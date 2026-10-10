import { Injectable, inject } from '@angular/core';
import { ApiClient } from '../../core/api/api-client';
import type { PriceRequest } from '../../core/api/schema';

export interface SpendPeriod {
  from: string;
  until: string;
  offsetMinutes: number;
}
export function datePeriod(from: string, through: string): SpendPeriod {
  const start = new Date(from + 'T00:00:00');
  const end = new Date(through + 'T00:00:00');
  end.setDate(end.getDate() + 1);
  return {
    from: start.toISOString(),
    until: end.toISOString(),
    offsetMinutes: -start.getTimezoneOffset(),
  };
}
export function localDate(date: Date): string {
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`;
}
export function money(amount: number, currency: string): string {
  if (!currency) return '未定價';
  return new Intl.NumberFormat('zh-TW', {
    style: 'currency',
    currency,
    currencyDisplay: 'code',
    maximumFractionDigits: 8,
  }).format(amount);
}
export function chargeKind(kind: string): string {
  return (
    (
      { api: 'API 計費估算', internal: '內部成本估算', free: '免費', unpriced: '未定價' } as Record<
        string,
        string
      >
    )[kind] ?? kind
  );
}
@Injectable({ providedIn: 'root' })
export class BillingApi {
  private readonly api = inject(ApiClient);
  conversation = (id: string) => this.api.get('/api/v1/conversations/{id}/spend', { path: { id } });
  dashboard = (period: SpendPeriod, scope: string, ownerId = '') =>
    this.api.get('/api/v1/dashboard', {
      query: { ...period, scope, ownerId: ownerId || undefined },
    });
  report = (period: SpendPeriod, ownerId = '') =>
    this.api.get('/api/v1/admin/billing/spend', {
      query: { ...period, ownerId: ownerId || undefined },
    });
  export = (period: SpendPeriod, ownerId = '') =>
    this.api.open('/api/v1/admin/billing/export', {
      query: { ...period, ownerId: ownerId || undefined },
    });
  prices = () => this.api.get('/api/v1/admin/billing/prices');
  targets = () => this.api.get('/api/v1/admin/billing/targets');
  addPrice = (body: PriceRequest) => this.api.post('/api/v1/admin/billing/prices', { body });
}
