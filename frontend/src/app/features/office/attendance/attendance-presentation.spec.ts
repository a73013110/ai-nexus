import { describe, expect, it } from 'vitest';
import type { GdForm } from '@core/api/schema';
import { ApiError } from '@core/errors/safe-errors';
import {
  GAP_COVERAGE,
  GD_FORM_FLOW,
  attendanceProblem,
  clockTime,
  formatSeconds,
  monthBounds,
  presentRecord,
  presentForm,
  shiftMonth,
  summarizePerson,
  taipeiToday,
  weekOf,
  weekday,
} from './attendance-presentation';

const ISSUE = 'NX-' + 'C'.repeat(32);
/** 跨兩天、還在流程中的假單。 */
const pendingForm: GdForm = {
  desNo: 'L-1',
  kind: 'Leave',
  title: '特休',
  startsAt: '2026-10-05T00:30:00Z',
  endsAt: '2026-10-06T10:00:00Z',
  flowStatus: 'Send',
  currentSigner: '林雅婷',
  currentStep: '主管',
  approved: false,
};

describe('attendance presentation', () => {
  it('formats lateness in seconds as minutes and seconds', () => {
    expect(formatSeconds(0)).toBe('');
    expect(formatSeconds(45)).toBe('45 秒');
    expect(formatSeconds(60)).toBe('1 分');
    expect(formatSeconds(725)).toBe('12 分 5 秒');
    expect(formatSeconds(3780)).toBe('1 小時 3 分');
  });

  it('shows punch times in Taipei to the second, whatever the browser zone', () => {
    expect(clockTime('2026-10-01T00:59:03Z')).toBe('08:59:03');
    expect(clockTime(null)).toBe('');
    expect(taipeiToday(Date.parse('2026-10-01T16:30:00Z'))).toBe('2026-10-02');
    expect(weekday('2026-10-01')).toBe('四');
  });

  it('walks months across years', () => {
    expect(monthBounds('2026-02')).toEqual({ from: '2026-02-01', to: '2026-02-28' });
    expect(shiftMonth('2026-01', -1)).toBe('2025-12');
    expect(shiftMonth('2026-12', 1)).toBe('2027-01');
  });

  it('takes every status, reason and form label from the one table', () => {
    const row = presentRecord(
      {
        date: '2026-10-05',
        dayType: 'FullDay',
        calendarRemark: null,
        status: 'Undetermined',
        reason: 'FormsUnavailable',
        clockIn: null,
        clockOut: null,
        expectedClockOut: null,
        lateSeconds: 0,
        earlyLeaveSeconds: 0,
        formState: 'Unavailable',
        formSeconds: 0,
        adjustedClockOut: null,
        coverage: null,
        shortSeconds: 0,
        forms: [],
      },
      '2026-10-05',
    );
    expect(row.status).toEqual({ label: '無法判定', tone: 'warning' });
    expect(row.reason).toContain('表單無法取得');
    expect(row.formsUnavailable).toBe(true);
    expect(row.today).toBe(true);
    const travel = presentRecord(
      {
        ...row.day,
        status: 'OnLeave',
        reason: null,
        formState: 'Filed',
        clockIn: '2026-10-05T01:00:00Z',
        clockOut: '2026-10-05T01:00:00Z',
        forms: [
          {
            desNo: 'A1',
            kind: 'Travel',
            title: '客戶拜訪',
            startsAt: '2026-10-05T01:00:00Z',
            endsAt: '2026-10-05T09:00:00Z',
            flowStatus: 'Decide',
            currentSigner: null,
            currentStep: null,
            approved: true,
          },
        ],
      },
      '2026-10-06',
    );
    expect(travel.status.label).toBe('請假');
    expect(travel.singlePunch).toBe(true);
    expect(travel.forms[0].kind).toBe('出差單');
    // 起訖都在這一天只寫時間；完整起訖留給 title 與報讀。
    expect(travel.forms[0].time).toBe('09:00–17:00');
    expect(travel.forms[0].span).toBe('10/05 09:00 – 10/05 17:00');
    expect(travel.forms[0].waiting).toBe('');
    // 流程狀態用舊系統報表的字樣（與匯出 Excel 相同），不認得的照原值。
    expect(travel.forms[0].flow).toEqual({ label: '核准', tone: 'success' });
    expect(GD_FORM_FLOW.Send).toEqual({ label: '流程中', tone: 'warning' });
    expect(GD_FORM_FLOW.Finish.label).toBe('退回結案');
    expect(
      presentRecord(
        { ...travel.day, forms: [{ ...travel.day.forms[0], flowStatus: 'Odd' as never }] },
        '',
      ).forms[0].flow,
    ).toEqual({ label: 'Odd', tone: 'neutral' });
  });

  it('shows a multi-day form with dates and who an in-flow form waits for', () => {
    const form = pendingForm;
    const presented = presentForm(form, '2026-10-05');
    expect(presented.time).toBe('10/05 08:30 – 10/06 18:00');
    expect(presented.waiting).toBe('等待 林雅婷（主管）');
    expect(presentForm({ ...form, currentSigner: null }, '2026-10-05').waiting).toBe('等待簽核');
  });

  it('labels the day with whether forms make up the gap and what is still short', () => {
    // 09:01:19 到、請 08:30～09:00、17:30:22 走：扣表單後 18:01:19 才可下班，還差 30 分 57 秒。
    const base = presentRecord(
      {
        date: '2026-10-05',
        dayType: 'FullDay',
        calendarRemark: null,
        status: 'Present',
        reason: null,
        clockIn: '2026-10-05T01:01:19Z',
        clockOut: '2026-10-05T09:30:22Z',
        expectedClockOut: '2026-10-05T10:00:00Z',
        lateSeconds: 79,
        earlyLeaveSeconds: 1778,
        formState: 'Filed',
        formSeconds: 1800,
        adjustedClockOut: '2026-10-05T10:01:19Z',
        coverage: 'Unexplained',
        shortSeconds: 1857,
        forms: [],
      },
      '',
    );
    expect(base.coverage).toEqual({
      ...GAP_COVERAGE.Unexplained,
      short: '差 30 分 57 秒',
    });
    expect(base.formHours).toBe('30 分');
    expect([base.expected, base.adjusted]).toEqual(['18:00:00', '18:01:19']);
    expect(base.lateNote).toBe('遲到 1 分 19 秒・未補足');
    expect(base.earlyNote).toBe('早退 29 分 38 秒・未補足');
    // 已補足不寫差額；扣表單後與原本相同時不重複顯示。
    const covered = presentRecord(
      {
        ...base.day,
        coverage: 'Covered',
        shortSeconds: 0,
        adjustedClockOut: base.day.expectedClockOut,
      },
      '',
    );
    expect([covered.coverage?.short, covered.adjusted]).toEqual(['', '']);
    // 沒有刷卡時依表單說明原因：表單還在簽核就說簽核。
    const pending = presentRecord(
      {
        ...base.day,
        status: 'Anomaly',
        reason: 'NoPunchOrLeave',
        clockIn: null,
        clockOut: null,
        expectedClockOut: null,
        adjustedClockOut: null,
        lateSeconds: 0,
        earlyLeaveSeconds: 0,
        formSeconds: 0,
        coverage: 'Pending',
        shortSeconds: 28800,
        forms: [pendingForm],
      },
      '',
    );
    expect(pending.coverage?.short).toBe('差 8 小時');
    expect(pending.reason).toBe('沒有刷卡，表單還在簽核');
    expect(summarizePerson([base.day, covered.day, pending.day])).toEqual({
      late: 2,
      early: 2,
      anomalies: 1,
      unexplained: 1,
      pending: 1,
    });
  });

  it('groups dates by the Monday of their week', () => {
    expect(weekOf('2026-10-05')).toBe('2026-10-05');
    expect(weekOf('2026-10-11')).toBe('2026-10-05');
    expect(weekOf('2026-10-12')).toBe('2026-10-12');
    expect(weekOf('2026-11-01')).toBe('2026-10-26');
  });

  it('explains document-system outages instead of the generic contact-admin message', () => {
    expect(attendanceProblem(new ApiError(503, 'attendance_gd_read_only', undefined, ISSUE))).toBe(
      `公文系統的連線設為唯讀，假日目前只能查詢；請聯絡管理員。 查證代碼：${ISSUE}`,
    );
    expect(
      attendanceProblem(new ApiError(503, 'attendance_gd_unavailable', undefined, ISSUE)),
    ).toContain('公文系統暫時無法連線');
    // 其他 5xx 照一般政策，不洩漏伺服器的文字。
    expect(attendanceProblem(new ApiError(503, 'service_unavailable', '內部細節', ISSUE))).toBe(
      `操作未完成，請聯絡管理員。查證代碼：${ISSUE}`,
    );
    expect(attendanceProblem(new ApiError(409, 'attendance_card_held', undefined, ''))).toContain(
      '確認後可由新持有人接手',
    );
  });
});
