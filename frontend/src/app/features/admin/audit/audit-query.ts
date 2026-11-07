import { catalogFeature } from '@core/api/catalog';
import type { AuditActionDto, AuditModuleDto } from '@core/api/schema';
import { TRACE_ID } from '@core/errors/safe-errors';
import { queryField } from '@shared/ui/query-fields';
import type { SelectOption } from '@shared/ui/select';
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
 * `module` 是動作名稱的第一段，`action` 是完整動作名稱（精確比對）；只選動作不選模組也可以。
 * `feature` 只從功能使用量頁的連結帶入，以 chip 清除。
 */
export interface AuditQuery {
  category: string;
  search: string;
  module: string;
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
const MODULE = /^[a-z0-9-]{1,40}$/;
// `<模組>.<對象>.<動詞>`：至少兩段、不以點結尾（舊連結的模組前綴 `attendance.` 不再是動作）。
const ACTION = /^(?=.{1,120}$)[a-z0-9-]+(?:\.[a-z0-9_-]+)+$/;
const FEATURE = /^[a-z0-9.-]{1,100}$/;

/** 分類（與模組）下可選的動作；未選時不限。 */
export const actionsFor = (actions: readonly AuditActionDto[], category: string, module = '') =>
  actions.filter(
    (action) =>
      (!category || action.category === category) && (!module || action.module === module),
  );

/** 動作的顯示名稱；目錄讀到前（或已移除的動作）顯示原始值。 */
export const actionLabel = (value: string, actions: readonly AuditActionDto[]) =>
  actions.find((item) => item.value === value)?.label ?? value;

/** 模組的顯示名稱；目錄讀到前顯示原始值。 */
export const moduleLabel = (value: string, modules: readonly AuditModuleDto[]) =>
  modules.find((item) => item.value === value)?.label ?? value;

/** 模組篩選的選項：只列分類下有動作的模組；搜尋也比對模組代碼。 */
export function moduleOptions(
  actions: readonly AuditActionDto[],
  modules: readonly AuditModuleDto[],
  category: string,
): SelectOption[] {
  const present = new Set(actionsFor(actions, category).map((action) => action.module));
  return [
    { value: '', label: '所有模組' },
    ...modules
      .filter((module) => present.has(module.value))
      .map(({ value, label }) => ({ value, label, keywords: value })),
  ];
}

/**
 * 動作篩選的選項：選了模組只列該模組的動作；沒選時列全部並依模組分組（目錄的動作已依模組排序，同組相鄰）。
 * 搜尋比對名稱、組名與動作代碼。
 */
export function actionOptions(
  actions: readonly AuditActionDto[],
  modules: readonly AuditModuleDto[],
  category: string,
  module: string,
): SelectOption[] {
  return [
    { value: '', label: '所有動作' },
    ...actionsFor(actions, category, module).map(({ value, label, module: owner }) => ({
      value,
      label,
      keywords: value,
      group: module ? undefined : moduleLabel(owner, modules),
    })),
  ];
}

export function auditFilters(query: AuditQuery, now = Date.now()): AuditFilters | string {
  const bounds = timeRangeBounds(query.range, AUDIT_RANGES, now);
  if (typeof bounds === 'string') return bounds;
  return {
    category: query.category || undefined,
    search: query.search.trim() || undefined,
    module: query.module || undefined,
    action: query.action || undefined,
    feature: query.feature || undefined,
    result: query.results.length ? query.results : undefined,
    traceId: query.traceId || undefined,
    from: bounds.from,
    until: bounds.to,
  };
}

/**
 * 稽核頁 `QueryState` 的設定；模組與動作 chip 的名稱來自 catalog，catalog 載入前先顯示原始值。
 * 換模組時清掉不屬於新模組的動作（`dependents`）；清回「所有模組」只是範圍變大，動作保留。
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
      module: {
        ...queryField.text('module', { pattern: MODULE }),
        chip: (module) => '模組：' + moduleLabel(module, modules()),
      },
      action: {
        ...queryField.text('action', { pattern: ACTION }),
        chip: (action) => '動作：' + actionLabel(action, actions()),
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
    dependents: { module: ['action'] },
    validate: (query) => {
      const filters = auditFilters(query);
      return typeof filters === 'string' ? filters : '';
    },
  };
}
