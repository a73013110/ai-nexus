import { FEATURE_NAMES } from '../../../core/feature-names';
import { APP_TIME_ZONE } from '../../../shared/browser/format';

const statuses: Record<string, string> = {
  healthy: '運作正常',
  degraded: '有失敗呼叫',
  failing: '呼叫失敗',
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
export const presenceState = (value: string) => states[value] ?? value;
export const monitoringTime = (value: string) => clock.format(new Date(value));
export const monitoringLatency = (value: number | null | undefined) =>
  value == null ? '—' : value.toLocaleString('zh-TW', { maximumFractionDigits: 1 }) + ' ms';

export const monitoringFeature = (id: string) =>
  FEATURE_NAMES[id] ??
  ({ reader: '閱讀器', settings: '個人設定', system: '系統' } as Record<string, string>)[id] ??
  id;
