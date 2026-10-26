import type {
  AttendanceDayType,
  AttendanceRecordDto,
  AttendanceReason,
  AttendanceScheduleDto,
  AttendanceStatus,
  GapCoverage,
  GdFormFlowOver,
  GdForm,
  GdFormKind,
} from '@core/api/schema';
import { publicErrorHints } from '@core/errors/public-error-catalog';
import { ApiError, safeMessage, validIssueCode } from '@core/errors/safe-errors';
import { formatDateTimeInput, formatDuration } from '@shared/browser/format';
import type { BadgeTone } from '@shared/ui/count-badge';

/** 出勤四頁共用的顯示文字與格式；狀態、原因、日型的字樣只寫在這裡，範本不另寫對照。 */

export interface Presented {
  label: string;
  tone: BadgeTone;
}

export const ATTENDANCE_STATUS: Record<AttendanceStatus, Presented> = {
  Present: { label: '出勤', tone: 'success' },
  OnLeave: { label: '請假', tone: 'info' },
  Anomaly: { label: '異常', tone: 'danger' },
  Undetermined: { label: '無法判定', tone: 'warning' },
  DayOff: { label: '放假', tone: 'neutral' },
};

/** 判定的原因；出勤與放假沒有原因。 */
export const ATTENDANCE_REASON: Record<NonNullable<AttendanceReason>, string> = {
  NoPunchOrLeave: '沒有刷卡，也沒有假單',
  SinglePunch: '只有一筆刷卡，分不出上班或下班',
  FormsUnavailable: '表單無法取得，沒刷卡的日子不判異常',
  DayTypeUnknown: '假日行事曆無法取得，不確定是否上班日',
  DayInProgress: '今天尚未結束，打卡檔晚上才匯入',
};

export const ATTENDANCE_DAY_TYPE: Record<AttendanceDayType, string> = {
  FullDay: '上班日',
  MorningOff: '上午放假',
  AfternoonOff: '下午放假',
  DayOff: '放假',
  Unknown: '未知',
};

export const GD_FORM_KIND: Record<GdFormKind, string> = {
  Leave: '假單',
  Travel: '出差單',
};

export const IMPORT_STATUS: Record<string, Presented> = {
  succeeded: { label: '成功', tone: 'success' },
  failed: { label: '失敗', tone: 'danger' },
};
export const importStatus = (status: string): Presented =>
  IMPORT_STATUS[status] ?? { label: status, tone: 'neutral' };
export const IMPORT_TRIGGER: Record<string, string> = {
  scheduled: '排程',
  manual: '手動重匯',
};

/**
 * 遲到、早退的秒數：「45 秒」「12 分 5 秒」「1 小時 3 分」；0 為空字串（沒有遲到不顯示）。
 * 一分鐘以上沿用共用的 `formatDuration`，與其他頁的耗時同一種寫法。
 */
export function formatSeconds(seconds: number): string {
  if (!(seconds > 0)) return '';
  return seconds < 60 ? `${Math.floor(seconds)} 秒` : formatDuration(seconds * 1000);
}

/** 刷卡時間（台北）的「時:分:秒」；沒有刷卡為空字串。 */
export const clockTime = (value: string | null | undefined) =>
  value ? formatDateTimeInput(new Date(value)).slice(11) : '';

const WEEKDAYS = '日一二三四五六';
/** `YYYY-MM-DD` 的星期（「一」）；日期本身沒有時區，以 UTC 計算才不會差一天。 */
export const weekday = (date: string) => WEEKDAYS[new Date(date + 'T00:00:00Z').getUTCDay()];
export const isWeekend = (date: string) => /[日六]/.test(weekday(date));
/** `YYYY-MM-DD` 那一週的週一（同樣以 UTC 計算）；用來在換週的列畫分隔線。 */
export function weekOf(date: string): string {
  const day = new Date(date + 'T00:00:00Z');
  day.setUTCDate(day.getUTCDate() - ((day.getUTCDay() + 6) % 7));
  return day.toISOString().slice(0, 10);
}

/** 台北今天的 `YYYY-MM-DD`；出勤日與 GD 行事曆都以台灣日期為準，不用瀏覽器的時區。 */
export const taipeiToday = (now = Date.now()) => formatDateTimeInput(new Date(now)).slice(0, 10);

/** `YYYY-MM` 的第一天與最後一天。 */
export function monthBounds(month: string): { from: string; to: string } {
  const [year, index] = month.split('-').map(Number);
  const last = new Date(Date.UTC(year, index, 0)).getUTCDate();
  return { from: `${month}-01`, to: `${month}-${String(last).padStart(2, '0')}` };
}
/** 往前（-1）或往後（1）幾個月的 `YYYY-MM`。 */
export function shiftMonth(month: string, delta: number): string {
  const [year, index] = month.split('-').map(Number);
  const date = new Date(Date.UTC(year, index - 1 + delta, 1));
  return `${date.getUTCFullYear()}-${String(date.getUTCMonth() + 1).padStart(2, '0')}`;
}
export const MONTH = /^\d{4}-(0[1-9]|1[0-2])$/;
export const monthLabel = (month: string) => `${month.slice(0, 4)} 年 ${Number(month.slice(5))} 月`;

