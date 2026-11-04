import { catalogFeature } from '@core/api/catalog';
import type { AuditActionDto, AuditModuleDto } from '@core/api/schema';
import { TRACE_ID } from '@core/errors/safe-errors';
import { queryField } from '@shared/ui/query-fields';
import type { QueryStateOptions } from '@shared/ui/query-state';
import {
  hoursAgo,
  timeRangeBounds,
  type TimeRange,
  type TimeRangePreset,
} from '@shared/ui/time-range-picker';
import type { AuditFilters } from './audit-api';

/**
 * 稽核頁的查詢條件；`category` 是上方的分類切換，切換即套用，不經過「查詢」。
 * `action` 是完整動作或模組前綴（`attendance.`）；`feature` 只從功能使用量頁的連結帶入，以 chip 清除。
 */
export interface AuditQuery {
  category: string;
  search: string;
  action: string;
  feature: string;
  results: string[];
  range: TimeRange;
  traceId: string;
}

export const AUDIT_RANGES: TimeRangePreset[] = [
  { value: 'all', label: '不限時間' },
  { value: '24h', label: '最近 24 小時', duration: hoursAgo(24) },
  { value: '7d', label: '最近 7 天', duration: hoursAgo(24 * 7) },
  { value: '30d', label: '最近 30 天', duration: hoursAgo(24 * 30) },
];
/** 伺服器端的結果群組：`success` 是 `completed`，`failed` 是其餘全部（錯誤代碼）。 */
export const AUDIT_RESULTS = [
  { value: 'success', label: '完成' },
  { value: 'failed', label: '失敗或拒絕' },
];
const ACTION = /^[a-z0-9_.-]{1,120}$/;
const FEATURE = /^[a-z0-9.-]{1,100}$/;

/** 分類下可選的動作；未選分類時列出全部。 */
export const actionsFor = (actions: readonly AuditActionDto[], category: string) =>
  category ? actions.filter((action) => action.category === category) : actions;

/** 動作或模組前綴的顯示名稱；目錄讀到前顯示原始值。 */
export function actionLabel(
  value: string,
  actions: readonly AuditActionDto[],
  modules: readonly AuditModuleDto[],
) {
  if (value.endsWith('.'))
    return (modules.find((item) => item.value + '.' === value)?.label ?? value) + '（全部）';
  return actions.find((item) => item.value === value)?.label ?? value;
}

export function auditFilters(query: AuditQuery, now = Date.now()): AuditFilters | string {
  const bounds = timeRangeBounds(query.range, AUDIT_RANGES, now);
  if (typeof bounds === 'string') return bounds;
  return {
    category: query.category || undefined,
    search: query.search.trim() || undefined,
    action: query.action || undefined,
    feature: query.feature || undefined,
    result: query.results.length ? query.results : undefined,
    traceId: query.traceId || undefined,
    from: bounds.from,
    until: bounds.to,
  };
}

/**
 * 稽核頁 `QueryState` 的設定；動作 chip 的名稱來自 catalog，catalog 載入前先顯示原始值。
 * 使用者詳情（`search`）、系統日誌（`traceId`）與功能使用量（`feature`）的連結會帶入條件。
 */
export function auditQueryOptions(
  actions: () => readonly AuditActionDto[],
  modules: () => readonly AuditModuleDto[],
): QueryStateOptions<AuditQuery> {
  return {
    fields: {
      category: queryField.text('category', { pattern: /^[a-z]{1,40}$/ }),
      search: queryField.text('search', { max: 120, chip: '搜尋' }),
      action: {
        ...queryField.text('action', { pattern: ACTION }),
        chip: (action) => '動作：' + actionLabel(action, actions(), modules()),
      },
      feature: {
        ...queryField.text('feature', { pattern: FEATURE }),
        chip: (feature) => '功能：' + (catalogFeature(feature)?.name ?? feature),
      },
      results: queryField.choices('result', AUDIT_RESULTS, { chip: '結果' }),
      range: queryField.range(AUDIT_RANGES, { initial: 'all', to: 'until', chip: '時間' }),
      traceId: queryField.text('traceId', {
        pattern: TRACE_ID,
        normalize: (value) => value.toLowerCase(),
        chip: '同一流程',
      }),
    },
    validate: (query) => {
      const filters = auditFilters(query);
      return typeof filters === 'string' ? filters : '';
    },
  };
}
