import type { HealthSummary, SubsystemFact } from '@core/api/schema';
import { inject } from '@angular/core';
import { WorkspaceSession } from '@core/auth/workspace-session';
import {
  APP_TIME_ZONE,
  compactNumber,
  formatBytes,
  formatMilliseconds,
  formatNumber,
  metricText,
} from '@shared/browser/format';
import type { BadgeTone } from '@shared/ui/count-badge';

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
export const monitoringLatency = (value: number | null | undefined) => formatMilliseconds(value, 1);

const PAGES: Readonly<Record<string, string>> = {
  reader: '閱讀器',
  settings: '個人設定',
  system: '系統',
};

/**
 * 為 presence 與流量回報的功能或頁面 id 命名。功能名稱是管理員設定的名稱（取自檢視者的 `/me`）；
 * 在 `inject` 可用處呼叫，例如欄位初始化時。
 */
export function monitoringFeatureNames() {
  const session = inject(WorkspaceSession);
  return (id: string) => PAGES[id] ?? session.featureName(id);
}

/**
 * 依賴項與子系統的狀態字，對應為徽章色調。只有真正的問題才用警告或危險色；
 * 健康是安靜的成功色，未觀察到的則維持中性。
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
/** 依賴項或子系統是否列為異常；閒置與尚無觀測都不算。 */
export const isProblem = (status: string) => status === 'degraded' || status === 'failing';
/** 健康問題的等級對應徽章色調與文字；不是嚴重就是注意。 */
export const severityTone = (severity: string): BadgeTone =>
  severity === 'critical' ? 'danger' : 'warning';
export const severityText = (tone: BadgeTone) => (tone === 'danger' ? '嚴重' : '注意');
/** 數列的已知值；裝飾用的趨勢會略過沒有樣本的桶。 */
export const knownValues = (values: readonly (number | null)[]) =>
  values.filter((value): value is number => value !== null && Number.isFinite(value));

/** 來自後端的事實，以徽章色調表示；正向狀態顯示為成功色。 */
export function factTone(tone: string): BadgeTone {
  return tone === 'positive'
    ? 'success'
    : tone === 'warning' || tone === 'danger'
      ? tone
      : 'neutral';
}

/** 程序已運作多久，取人會讀的精度：「3 天 4 小時」。 */
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

/** 子系統的事實以其單位呈現為文字；未知的值顯示「—」，絕不顯示為零。 */
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
  /** 只有文字改變時才改變，讓標題只在真正的變化時動畫。 */
  readonly key: string;
  readonly title: string;
  readonly detail: string;
  readonly tone: BadgeTone;
}

/** 健康區塊的一行答案，回答「系統是否正常？」並說明接下來要做什麼。 */
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

/** 健康狀態穩定到新狀態時，螢幕閱讀器會聽到的內容。 */
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