/**
 * 公文系統與打卡檔資料夾無法使用時回 503；一般錯誤政策對 5xx 一律顯示「請聯絡管理員」，
 * 但這幾個代碼的原因是固定的（GD 唯讀、連不上、資料夾讀不到），顯示公開提示讓 HR 知道問題在哪，查證代碼照樣附上。
 */
const EXPLAINED_UNAVAILABLE = new Set([
  'attendance_gd_unavailable',
  'attendance_gd_read_only',
  'attendance_import_source_unavailable',
]);
export function attendanceProblem(error: unknown): string {
  if (
    error instanceof ApiError &&
    error.status === 503 &&
    EXPLAINED_UNAVAILABLE.has(error.code) &&
    Object.hasOwn(publicErrorHints, error.code)
  )
    return (
      publicErrorHints[error.code] +
      (validIssueCode(error.issueCode) ? ` 查證代碼：${error.issueCode}` : '')
    );
  return safeMessage(error);
}
/** 錯誤的代碼；不是 API 錯誤時為空字串。 */
export const errorCode = (error: unknown) => (error instanceof ApiError ? error.code : '');

/**
 * 規則說明裡一種日型的時間（後端 `AttendanceRules` 提供設定值，這裡只排版）：「08:30–18:00」「上班＋9.5 小時」。
 * 時間是 `HH:mm:ss` 字串（台北），只取到分。
 */
export function presentSchedule(schedule: AttendanceScheduleDto) {
  const time = (value: string) => value.slice(0, 5);
  return {
    dayType: ATTENDANCE_DAY_TYPE[schedule.dayType] ?? schedule.dayType,
    scheduled: `${time(schedule.scheduledStart)}–${time(schedule.scheduledEnd)}`,
    flex: `${time(schedule.flexStart)}–${time(schedule.flexEnd)}`,
    clockOut: `上班＋${Number(schedule.workHours.toFixed(2))} 小時`,
  };
}

/**
 * 公文系統的流程狀態（`FlowOver`）：字樣與 GD 畫面、匯出 Excel 的備註相同。出勤只讀核准與流程中兩種；
 * 其他狀態列出來是為了未知值也有可讀的字樣（後端 `GdFlowOver.Label`）。
 */
export const GD_FORM_FLOW: Record<GdFormFlowOver, Presented> = {
  Decide: { label: '核准', tone: 'success' },
  Send: { label: '流程中', tone: 'warning' },
  Temp: { label: '暫存', tone: 'neutral' },
  Back: { label: '退回', tone: 'neutral' },
  Cancel: { label: '銷單', tone: 'neutral' },
  Finish: { label: '退回結案', tone: 'neutral' },
};

/**
 * 當天的遲到、早退或異常有沒有被表單補足（後端 `GapCoverage`，規則在 `AttendanceRules` 的「表單補足」）。
 * 已補足是綠、還在等簽核是黃、不夠是紅；表單無法取得是灰，不當成沒請假。
 */
export const GAP_COVERAGE: Record<NonNullable<GapCoverage>, Presented> = {
  Covered: { label: '已補足', tone: 'success' },
  Pending: { label: '簽核中', tone: 'warning' },
  Unexplained: { label: '未補足', tone: 'danger' },
  Unknown: { label: '表單無法取得', tone: 'neutral' },
};
const coverage = (value: GapCoverage | undefined) => (value ? GAP_COVERAGE[value] : null);
/** 時間格標色時的提示：「遲到 12 分 5 秒・未補足」；沒有缺口時為空字串。 */
const gapNote = (label: string, seconds: number, value: GapCoverage | undefined) =>
  seconds > 0
    ? [`${label} ${formatSeconds(seconds)}`, coverage(value)?.label].filter(Boolean).join('・')
    : '';

/** 台北時間的「MM/DD」與「HH:mm」。 */
const formParts = (value: string) => {
  const text = formatDateTimeInput(new Date(value));
  return {
    date: text.slice(0, 10),
    day: text.slice(5, 10).replace('-', '/'),
    time: text.slice(11, 16),
  };
};

/**
 * 表單在某一天的顯示值。起訖都在這一天只寫時間（「08:30–12:00」），跨日才帶日期，同一天多張單時一眼比得出時段。
 * 流程中的單附上目前停在誰（GD `FLOW_CURRENT`），查不到時只寫「等待簽核」。
 */
