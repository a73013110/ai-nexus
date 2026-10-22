import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { AttendanceRecordDto, AttendanceRecordsDto } from '@core/api/schema';
import { AuthService } from '@core/auth/auth-service';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { OrganizationApi } from '@features/admin/organization/organization-api';
import { AttendanceApi } from '../attendance-api';
import { AttendanceRecordsPage } from './attendance-records-page';

const day = (date: string, patch: Partial<AttendanceRecordDto> = {}): AttendanceRecordDto => ({
  date,
  dayType: 'FullDay',
  status: 'Present',
  reason: null,
  clockIn: `${date}T08:59:03+08:00`,
  clockOut: `${date}T18:05:41+08:00`,
  expectedClockOut: `${date}T18:00:00+08:00`,
  lateSeconds: 0,
  earlyLeaveSeconds: 0,
  leave: 'None',
  sheets: [],
  ...patch,
});

const records = (patch: Partial<AttendanceRecordsDto> = {}): AttendanceRecordsDto => ({
  from: '2026-10-01',
  to: '2026-10-31',
  today: '2026-10-22',
  calendarAvailable: true,
  leaveAvailable: true,
  people: [
    {
      userId: 'u1',
      displayName: '王小明',
      account: 'TEST\\ming',
      leaveAvailable: true,
      excluded: false,
      days: [
        day('2026-10-01', { lateSeconds: 725 }),
        day('2026-10-02', {
          status: 'Anomaly',
          reason: 'NoPunchOrLeave',
          clockIn: null,
          clockOut: null,
          expectedClockOut: null,
        }),
      ],
    },
  ],
  ...patch,
});

function setup(read: () => Promise<AttendanceRecordsDto>, features = ['attendance.records']) {
  vi.stubGlobal('matchMedia', (query: string) => ({
    matches: false,
    media: query,
    addEventListener() {},
    removeEventListener() {},
  }));
  const generation = signal(1);
  const session = {
    auth: { generation },
    load: vi.fn(async () => true),
    me: signal({ id: 'u1' }),
    has: (feature: string) => features.includes(feature),
    featureName: (id: string) => (id === 'attendance.records' ? '出勤紀錄' : id),
    testing: signal(false),
    testingNote: signal(''),
  };
  const api = { records: vi.fn((_query: { from?: string; userId?: string }) => read()) };
  const organization = { units: vi.fn(async () => []) };
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      { provide: WorkspaceSession, useValue: session },
      { provide: AuthService, useValue: { generation, guard: () => () => true } },
      { provide: AttendanceApi, useValue: api },
      { provide: OrganizationApi, useValue: organization },
    ],
  });
  const fixture = TestBed.createComponent(AttendanceRecordsPage);
  const root = fixture.nativeElement as HTMLElement;
  return { fixture, root, api, organization };
}

describe('AttendanceRecordsPage', () => {
  afterEach(() => {
    TestBed.resetTestingModule();
    vi.unstubAllGlobals();
  });

  it('reads the current month and lists each person by day with readable lateness', async () => {
    const { fixture, root, api, organization } = setup(async () => records());
    await fixture.whenStable();
    expect(api.records).toHaveBeenCalledOnce();
    const [query] = api.records.mock.calls[0];
    expect(query.from).toMatch(/^\d{4}-\d{2}-01$/);
    expect(query.userId).toBeUndefined();
    expect(organization.units).not.toHaveBeenCalled();
    expect(root.querySelector('.records-person-row')?.textContent).toContain('王小明');
    expect(root.querySelector('.records-person-row')?.textContent).toContain('異常 1 天');
    const rows = root.querySelectorAll('.records-person-group tr:not(.records-person-row)');
    expect(rows).toHaveLength(2);
    expect(rows[0].textContent).toContain('08:59:03');
    expect(rows[0].textContent).toContain('12 分 5 秒');
    expect(rows[1].textContent).toContain('異常');
    expect(rows[1].textContent).toContain('沒有刷卡，也沒有假單');
    expect(root.querySelector('.records-leave-unavailable')).toBeNull();
    expect(root.querySelector('.records-calendar-unavailable')).toBeNull();
    // 只看得到自己時，「含不列入出勤」沒有作用，不顯示。
    expect(root.querySelector('.records-excluded')).toBeNull();
  });

  it('marks leave and calendar as unavailable when the document system is down', async () => {
    const down = records({ calendarAvailable: false, leaveAvailable: false });
    down.people[0].leaveAvailable = false;
    down.people[0].days = [
      day('2026-10-05', {
        dayType: 'Unknown',
        status: 'Undetermined',
        reason: 'DayTypeUnknown',
        leave: 'Unavailable',
      }),
    ];
    const { fixture, root } = setup(async () => down);
    await fixture.whenStable();
    expect(root.querySelector('.records-leave-unavailable')?.textContent).toContain('假單無法取得');
    expect(root.querySelector('.records-calendar-unavailable')?.textContent).toContain(
      '假日行事曆無法取得',
    );
    const row = root.querySelector('.records-person-group tr:not(.records-person-row)')!;
    expect(row.textContent).toContain('無法判定');
    expect(row.querySelector('.records-unavailable')?.textContent).toContain('無法取得');
  });

  it('flags a person whose leave cannot be matched while the rest are available', async () => {
    const data = records();
    data.people.push({
      ...data.people[0],
      userId: 'u2',
      displayName: '陳小華',
      account: 'TEST\\hua',
      leaveAvailable: false,
      excluded: true,
      days: [day('2026-10-01', { leave: 'Unavailable' })],
    });
    const { fixture, root } = setup(async () => data);
    await fixture.whenStable();
    const groups = root.querySelectorAll('.records-person-row');
    expect(groups[0].textContent).not.toContain('假單無法取得');
    expect(groups[1].textContent).toContain('假單無法取得');
    expect(groups[1].textContent).toContain('不列入出勤');
    expect(root.querySelector('.records-leave-unavailable')).toBeNull();
    expect(root.querySelector('.records-excluded')).not.toBeNull();
  });

  it('offers units only to accounts that can read the organization', async () => {
    const { fixture, organization } = setup(
      async () => records(),
      ['attendance.records', 'organization'],
    );
    await fixture.whenStable();
    expect(organization.units).toHaveBeenCalledOnce();
    expect(fixture.componentInstance.unitOptions()?.[0].label).toBe('所有單位');
  });
});
