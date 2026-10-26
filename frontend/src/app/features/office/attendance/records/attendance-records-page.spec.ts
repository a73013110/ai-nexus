import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type {
  AttendancePersonOptionDto,
  AttendanceRecordDto,
  AttendanceRecordsDto,
  AttendanceRulesDto,
} from '@core/api/schema';
import { AuthService } from '@core/auth/auth-service';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { tourSteps } from '@shared/ui/product-tour';
import { OrganizationApi } from '@features/admin/organization/organization-api';
import { AttendanceApi, type AttendanceRecordFilters } from '../attendance-api';
import { AttendanceRecordsPage } from './attendance-records-page';

const day = (date: string, patch: Partial<AttendanceRecordDto> = {}): AttendanceRecordDto => ({
  date,
  dayType: 'FullDay',
  calendarRemark: null,
  status: 'Present',
  reason: null,
  clockIn: `${date}T08:59:03+08:00`,
  clockOut: `${date}T18:05:41+08:00`,
  expectedClockOut: `${date}T18:00:00+08:00`,
  lateSeconds: 0,
  earlyLeaveSeconds: 0,
  formState: 'None',
  formSeconds: 0,
  adjustedClockOut: null,
  coverage: null,
  shortSeconds: 0,
  gaps: [],
  forms: [],
  ...patch,
});

const records = (patch: Partial<AttendanceRecordsDto> = {}): AttendanceRecordsDto => {
  const data: AttendanceRecordsDto = {
    from: '2026-10-01',
    to: '2026-10-31',
    today: '2026-10-22',
    calendarAvailable: true,
    formsAvailable: true,
    total: 0,
    offset: 0,
    people: [
      {
        userId: 'u1',
        cardNumber: null,
        displayName: '王小明',
        account: 'ming',
        formsAvailable: true,
        hidden: false,
        days: [
          day('2026-10-01', { lateSeconds: 725, coverage: 'Unexplained' }),
          day('2026-10-02', {
            status: 'Anomaly',
            reason: 'NoPunchOrLeave',
            clockIn: null,
            clockOut: null,
            expectedClockOut: null,
            coverage: 'Unexplained',
          }),
        ],
      },
    ],
    ...patch,
  };
  return { ...data, total: patch.total ?? data.people.length };
};

/** 只看得到自己時，人員選單只有本人。 */
const option = (patch: Partial<AttendancePersonOptionDto>): AttendancePersonOptionDto => ({
  userId: null,
  cardNumber: null,
  displayName: '',
  account: null,
  hidden: false,
  holding: true,
  ...patch,
});
const self = [option({ userId: 'u1', displayName: '王小明', account: 'ming' })];
/** 全公司範圍：含不持卡、卡已隱藏的人，以及只有姓名的持有人（依卡號）。 */
const company = [
  ...self,
  option({
    userId: 'u2',
    displayName: '陳小華',
    account: 'hua',
    hidden: true,
    holding: false,
  }),
  option({ cardNumber: '3368', displayName: '陳怡君' }),
];

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