export function presentForm(form: GdForm, date: string) {
  const start = formParts(form.startsAt);
  const end = formParts(form.endsAt);
  const sameDay = start.date === date && end.date === date;
  const signer = form.currentSigner
    ? `等待 ${form.currentSigner}${form.currentStep ? `（${form.currentStep}）` : ''}`
    : '等待簽核';
  return {
    form,
    kind: GD_FORM_KIND[form.kind] ?? form.kind,
    flow: GD_FORM_FLOW[form.flowStatus] ?? { label: form.flowStatus, tone: 'neutral' },
    time: sameDay
      ? `${start.time}–${end.time}`
      : `${start.day} ${start.time} – ${end.day} ${end.time}`,
    /** 完整起訖，放在 title 與報讀文字。 */
    span: `${start.day} ${start.time} – ${end.day} ${end.time}`,
    waiting: form.flowStatus === 'Send' ? signer : '',
  };
}
export type PresentedForm = ReturnType<typeof presentForm>;

/** 沒有刷卡時的原因：依表單的狀況說清楚是沒請假、只請了一部分還是還在簽核。 */
function noPunchReason(day: AttendanceRecordDto) {
  if (!day.forms.length) return ATTENDANCE_REASON.NoPunchOrLeave;
  return day.coverage === 'Pending' ? '沒有刷卡，表單還在簽核' : '沒有刷卡，核准的表單不足一天';
}

/**
 * 補足狀態加上核准的表單還差多少（`short`：「差 30 分 57 秒」，未補足與簽核中才有；簽核中的差額在單核准後補上）。
 * 沒有缺口時為 null。
 */
function coverageOf(day: AttendanceRecordDto) {
  const value = coverage(day.coverage);
  if (!value) return null;
  const short = formatSeconds(day.shortSeconds);
  return { ...value, short: short ? `差 ${short}` : '' };
}

/** 出勤紀錄一天的顯示值；範本只讀這些欄位，不自己查對照表。 */
export function presentRecord(day: AttendanceRecordDto, today: string) {
  return {
    day,
    date: day.date.slice(5).replace('-', '/'),
    weekday: weekday(day.date),
    weekend: isWeekend(day.date),
    today: day.date === today,
    dayType: ATTENDANCE_DAY_TYPE[day.dayType] ?? day.dayType,
    /** 假日設定的備註（例如「國慶日」）：放假的原因。 */
    remark: day.calendarRemark ?? '',
    status: ATTENDANCE_STATUS[day.status] ?? { label: day.status, tone: 'neutral' },
    reason:
      day.reason === 'NoPunchOrLeave'
        ? noPunchReason(day)
        : day.reason
          ? (ATTENDANCE_REASON[day.reason] ?? day.reason)
          : '',
    clockIn: clockTime(day.clockIn),
    clockOut: clockTime(day.clockOut),
    /** 只有一筆刷卡時上下班是同一筆，只顯示一次。 */
    singlePunch: !!day.clockIn && day.clockIn === day.clockOut,
    expected: clockTime(day.expectedClockOut),
    /** 扣掉核准表單後的應下班；與原本的應下班相同時不重複顯示。 */
    adjusted:
      day.adjustedClockOut && day.adjustedClockOut !== day.expectedClockOut
        ? clockTime(day.adjustedClockOut)
        : '',
    /** 核准表單抵掉的時數（「2 小時」）。 */
    formHours: formatSeconds(day.formSeconds),
    late: formatSeconds(day.lateSeconds),
    early: formatSeconds(day.earlyLeaveSeconds),
    /** 當天的補足狀態（遲到、早退、異常合在一起看）；沒有缺口時為 null。 */
    coverage: coverageOf(day),
    lateNote: gapNote('遲到', day.lateSeconds, day.coverage),
    earlyNote: gapNote('早退', day.earlyLeaveSeconds, day.coverage),
    /** 沒有刷卡、表單也沒補足：上下班格直接標「未刷卡」，不只在狀態欄說明。 */
    missing: day.reason === 'NoPunchOrLeave',
    formsUnavailable: day.formState === 'Unavailable',
    forms: day.forms.map((form) => presentForm(form, day.date)),
  };
}
export type PresentedRecord = ReturnType<typeof presentRecord>;

/** 一個人的小計：天數照舊系統（遲到、早退、異常各幾天），另數出未補足、簽核中的天數。 */
export function summarizePerson(days: readonly AttendanceRecordDto[]) {
  return {
    late: days.filter((day) => day.lateSeconds > 0).length,
    early: days.filter((day) => day.earlyLeaveSeconds > 0).length,
    anomalies: days.filter((day) => day.status === 'Anomaly').length,
    unexplained: days.filter((day) => day.coverage === 'Unexplained').length,
    pending: days.filter((day) => day.coverage === 'Pending').length,
  };
}
