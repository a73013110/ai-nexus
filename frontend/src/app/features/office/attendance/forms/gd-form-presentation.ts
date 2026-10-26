import type {
  GdFlowStep,
  GdFlowStepState,
  GdFormAttachmentDto,
  GdFormDetailDto,
  GdFormPeriod,
  GdPerson,
} from '@core/api/schema';
import { formatDateTimeInput } from '@shared/browser/format';
import type { ApprovalState, ApprovalStep } from '@shared/ui/approval-timeline';
import type { FileListItem } from '@shared/ui/file-list';
import { remoteFileContent } from '@shared/browser/reader-overlay';
import { weekday } from '../attendance-presentation';

/** GD 以 8 小時算一天（請假時數與天數的換算）。 */
const HOURS_PER_DAY = 8;
const DAY = 86_400_000;

/** 台北時間的日期鍵（`YYYY-MM-DD`）與「HH:mm」。 */
export function taipeiParts(value: string) {
  const text = formatDateTimeInput(new Date(value));
  return { date: text.slice(0, 10), time: text.slice(11, 16) };
}

/** 「2026/09/01（二）」。單據可能跨年度（年底請到隔年、翻查前一年的單），所以日期一律帶年。 */
const dayLabel = (date: string) => `${date.replaceAll('-', '/')}（${weekday(date)}）`;

/** 「2026/10/22（四）08:00」。 */
export function stamp(value: string) {
  const parts = taipeiParts(value);
  return `${dayLabel(parts.date)}${parts.time}`;
}

/** 「2026/09/01（二）」：跨期間假單的起訖只寫日期。 */
export const dateStamp = (value: string) => dayLabel(taipeiParts(value).date);

/**
 * 期間摘要的時數：滿一天寫「3 天」、說明「共 24 小時」；不滿一天只寫小時。`segments` 有值（跨期間）時
 * 說明前面加段數：「共 36 段・144 小時」，不滿一天就只寫「共 5 段」。沒有時數回傳 null。
 */
export function hoursSummary(hours: number | null | undefined, segments = 0) {
  if (hours == null || !(hours > 0)) return null;
  const round = (value: number) => String(Math.round(value * 10) / 10);
  const total = `${round(hours)} 小時`;
  const value = hours < HOURS_PER_DAY ? total : `${round(hours / HOURS_PER_DAY)} 天`;
  const sum = value === total ? '' : `共 ${total}`;
  const detail = segments ? [`共 ${segments} 段`, sum.slice(2)].filter(Boolean).join('・') : sum;
  return { value, detail };
}

/** 人員的一行說明：「資訊組 · 專員」。 */
export const personDetail = (person: GdPerson | null | undefined) =>
  [person?.unit, person?.title].filter(Boolean).join(' · ');

/**
 * 跨期間的一條規則：同一個星期幾、同一段時間的各段合成一條（「每週一 09:00–12:00」，共 4 次）；
 * `morning`、`afternoon` 是這段落在上午或下午，與月曆的上緣、下緣對應。
 */
export interface PeriodRule {
  key: string;
  label: string;
  count: number;
  morning: boolean;
  afternoon: boolean;
}

/** 依星期（週一起）與起訖時間分組；GD 的跨期間假單本來就是「某幾個星期幾的某個時段」展開成各段。 */
export function periodRules(segments: readonly GdFormPeriod[]): PeriodRule[] {
  const groups = new Map<string, Omit<PeriodRule, 'key'> & { order: number }>();
  for (const segment of segments) {
    const start = taipeiParts(segment.startsAt),
      end = taipeiParts(segment.endsAt);
    const day = new Date(start.date + 'T00:00:00Z').getUTCDay();
    const key = `${day}|${start.time}|${end.time}`;
    const group = groups.get(key);
    if (group) group.count++;
    else
      groups.set(key, {
        order: ((day + 6) % 7) * 10000 + Number(start.time.replace(':', '')),
        label: `每週${weekday(start.date)} ${start.time}–${end.time}`,
        count: 1,
        ...halves(start, end),
      });
  }
  return [...groups.entries()]
    .sort(([, a], [, b]) => a.order - b.order)
    .map(([key, { label, count, morning, afternoon }]) => ({
      key,
      label,
      count,
      morning,
      afternoon,
    }));
}

/** 12:00 前開始算上午、12:00 後結束（或跨日）算下午；兩者都是就是整天。 */
function halves(start: ReturnType<typeof taipeiParts>, end: ReturnType<typeof taipeiParts>) {
  return { morning: start.time < '12:00', afternoon: end.time > '12:00' || end.date > start.date };
}

/** 某一天被請到的半天：上午（12:00 前開始）、下午（12:00 後結束），報讀用的時段字樣另外列出。 */
export interface PeriodDay {
  morning: boolean;
  afternoon: boolean;
  times: string[];
}

