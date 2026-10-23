import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type {
  AttendanceRecordDto,
  AttendanceRecordsDto,
  AttendanceRulesDto,
} from '@core/api/schema';
import { AuthService } from '@core/auth/auth-service';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { tourSteps } from '@shared/ui/product-tour';
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

const rules: AttendanceRulesDto = {
  schedules: [
    {
      dayType: 'FullDay',
      scheduledStart: '08:30:00',
      scheduledEnd: '18:00:00',
      flexStart: '08:00:00',
      flexEnd: '09:00:00',
      workHours: 9.5,
    },
  ],
  rules: [{ title: '遲到', text: '晚於彈性終點上班為遲到。例：上班日 09:10 到，遲到 10 分。' }],
};

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
  const api = {
    records: vi.fn((_query: { from?: string; userId?: string }) => read()),
    rules: vi.fn(async () => rules),
    exportRecords: vi.fn(async (_query: { from?: string; userId?: string }) => new Response('')),
  };
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
  const { createObjectURL, revokeObjectURL } = URL;
  afterEach(() => {
    TestBed.resetTestingModule();
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
    Object.assign(URL, { createObjectURL, revokeObjectURL });
  });

  /** 攔下下載：記錄 `<a download>` 的檔名，不真的觸發瀏覽器下載。 */
  function captureDownloads() {
    const names: string[] = [];
    Object.assign(URL, { createObjectURL: () => 'blob:records', revokeObjectURL: () => undefined });
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (
      this: HTMLAnchorElement,
    ) {
      names.push(this.download);
    });
    return names;
  }

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

  it('shows the judgement rules from the server in the header panel', async () => {
    const { fixture, root, api } = setup(async () => records());
    await fixture.whenStable();
    expect(api.rules).toHaveBeenCalledOnce();
    const schedule = root.querySelector('.records-rules .info-table')!;
    expect(schedule.textContent).toContain('上班日');
    expect(schedule.textContent).toContain('08:30–18:00');
    expect(schedule.textContent).toContain('08:00–09:00');
    expect(schedule.textContent).toContain('上班＋9.5 小時');
    expect(root.querySelector('.records-rules .info-terms')?.textContent).toContain(
      '09:10 到，遲到 10 分',
    );
  });

  it('exports the applied filters and saves the file under the server name', async () => {
    const { fixture, root, api } = setup(async () => records());
    await fixture.whenStable();
    const downloads = captureDownloads();
    let finish!: (response: Response) => void;
    api.exportRecords.mockReturnValueOnce(new Promise<Response>((resolve) => (finish = resolve)));
    const button = root.querySelector<HTMLButtonElement>('.records-export')!;
    expect(button.textContent).toContain('匯出 Excel');

    button.click();
    await fixture.whenStable();
    // 匯出期間按鈕停用並顯示進度，避免重複送出。
    expect(button.disabled).toBe(true);
    expect(button.textContent).toContain('匯出中');
    expect(api.exportRecords).toHaveBeenCalledOnce();
    expect(api.exportRecords.mock.calls[0][0]).toEqual(api.records.mock.calls[0][0]);

    finish(
      new Response('xlsx', {
        headers: {
          'Content-Disposition': `attachment; filename*=UTF-8''${encodeURIComponent('出勤紀錄_20261001-20261031_20261022_100000.xlsx')}`,
        },
      }),
    );
    await vi.waitFor(() =>
      expect(downloads).toEqual(['出勤紀錄_20261001-20261031_20261022_100000.xlsx']),
    );
    await fixture.whenStable();
    expect(button.disabled).toBe(false);
    expect(root.querySelector('.records-export-error')).toBeNull();
  });

  it('reports a failed export without downloading anything', async () => {
    const { fixture, root, api } = setup(async () => records());
    await fixture.whenStable();
    const downloads = captureDownloads();
    api.exportRecords.mockRejectedValueOnce(new Error('network down'));

    root.querySelector<HTMLButtonElement>('.records-export')!.click();
    await vi.waitFor(async () => {
      await fixture.whenStable();
      expect(root.querySelector('.records-export-error')).not.toBeNull();
    });
    expect(downloads).toEqual([]);
  });

  it('finds every tour target and mentions units only when they can be chosen', async () => {
    const { fixture, root } = setup(async () => records(), ['attendance.records', 'organization']);
    await fixture.whenStable();
    const tour = fixture.componentInstance.tour();
    expect(root.querySelector('nx-feature-page nx-product-tour-button')).not.toBeNull();
    expect(
      tourSteps(tour)
        .map((step) => step.description)
        .join(),
    ).toContain('單位（含下層）');
    for (const step of tourSteps(tour))
      expect(
        root.querySelector(step.target.replace(/^nx-attendance-records-page /, '')),
        step.target,
      ).not.toBeNull();
  });

  it('does not offer an export when nobody is listed', async () => {
    const { fixture, root } = setup(async () => records({ people: [] }));
    await fixture.whenStable();
    expect(root.querySelector<HTMLButtonElement>('.records-export')!.disabled).toBe(true);
  });
});