function setup(
  read: () => Promise<AttendanceRecordsDto>,
  features = ['attendance.records'],
  people = self,
) {
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
    records: vi.fn((_query: AttendanceRecordFilters, _offset: number, _limit: number) => read()),
    people: vi.fn(async () => people),
    rules: vi.fn(async () => rules),
    exportRecords: vi.fn(async (_query: AttendanceRecordFilters) => new Response('')),
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
    // 小計照狀態標籤數天數，只列有的。
    expect(
      [...root.querySelector('.records-person-summary')!.children].map((item) =>
        item.textContent?.trim(),
      ),
    ).toEqual(['遲到 1 天', '未刷卡 1 天']);
    const rows = root.querySelectorAll('.records-person-group tr:not(.records-person-row)');
    expect(rows).toHaveLength(2);
    expect(rows[0].textContent).toContain('08:59:03');
    expect(rows[0].textContent).toContain('12 分 5 秒');
    // 狀態標籤寫發生什麼事，原因放在 title。
    const status = rows[1].querySelector('td[nxTableColumn="status"] nx-status-badge')!;
    expect(status.textContent?.trim()).toBe('未刷卡');
    expect(status.getAttribute('title')).toBe('沒有刷卡，也沒有假單');
    // 人員欄只在第一天出現、跨過他的每一天，並列出遲到、早退與異常天數（0 天也列）。
    const person = rows[0].querySelector('.records-person-cell')!;
    expect(person.getAttribute('rowspan')).toBe('2');
    expect(person.textContent).toContain('王小明');
    expect(person.textContent).toContain('未刷卡 1 天');
    expect(rows[1].querySelector('.records-person-cell')).toBeNull();
    // 遲到的上班直接標色；沒刷卡也沒表單補足在上下班格標「未刷卡」。
    expect(rows[0].querySelector('.records-time-flag')?.textContent).toContain('08:59:03');
    expect(rows[1].querySelectorAll('.records-missing')).toHaveLength(2);
    expect(root.querySelector('.records-forms-unavailable')).toBeNull();
    expect(root.querySelector('.records-calendar-unavailable')).toBeNull();
    // 查詢列只有區間與含放假日（只看得到自己時沒有人員選單）。
    expect(root.querySelectorAll('nx-query-bar nx-checkbox')).toHaveLength(1);
  });

  it('marks forms and calendar as unavailable when the document system is down', async () => {
    const down = records({ calendarAvailable: false, formsAvailable: false });
    down.people[0].formsAvailable = false;
    down.people[0].days = [
      day('2026-10-05', {
        dayType: 'Unknown',
        status: 'Undetermined',
        reason: 'DayTypeUnknown',
        formState: 'Unavailable',
      }),
    ];
    const { fixture, root } = setup(async () => down);
    await fixture.whenStable();
    expect(root.querySelector('.records-forms-unavailable')?.textContent).toContain('表單無法取得');
    expect(root.querySelector('.records-calendar-unavailable')?.textContent).toContain(
      '假日行事曆無法取得',
    );
    const row = root.querySelector('.records-person-group tr:not(.records-person-row)')!;
    expect(row.textContent).toContain('無法判定');
    expect(row.querySelector('.records-unavailable')?.textContent).toContain('無法取得');
  });

  it('flags a person whose forms cannot be matched while the rest are available', async () => {
    const data = records();
    data.people.push({
      ...data.people[0],
      userId: 'u2',
      displayName: '陳小華',
      account: 'hua',
      formsAvailable: false,
      hidden: true,
      days: [day('2026-10-01', { formState: 'Unavailable' })],
    });
    const { fixture, root } = setup(async () => data);
    await fixture.whenStable();
    const groups = root.querySelectorAll('.records-person-row');
    expect(groups[0].textContent).not.toContain('表單無法取得');
    expect(groups[1].textContent).toContain('表單無法取得');
    expect(groups[1].textContent).toContain('已隱藏');
    // 沒有卡號對應頁的權限時只標示，不給連結。
    expect(groups[1].querySelector('a.records-badge-link')).toBeNull();
    expect(root.querySelector('.records-forms-unavailable')).toBeNull();
  });

  it('offers units only to accounts that can read the organization', async () => {
    const { fixture, organization } = setup(
      async () => records(),
      ['attendance.records', 'organization'],
    );
    await fixture.whenStable();
    expect(organization.units).toHaveBeenCalledOnce();
    expect(fixture.componentInstance.unitNodes()).not.toBeNull();
  });

  it('picks units from the tree, all units first and the parents as the trigger path', async () => {
    const { fixture, organization } = setup(
      async () => records(),
      ['attendance.records', 'organization'],
    );
    const unit = (id: string, name: string, parentId: string | null, sortOrder = 0) => ({
      id,
      name,
      parentId,
      sortOrder,
      enabled: true,
    });
    organization.units.mockResolvedValueOnce([
      unit('c', '星河科技股份有限公司（總公司）', null),
      unit('r', '研發處', 'c'),
      unit('p', '平台部', 'r'),
    ] as never);
    await fixture.whenStable();
    expect(fixture.componentInstance.unitNodes()!.map((node) => [node.label, node.parent])).toEqual(
      [
        ['星河科技股份有限公司（總公司）', null],
        ['研發處', 'c'],
        ['平台部', 'r'],
      ],
    );
    fixture.componentInstance.query.set('unitId', 'p');
    fixture.detectChanges();
    const trigger = (fixture.nativeElement as HTMLElement).querySelector(
      '.records-unit [role="combobox"]',
    )!;
    expect(trigger.getAttribute('aria-label')).toBe('單位（含下層）');
    expect(trigger.getAttribute('title')).toBe('星河科技股份有限公司（總公司） › 研發處 › 平台部');
  });

  it('picks people from the roster list, including holders without an account', async () => {
    const { fixture, root, api } = setup(async () => records(), ['attendance.records'], company);
    await fixture.whenStable();
    const options = fixture.componentInstance.personOptions();
    expect(options.map((option) => option.label)).toEqual(['所有人', '王小明', '陳小華', '陳怡君']);
    expect(options[2].description).toBe('hua・已不持卡・已隱藏');
    expect(options[3].description).toBe('卡號 3368・尚未綁帳號');

    fixture.componentInstance.query.commit({ person: options[3].value });
    await fixture.whenStable();
    const [query] = api.records.mock.calls.at(-1)!;
    expect(query.card).toBe('3368');
    expect(query.userId).toBeUndefined();
    expect(root.querySelector('.query-chip, nx-query-bar')?.textContent).toContain('陳怡君');
  });

  it('puts the unit before the person and lists only the people of the chosen unit', async () => {
    const { fixture, root, api } = setup(
      async () => records(),
      ['attendance.records', 'organization'],
      company,
    );
    await fixture.whenStable();
    const fields = [...root.querySelectorAll('nx-query-bar .ui-field')].map((field) =>
      field.classList.contains('records-unit')
        ? 'unit'
        : field.classList.contains('records-person')
          ? 'person'
          : '',
    );
    expect(fields.filter(Boolean)).toEqual(['unit', 'person']);
    expect(api.people).toHaveBeenLastCalledWith('', expect.anything());

    // 換單位就清掉已選的人並依單位重讀；清回所有單位時保留選的人。
    const page = fixture.componentInstance;
    page.query.set('person', 'u2');
    page.query.set('unitId', 'unit-1');
    await fixture.whenStable();
    expect(page.draft().person).toBe('');
    expect(api.people).toHaveBeenLastCalledWith('unit-1', expect.anything());
    page.query.set('person', 'u1');
    page.query.set('unitId', '');
    expect(page.draft().person).toBe('u1');
  });

  it('marks holders without an account and shows the calendar remark of a day off', async () => {
    const data = records();
    data.people[0] = {
      ...data.people[0],
      userId: null,
      cardNumber: '3368',
      displayName: '陳怡君',
      account: null,
      formsAvailable: false,
      days: [
        day('2026-10-10', {
          dayType: 'DayOff',
          calendarRemark: '國慶日',
          status: 'DayOff',
          clockIn: null,
          clockOut: null,
          expectedClockOut: null,
        }),
      ],
    };
    const { fixture, root } = setup(async () => data);
    await fixture.whenStable();
    const head = root.querySelector('.records-person-row')!;
    expect(head.textContent).toContain('尚未綁帳號');
    expect(head.textContent).toContain('卡號 3368');
    // 沒有帳號本來就查不到表單，不重複標「表單無法取得」。
    expect(head.textContent).not.toContain('表單無法取得');
    expect(root.querySelector('.records-remark')?.textContent).toContain('國慶日');
  });

  it('marks a person on hidden cards and links to them when the cards page is granted', async () => {
    const data = records();
    data.people[0] = { ...data.people[0], hidden: true };
    const { fixture, root } = setup(async () => data, ['attendance.records', 'attendance.cards']);
    await fixture.whenStable();
    const link = root.querySelector('.records-person-row a.records-badge-link')!;
    expect(link.textContent).toContain('已隱藏');
    expect(link.getAttribute('href')).toContain('status=hidden');
    expect(link.getAttribute('href')).toContain('search=');
  });

  it('pages by person and returns to the first page when the filters change', async () => {
    const { fixture, root, api } = setup(async () => records({ total: 45 }));
    await fixture.whenStable();
    expect(api.records.mock.calls[0].slice(1, 3)).toEqual([0, 20]);
    expect(root.querySelector('.ui-count-label')?.textContent).toContain('45 人');
    expect(root.querySelector('nx-table-pagination')).not.toBeNull();

    fixture.componentInstance.changePage(1);
    await fixture.whenStable();
    expect(api.records.mock.calls.at(-1)!.slice(1, 3)).toEqual([20, 20]);
    fixture.componentInstance.changePageSize(50);
    await fixture.whenStable();
    expect(api.records.mock.calls.at(-1)!.slice(1, 3)).toEqual([0, 50]);

    fixture.componentInstance.changePage(1);
    fixture.componentInstance.query.commit({ includeDaysOff: true });
    await fixture.whenStable();
    const [query, offset] = api.records.mock.calls.at(-1)!;
    expect([query.includeDaysOff, offset]).toEqual([true, 0]);
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
    // 匯出期間按鈕轉圈並擋下重複送出；用 aria-disabled 而不是 disabled，焦點才留在按鈕上。
    expect(button.disabled).toBe(false);
    expect(button.getAttribute('aria-busy')).toBe('true');
    expect(button.getAttribute('aria-disabled')).toBe('true');
    expect(button.textContent).toContain('匯出中');
    button.click();
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
    expect(button.getAttribute('aria-busy')).toBeNull();
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

  it('narrows the table to unexplained days from the switch before the query fields', async () => {
    const { fixture, root, api } = setup(async () => records());
    await fixture.whenStable();
    expect(api.records.mock.calls[0][0].coverage).toBeUndefined();
    expect(root.querySelector('.records-person-flags')?.textContent).toContain('未補足 2 天');

    const unexplained = [
      ...root.querySelectorAll<HTMLButtonElement>('.records-coverage button'),
    ].find((button) => button.textContent?.includes('未補足'))!;
    unexplained.click();
    await fixture.whenStable();
    expect(api.records.mock.calls.at(-1)![0].coverage).toBe('Unexplained');
  });

  it('shows each form as a card and the day as waiting on a form', async () => {
    const data = records();
    data.people[0].days = [
      day('2026-10-05', {
        clockIn: '2026-10-05T09:03:06+08:00',
        lateSeconds: 186,
        earlyLeaveSeconds: 900,
        clockOut: '2026-10-05T17:45:00+08:00',
        expectedClockOut: '2026-10-05T18:00:00+08:00',
        formState: 'Filed',
        formSeconds: 1800,
        adjustedClockOut: '2026-10-05T18:03:06+08:00',
        coverage: 'Pending',
        shortSeconds: 1086,
        gaps: [
          {
            kind: 'Late',
            startsAt: '2026-10-05T09:00:00+08:00',
            endsAt: '2026-10-05T09:03:06+08:00',
          },
          {
            kind: 'EarlyLeave',
            startsAt: '2026-10-05T17:45:00+08:00',
            endsAt: '2026-10-05T18:00:00+08:00',
          },
        ],
        forms: [
          {
            desNo: 'L-1',
            kind: 'Leave',
            title: '特休',
            startsAt: '2026-10-05T08:30:00+08:00',
            endsAt: '2026-10-05T09:00:00+08:00',
            flowStatus: 'Decide',
            currentSigner: null,
            currentStep: null,
            approved: true,
          },
          {
            desNo: 'L-2',
            kind: 'Leave',
            title: '事假',
            startsAt: '2026-10-05T17:45:00+08:00',
            endsAt: '2026-10-05T18:30:00+08:00',
            flowStatus: 'Send',
            currentSigner: '林雅婷',
            currentStep: '主管',
            approved: false,
          },
        ],
      }),
    ];
    const { fixture, root } = setup(async () => data);
    await fixture.whenStable();
    const row = root.querySelector('.records-person-group tr:not(.records-person-row)')!;
    const forms = row.querySelectorAll('nx-gd-form-list li');
    expect([...forms].map((form) => form.getAttribute('data-flow'))).toEqual(['Decide', 'Send']);
    expect(forms[0].textContent).toContain('L-1');
    expect(forms[0].textContent).toContain('08:30–09:00');
    expect(forms[1].textContent).toContain('等待 林雅婷（主管）');
    // 狀態欄只有標籤；補足欄寫差額、狀態與缺口時段，緊鄰表單欄對照。
    expect(
      [...row.querySelectorAll('td[nxTableColumn="status"] nx-status-badge')].map((badge) =>
        badge.textContent?.trim(),
      ),
    ).toEqual(['遲到', '早退']);
    const gap = row.querySelector('.records-gap-cell')!;
    expect(gap.getAttribute('data-tone')).toBe('warning');
    expect(gap.querySelector('.records-short')?.textContent?.trim()).toBe('差 18 分 6 秒');
    expect(gap.querySelector('.records-gap-head nx-status-badge')?.textContent?.trim()).toBe(
      '流程中',
    );
    expect(
      [...gap.querySelectorAll('.records-gaps li')].map(
        (item) =>
          `${item.querySelector('span')?.textContent} ${item.querySelector('time')?.textContent}`,
      ),
    ).toEqual(['遲到 09:00–09:03:06', '早退 17:45–18:00']);
    expect(row.querySelector('.records-adjusted')?.textContent?.trim()).toBe('扣表單 18:03:06');
    expect(row.querySelector('.records-time-flag')?.getAttribute('title')).toBe(
      '遲到 3 分 6 秒・流程中',
    );
    expect(root.querySelector('.records-person-flags')?.textContent).toContain('流程中 1 天');
  });

  it('does not offer an export when nobody is listed', async () => {
    const { fixture, root } = setup(async () => records({ people: [] }));
    await fixture.whenStable();
    expect(root.querySelector<HTMLButtonElement>('.records-export')!.disabled).toBe(true);
  });
});