export function periodDays(segments: readonly GdFormPeriod[]) {
  const days = new Map<string, PeriodDay>();
  for (const segment of segments) {
    const start = taipeiParts(segment.startsAt),
      end = taipeiParts(segment.endsAt);
    const day = days.get(start.date) ?? { morning: false, afternoon: false, times: [] };
    const half = halves(start, end);
    day.morning ||= half.morning;
    day.afternoon ||= half.afternoon;
    day.times.push(`${start.time}–${end.time}`);
    days.set(start.date, day);
  }
  return days;
}

/**
 * 從出勤紀錄點開的那天落在第幾段：「2026/10/22（四）08:00–12:00」與「第 16／36 段」。那天沒有請假時為 null。
 */
export function focusSegment(segments: readonly GdFormPeriod[], date: string | null) {
  if (!date) return null;
  const index = segments.findIndex((segment) => taipeiParts(segment.startsAt).date === date);
  if (index < 0) return null;
  const start = taipeiParts(segments[index].startsAt),
    end = taipeiParts(segments[index].endsAt);
  return {
    label: `${dayLabel(start.date)}${start.time}–${end.time}`,
    position: `第 ${index + 1}／${segments.length} 段`,
  };
}

/** 第一段到最後一段經過的月份（`YYYY-MM`），中間沒有任何一段的月份也列出，月曆才連續。 */
export function periodMonths(segments: readonly GdFormPeriod[]): string[] {
  const keys = segments.map((segment) => taipeiParts(segment.startsAt).date.slice(0, 7)).sort();
  if (!keys.length) return [];
  const months: string[] = [];
  let [year, month] = keys[0].split('-').map(Number);
  const last = keys[keys.length - 1];
  for (let guard = 0; guard < 36; guard++) {
    const key = `${year}-${String(month).padStart(2, '0')}`;
    months.push(key);
    if (key >= last) break;
    month = month === 12 ? 1 : month + 1;
    if (month === 1) year++;
  }
  return months;
}

const STATE: Record<GdFlowStepState, ApprovalState> = {
  Done: 'done',
  Current: 'current',
  Pending: 'pending',
};

/** 從 `at` 到現在經過幾個台北日曆日。 */
function daysSince(at: string, now: number) {
  const day = (value: number) => Date.parse(formatDateTimeInput(new Date(value)).slice(0, 10));
  return Math.max(0, Math.round((day(now) - day(Date.parse(at))) / DAY));
}

/**
 * 簽核流程轉成共用時間軸：已處理寫處理時間；目前關卡在時間的位置寫已等候幾天（當天送達寫「今天送達」），
 * 送達時間留在提示；未到的關卡沒有時間。代理簽核的關卡附上代理人。
 */
export function approvalSteps(steps: readonly GdFlowStep[], now = Date.now()): ApprovalStep[] {
  return steps.map((step, index) => {
    const state = STATE[step.state] ?? 'pending';
    const waited = state === 'current' && step.at ? daysSince(step.at, now) : null;
    return {
      key: `${index}`,
      state,
      title: step.action,
      person: step.person?.name ?? null,
      detail: personDetail(step.person) || null,
      note: step.agent ? `代理：${step.agent}` : null,
      waiting: waited === null ? null : waited === 0 ? '今天送達' : `已等候 ${waited} 天`,
      time: step.at ? stamp(step.at) : null,
      dateTime: step.at,
      memo: step.memo,
    };
  });
}

/** 「第 2 關，共 4 關」；全部完成寫「共 4 關，已完成」，沒有關卡時為空字串。 */
export function progressText(steps: readonly GdFlowStep[]) {
  if (!steps.length) return '';
  const current = steps.findIndex((step) => step.state !== 'Done');
  return current < 0
    ? `共 ${steps.length} 關，已完成`
    : `第 ${current + 1} 關，共 ${steps.length} 關`;
}

/** 公文系統的附件轉成共用檔案清單：預覽與下載都走憑證，不經過公文系統的網址。 */
export function attachmentItems(files: readonly GdFormAttachmentDto[]): FileListItem[] {
  return files.map(({ file, memo, uploader }) => ({
    key: file.ticket,
    fileName: file.fileName,
    detail: [memo, uploader ? `${uploader} 上傳` : ''].filter(Boolean).join(' · ') || '附件',
    reader: {
      id: file.ticket,
      attachment: false,
      remote: { ticket: file.ticket, fileName: file.fileName, contentType: file.contentType },
    },
    downloadUrl: remoteFileContent(file.ticket, true),
    thumbnail: file.isImage ? remoteFileContent(file.ticket) : null,
  }));
}

/** 對話框標題：請假寫假別，出差寫出差事由（公文系統的主旨）。 */
export const formHeading = (form: GdFormDetailDto) =>
  form.title || (form.kind === 'Travel' ? '出差單' : '請假單');
