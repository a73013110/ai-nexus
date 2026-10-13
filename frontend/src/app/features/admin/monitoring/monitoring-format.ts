import type { HealthSummary, SubsystemFact } from '../../../core/api/schema';
import { FEATURE_NAMES } from '../../../core/auth/feature-names';
import {
  APP_TIME_ZONE,
  compactNumber,
  formatBytes,
  formatNumber,
  metricText,
} from '../../../shared/browser/format';
import type { BadgeTone } from '../../../shared/ui/count-badge';

const statuses: Record<string, string> = {
  healthy: '運作正常',
  degraded: '有失敗呼叫',
  failing: '呼叫失敗',
  unobserved: '尚無觀測',
};
const subsystemStatuses: Record<string, string> = {
  healthy: '運作正常',
  degraded: '效能下降',
  failing: '發生故障',
  idle: '閒置',
  unobserved: '尚無觀測',
};
const states: Record<string, string> = { active: '使用中', idle: '閒置', background: '背景' };
const clock = new Intl.DateTimeFormat('zh-TW', {
  timeZone: APP_TIME_ZONE,
  hour: '2-digit',
  minute: '2-digit',
  second: '2-digit',
  hour12: false,
});
export const dependencyStatus = (value: string) => statuses[value] ?? value;
export const subsystemStatus = (value: string) => subsystemStatuses[value] ?? value;
export const presenceState = (value: string) => states[value] ?? value;
export const monitoringTime = (value: string) => clock.format(new Date(value));
export const monitoringLatency = (value: number | null | undefined) =>
  value == null ? '—' : value.toLocaleString('zh-TW', { maximumFractionDigits: 1 }) + ' ms';

export const monitoringFeature = (id: string) =>
  FEATURE_NAMES[id] ??
  ({ reader: '閱讀器', settings: '個人設定', system: '系統' } as Record<string, string>)[id] ??
  id;

/**
 * Status words of dependencies and subsystems as badge tones. Only real problems carry warning or
 * danger; healthy is the quiet success tone and anything not observed stays neutral.
 */
export function toneOf(status: string): BadgeTone {
  switch (status) {
    case 'healthy':
      return 'success';
    case 'degraded':
    case 'attention':
    case 'warning':
      return 'warning';
    case 'failing':
    case 'critical':
      return 'danger';
    default:
      return 'neutral';
  }
}
/** A fact's tone from the backend as a badge tone; positive reads as success. */
export function factTone(tone: string): BadgeTone {
  return tone === 'positive'
    ? 'success'
    : tone === 'warning' || tone === 'danger'
      ? tone
      : 'neutral';
}

/** How long the process has run, at the precision a person reads: 「3 天 4 小時」. */
export function uptimeText(seconds: number | null | undefined) {
  if (seconds == null || !Number.isFinite(seconds) || seconds < 0) return '—';
  const minutes = Math.floor(seconds / 60),
    hours = Math.floor(minutes / 60),
    days = Math.floor(hours / 24);
  if (days) return `${days} 天 ${hours % 24} 小時`;
  if (hours) return `${hours} 小時 ${minutes % 60} 分`;
  if (minutes) return `${minutes} 分鐘`;
  return '不到 1 分鐘';
}

/** A subsystem fact as text in its unit; an unknown value is 「—」, never zero. */
export function factText(fact: Pick<SubsystemFact, 'value' | 'unit' | 'text'>): string {
  if (fact.unit === 'text') return fact.text?.trim() || '—';
  const value = fact.value;
  if (value === null || value === undefined || !Number.isFinite(value))
    return fact.text?.trim() || '—';
  switch (fact.unit) {
    case 'bytes':
      return formatBytes(value);
    case 'percent':
      return metricText(value, 'percent');
    case 'ms':
      return metricText(value, 'duration');
    case 'seconds':
      return value < 3600 ? metricText(value * 1000, 'duration') : uptimeText(value);
    case 'tokens':
      return Math.abs(value) >= 100_000 ? compactNumber(value) : formatNumber(Math.round(value));
    default:
      return metricText(value, 'count');
  }
}

export interface HealthView {
  readonly enabled: boolean;
  readonly health: HealthSummary;
  readonly connection: string;
  readonly stale: boolean;
  readonly at: string;
  readonly minutes: number;
}
export interface HealthHeadline {
  /** Changes only when the words do, so the headline animates on real changes. */
  readonly key: string;
  readonly title: string;
  readonly detail: string;
  readonly tone: BadgeTone;
}

/** The hero's one-line answer to "is the system all right?", with what to do next. */
export function healthHeadline(view: HealthView): HealthHeadline {
  const issues = view.health.issues;
  if (!view.enabled)
    return {
      key: 'disabled',
      title: '監控採集已停用',
      detail: '啟用 Monitoring.Enabled 並重新啟動服務後開始採集。',
      tone: 'neutral',
    };
  if (view.connection === 'paused')
    return {
      key: 'paused',
      title: '已暫停更新',
      detail: `顯示 ${monitoringTime(view.at)} 的資料，按「繼續更新」恢復即時資料。`,
      tone: 'neutral',
    };
  if (view.stale)
    return {
      key: 'stale',
      title: `連線中斷，顯示 ${monitoringTime(view.at)} 的資料`,
      detail: '正在重新連線；恢復後自動更新。',
      tone: 'warning',
    };
  switch (view.health.status) {
    case 'critical': {
      const critical = issues.filter((issue) => issue.severity === 'critical');
      const first = critical[0] ?? issues[0];
      const others = issues.length - critical.length;
      return {
        key: `critical:${critical.length}`,
        title: `${Math.max(1, critical.length)} 項嚴重問題`,
        detail:
          (first ? `請優先處理：${first.title}` : '請檢查下方的問題清單。') +
          (others > 0 ? `；另有 ${others} 項需要注意` : ''),
        tone: 'danger',
      };
    }
    case 'attention':
      return {
        key: `attention:${issues.length}`,
        title: `${Math.max(1, issues.length)} 項需要注意`,
        detail: issues[0]?.title ?? '請檢查下方的問題清單。',
        tone: 'warning',
      };
    case 'healthy':
      return {
        key: 'healthy',
        title: '系統運作正常',
        detail: `最近 ${view.minutes} 分鐘沒有需要處理的問題`,
        tone: 'success',
      };
    default:
      return {
        key: 'unobserved',
        title: '尚無足夠觀測資料',
        detail: '採集剛啟動，第一個 10 秒區間完成後顯示。',
        tone: 'neutral',
      };
  }
}

/** What a screen reader hears when the health settles on a new state. */
export function healthAnnouncement(health: HealthSummary) {
  const first = health.issues[0]?.title;
  switch (health.status) {
    case 'critical':
      return `系統有嚴重問題：${first ?? '請檢查監控頁'}`;
    case 'attention':
      return `系統有 ${health.issues.length} 項需要注意：${first ?? ''}`;
    case 'healthy':
      return '系統恢復正常';
    default:
      return '尚無足夠觀測資料';
  }
}
