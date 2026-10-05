import { Injectable, inject } from '@angular/core';
import { ApiTransport } from '../../core/api/api-transport';
import type { ConversationSpend, Dashboard, ModelPrice, PriceRequest, SpendReport } from '../../core/api/types';

export interface SpendPeriod { from: string; until: string; offsetMinutes: number }
export function datePeriod(from: string, through: string): SpendPeriod {
  const start = new Date(from + 'T00:00:00');
  const end = new Date(through + 'T00:00:00'); end.setDate(end.getDate() + 1);
  return { from: start.toISOString(), until: end.toISOString(), offsetMinutes: -start.getTimezoneOffset() };
}
export function localDate(date: Date): string {
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`;
}
export function money(amount: number, currency: string): string {
  if (!currency) return '未定價';
  return new Intl.NumberFormat('zh-TW', { style: 'currency', currency, currencyDisplay: 'code', maximumFractionDigits: 8 }).format(amount);
}
export function chargeKind(kind: string): string {
  return ({ api: 'API 計費估算', internal: '本機內部成本', free: '免費', unpriced: '未定價' } as Record<string, string>)[kind] ?? kind;
}
@Injectable({ providedIn: 'root' })
export class BillingApi {
  private readonly http = inject(ApiTransport);
  conversation = (id: string) => this.http.json<ConversationSpend>(`/conversations/${encodeURIComponent(id)}/spend`);
  dashboard = (period: SpendPeriod, scope: string, ownerId = '') => this.http.json<Dashboard>(
    `/dashboard?${this.query(period, scope, ownerId)}`);
  report = (period: SpendPeriod, ownerId = '') => this.http.json<SpendReport>(`/admin/billing/spend?${this.query(period, 'platform', ownerId)}`);
  export = (period: SpendPeriod, ownerId = '') => this.http.response(`/admin/billing/export?${this.query(period, 'platform', ownerId)}`);
  prices = () => this.http.json<ModelPrice[]>('/admin/billing/prices');
  addPrice = (body: PriceRequest) => this.http.json<ModelPrice>('/admin/billing/prices', 'POST', body);
  private query(period: SpendPeriod, scope: string, ownerId: string) {
    return new URLSearchParams({ ...period, offsetMinutes: String(period.offsetMinutes), scope, ...(ownerId ? { ownerId } : {}) });
  }
}
