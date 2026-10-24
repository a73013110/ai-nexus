import type {
  AttendanceDayType,
  AttendanceRecordDto,
  AttendanceReason,
  AttendanceScheduleDto,
  AttendanceStatus,
  LeaveSheetKind,
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
  LeaveUnavailable: '假單無法取得，沒刷卡的日子不判異常',
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

export const LEAVE_SHEET_KIND: Record<LeaveSheetKind, string> = {
  Leave: '假單',
  Travel: '出差單',
};

export const CARD_KINDS = [
  { value: 'employee', label: '員工卡' },
  { value: 'visitor', label: '訪客卡' },
];
export const cardKindLabel = (kind: string) =>
  CARD_KINDS.find((item) => item.value === kind)?.label ?? kind;

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

/** 單據起訖（台北）的「MM/DD HH:mm」。 */
const sheetTime = (value: string) => {
  const text = formatDateTimeInput(new Date(value));
  return `${text.slice(5, 10).replace('-', '/')} ${text.slice(11, 16)}`;
};

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
    reason: day.reason ? (ATTENDANCE_REASON[day.reason] ?? day.reason) : '',
    clockIn: clockTime(day.clockIn),
    clockOut: clockTime(day.clockOut),
    /** 只有一筆刷卡時上下班是同一筆，只顯示一次。 */
    singlePunch: !!day.clockIn && day.clockIn === day.clockOut,
    expected: clockTime(day.expectedClockOut),
    late: formatSeconds(day.lateSeconds),
    early: formatSeconds(day.earlyLeaveSeconds),
    leaveUnavailable: day.leave === 'Unavailable',
    sheets: day.sheets.map((sheet) => ({
      sheet,
      kind: LEAVE_SHEET_KIND[sheet.kind] ?? sheet.kind,
      time: `${sheetTime(sheet.startsAt)} – ${sheetTime(sheet.endsAt)}`,
    })),
  };
}
export type PresentedRecord = ReturnType<typeof presentRecord>;
