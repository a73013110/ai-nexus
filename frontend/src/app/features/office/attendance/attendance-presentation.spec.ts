import { describe, expect, it } from 'vitest';
import { ApiError } from '@core/errors/safe-errors';
import {
  attendanceProblem,
  clockTime,
  formatSeconds,
  monthBounds,
  presentRecord,
  shiftMonth,
  taipeiToday,
  weekday,
} from './attendance-presentation';

const ISSUE = 'NX-' + 'C'.repeat(32);

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

  it('takes every status, reason and sheet label from the one table', () => {
    const row = presentRecord(
      {
        date: '2026-10-05',
        dayType: 'FullDay',
        calendarRemark: null,
        status: 'Undetermined',
        reason: 'LeaveUnavailable',
        clockIn: null,
        clockOut: null,
        expectedClockOut: null,
        lateSeconds: 0,
        earlyLeaveSeconds: 0,
        leave: 'Unavailable',
        sheets: [],
      },
      '2026-10-05',
    );
    expect(row.status).toEqual({ label: '無法判定', tone: 'warning' });
    expect(row.reason).toContain('假單無法取得');
    expect(row.leaveUnavailable).toBe(true);
    expect(row.today).toBe(true);
    const travel = presentRecord(
      {
        ...row.day,
        status: 'OnLeave',
        reason: null,
        leave: 'Filed',
        clockIn: '2026-10-05T01:00:00Z',
        clockOut: '2026-10-05T01:00:00Z',
        sheets: [
          {
            desNo: 'A1',
            kind: 'Travel',
            title: '客戶拜訪',
            startsAt: '2026-10-05T01:00:00Z',
            endsAt: '2026-10-05T09:00:00Z',
            flowStatus: '已核准',
          },
        ],
      },
      '2026-10-06',
    );
    expect(travel.status.label).toBe('請假');
    expect(travel.singlePunch).toBe(true);
    expect(travel.sheets[0].kind).toBe('出差單');
    expect(travel.sheets[0].time).toBe('10/05 09:00 – 10/05 17:00');
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
