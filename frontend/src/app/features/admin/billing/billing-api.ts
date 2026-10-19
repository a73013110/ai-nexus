import { Injectable, inject } from '@angular/core';
import { ApiClient } from '@core/api/api-client';
import type { PriceRequest } from '@core/api/schema';

export interface SpendPeriod {
  from: string;
  until: string;
  offsetMinutes: number;
}
/** 網頁選的結束日期含當天，送出時轉為隔天零點（API 的期間是開始含、結束不含）。 */
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
const CHARGE_KINDS: Record<string, string> = {
  api: 'API 計費估算',
  internal: '內部成本估算',
  free: '免費',
  unpriced: '未定價',
};
export const chargeKind = (kind: string): string => CHARGE_KINDS[kind] ?? kind;
/** 可定價的供應商，依價格表與選單的顯示順序排列。 */
export const PROVIDERS = ['google', 'ollama', 'searxng', 'brave'];
const PROVIDER_NAMES: Record<string, string> = {
  google: 'Google AI',
  ollama: '地端 Ollama',
  searxng: 'SearXNG',
  brave: 'Brave Search',
};
/** 模型與搜尋供應商的顯示名稱；未知的 id 原樣顯示。 */
export const providerName = (id: string): string => PROVIDER_NAMES[id] ?? id;

@Injectable({ providedIn: 'root' })
export class BillingApi {
  private readonly api = inject(ApiClient);
  conversation = (id: string) => this.api.get('/api/v1/conversations/{id}/spend', { path: { id } });
  export = (period: SpendPeriod, ownerId = '') =>
    this.api.open('/api/v1/admin/billing/export', {
      query: { ...period, ownerId: ownerId || undefined },
    });
  prices = () => this.api.get('/api/v1/admin/billing/prices');
  targets = () => this.api.get('/api/v1/admin/billing/targets');
  addPrice = (body: PriceRequest) => this.api.post('/api/v1/admin/billing/prices', { body });
}
