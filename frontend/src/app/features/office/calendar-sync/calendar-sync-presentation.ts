import type { CalendarEventFieldChange, CalendarSyncRunItemDto } from '@core/api/schema';
import { formatDate } from '@shared/browser/format';
import type { BadgeTone } from '@shared/ui/count-badge';

interface Presented {
  label: string;
  tone: BadgeTone;
}

/** 事件的同步狀態；已移除但還沒刪掉 Google 事件的列仍是待同步或失敗。 */
const EVENT_STATES: Record<string, Presented> = {
  pending: { label: '待同步', tone: 'info' },
  failed: { label: '推送失敗', tone: 'danger' },
  synced: { label: '已同步', tone: 'success' },
  removed: { label: '已移除', tone: 'neutral' },
};

const RUN_STATUSES: Record<string, Presented> = {
  succeeded: { label: '完成', tone: 'success' },
  partial: { label: '部分失敗', tone: 'warning' },
  held: { label: '等待確認移除', tone: 'warning' },
  failed: { label: '無法執行', tone: 'danger' },
};

/** 一輪逐筆的結果，與該輪的數量一一對應；`held` 是被安全閥擋下、還沒動到日曆的移除。 */
const RUN_ITEM_OUTCOMES: Record<string, Presented> = {
  created: { label: '新增', tone: 'success' },
  updated: { label: '更新', tone: 'info' },
  deleted: { label: '刪除', tone: 'neutral' },
  failed: { label: '失敗', tone: 'danger' },
  held: { label: '等待確認', tone: 'warning' },
};

export const EVENT_STATE_OPTIONS = Object.entries(EVENT_STATES).map(([value, item]) => ({
  value,
  label: item.label,
}));
export const RUN_STATUS_OPTIONS = Object.entries(RUN_STATUSES).map(([value, item]) => ({
  value,
  label: item.label,
}));

export const RUN_ITEM_OUTCOME_OPTIONS = Object.entries(RUN_ITEM_OUTCOMES).map(([value, item]) => ({
  value,
  label: item.label,
}));

export const EVENT_KINDS: Record<string, string> = { leave: '請假', travel: '出差' };
export const FLOW_STATUSES: Record<string, string> = { Decide: '已核准', Send: '簽核中' };
export const RUN_TRIGGERS: Record<string, string> = { scheduled: '排程', manual: '手動' };
/** 單據的流程狀態（字樣與公文系統相同）；移除的原因會出現同步範圍以外的狀態。 */
const FORM_FLOWS: Record<string, string> = {
  ...FLOW_STATUSES,
  Temp: '暫存',
  Back: '退回',
  Cancel: '銷單',
  Finish: '退回結案',
};

export const eventState = (state: string): Presented =>
  EVENT_STATES[state] ?? { label: state, tone: 'neutral' };
export const runStatus = (status: string): Presented =>
  RUN_STATUSES[status] ?? { label: status, tone: 'neutral' };
export const runItemOutcome = (outcome: string): Presented =>
  RUN_ITEM_OUTCOMES[outcome] ?? { label: outcome, tone: 'neutral' };
export const runDuration = (run: { startedAt: string; finishedAt: string }) =>
  Date.parse(run.finishedAt) - Date.parse(run.startedAt);

/** 事件被處理的原因（後端 `CalendarEventCauses`）。 */
const CAUSES: Record<string, string> = {
  content: '事件格式更新',
  restored: '單據恢復，放回日曆',
  form_status: '單據不再生效',
  form_missing: '公文系統已找不到這張單',
  segment_gone: '這段期間已不在單據上',
  unknown: '查不到單據現況，原因不明',
  reset: '重設日曆',
};

const CHANGE_LABELS: Record<string, string> = {
  kind: '類別',
  applicantName: '申請人',
  agentName: '代理人',
  flowStatus: '簽核',
  startsAt: '開始',
  endsAt: '結束',
};

function changeValue(field: string, value: string | null | undefined) {
  if (value === null || value === undefined) return '（無）';
  if (field === 'flowStatus') return FORM_FLOWS[value] ?? value;
  if (field === 'kind') return EVENT_KINDS[value] ?? value;
  return field === 'startsAt' || field === 'endsAt' ? formatDate(value) : value;
}

/** 一個欄位的變更；舊系統轉入的單可能沒有舊狀態可比，只寫新狀態。 */
export function changeText({ field, from, to }: CalendarEventFieldChange) {
  const before = field === 'flowStatus' && !from ? '' : `${changeValue(field, from)} → `;
  return `${CHANGE_LABELS[field] ?? field}：${before}${changeValue(field, to)}`;
}

/** 一筆明細要顯示的原因，一行一件事。有欄位變更時就是原因本身，「內容變更」不必再寫一次。 */
export function runItemReasons({
  cause,
  changes,
}: Pick<CalendarSyncRunItemDto, 'cause' | 'changes'>) {
  const details = changes.map(changeText);
  if (!cause || (cause === 'content' && details.length)) return details;
  return [CAUSES[cause] ?? cause, ...details];
}

/**
 * 同步失敗原因（後端 `CalendarSyncFailures`）：一句原因＋一句怎麼辦。
 * 不是 HTTP 錯誤，所以不在共用的錯誤目錄；連線測試也用同一組代碼。
 */
const FAILURES: Record<string, string> = {
  google_not_configured:
    '還沒設定服務帳戶金鑰，或金鑰檔讀不到。請確認 Google:ServiceAccountKeyFile。',
  google_credentials_rejected: 'Google 拒絕了服務帳戶的憑證。請確認金鑰沒有被停用或刪除。',
  google_forbidden: '服務帳戶沒有這本日曆的權限。請把日曆分享給服務帳戶，權限選「變更活動」。',
  google_calendar_not_found: '找不到這本日曆。請確認日曆 ID，並已分享給服務帳戶。',
  google_rate_limited: 'Google 暫時限制呼叫次數。會自動稍後再試。',
  google_unavailable: '暫時連不上 Google。會自動稍後再試。',
  google_rejected: 'Google 不接受這筆事件的內容。請把文號提供給管理員查看。',
  gd_unavailable: '暫時連不上公文系統，這一輪沒有比對。恢復後下一輪會自動補上。',
  removals_held: '要移除的事件超過安全門檻，已暫停移除。確認無誤後按「確認移除」。',
  unexpected: '同步時發生未預期的錯誤。請到系統日誌查看。',
};

export const failureText = (code: string | null | undefined) =>
  code ? (FAILURES[code] ?? `同步失敗（${code}）。`) : '';
