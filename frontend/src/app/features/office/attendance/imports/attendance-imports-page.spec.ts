import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { afterEach, beforeAll, describe, expect, it, vi } from 'vitest';
import type { PunchDto, PunchImportDto, PunchReimportResult } from '@core/api/schema';
import { AuthService } from '@core/auth/auth-service';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { ApiError } from '@core/errors/safe-errors';
import { tourSteps } from '@shared/ui/product-tour';
import { ToastService } from '@shared/ui/toast-service';
import { AttendanceApi } from '../attendance-api';
import { AttendanceImportsPage } from './attendance-imports-page';
import { ReimportDialog, reimportOutcome } from './reimport-dialog';

const item = (id: string, patch: Partial<PunchImportDto> = {}): PunchImportDto => ({
  id,
  punchFileId: 'f-' + id,
  fileName: '20261001.txt',
  triggerKind: 'scheduled',
  requestedById: null,
  requestedByName: null,
  startedAt: '2026-10-01T15:52:00Z',
  finishedAt: '2026-10-01T15:52:01Z',
  sourceModifiedAt: '2026-10-01T15:52:00Z',
  sourceSize: 2048,
  punchCount: 1234,
  status: 'succeeded',
  errorMessage: null,
  isCurrent: true,
  ...patch,
});
const punch = (id: number, patch: Partial<PunchDto> = {}): PunchDto => ({
  id,
  cardNumber: 'A001',
  punchedAt: '2026-10-01T00:00:00Z',
  machineCode: '001',
  eventCode: '11',
  unattributed: false,
  userId: 'u2',
  holder: '王小明',
  ...patch,
});

function setup({
  features = ['attendance.imports', 'attendance.imports.reimport'],
  reimport = async (_from: string, _to: string): Promise<PunchReimportResult> => ({
    imports: [item('new')],
    missingFiles: [],
  }),
} = {}) {
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
    featureName: () => '打卡匯入紀錄',
  };
  const api = {
    imports: vi.fn(async (_filters: object, _table: object) => ({
      items: [
        item('a', {
          status: 'failed',
          punchCount: 0,
          errorMessage: '第 3 行：時間格式錯誤',
          triggerKind: 'manual',
          requestedByName: '人事專員',
          isCurrent: false,
        }),
        item('b'),
        item('c', { fileName: '20260930.txt', isCurrent: false }),
      ],
      total: 3,
      offset: 0,
      complete: true,
    })),
    reimport: vi.fn(reimport),
    importPunches: vi.fn(async (_id: string, _table: object) => ({
      items: [
        punch(2, { cardNumber: 'B002', punchedAt: '2026-10-01T10:05:00Z' }),
        punch(1, { unattributed: true, userId: null, holder: null }),
      ],
      total: 2,
      offset: 0,
      complete: true,
    })),
  };
  const toast = { success: vi.fn() };
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      { provide: WorkspaceSession, useValue: session },
      { provide: AuthService, useValue: { generation, guard: () => () => true } },
      { provide: AttendanceApi, useValue: api },
      { provide: ToastService, useValue: toast },
    ],
  });
  const fixture = TestBed.createComponent(AttendanceImportsPage);
  const root = fixture.nativeElement as HTMLElement;
  const dialog = () =>
    fixture.debugElement.query((node) => node.componentInstance instanceof ReimportDialog)
      .componentInstance as ReimportDialog;
  return { fixture, root, api, toast, dialog };
}

describe('AttendanceImportsPage', () => {
  beforeAll(() => {
    // jsdom 沒有捲動；換頁時表格捲回開頭。
    Element.prototype.scrollTo ??= function () {};
    HTMLDialogElement.prototype.show ??= function (this: HTMLDialogElement) {
      this.setAttribute('open', '');
    };
    HTMLDialogElement.prototype.showModal ??= function (this: HTMLDialogElement) {
      this.setAttribute('open', '');
    };
    HTMLDialogElement.prototype.close ??= function (this: HTMLDialogElement) {
      this.removeAttribute('open');
    };
  });
  afterEach(() => {
    TestBed.resetTestingModule();
    vi.unstubAllGlobals();
    vi.useRealTimers();
  });

  it('lists imports with file, result, count and who triggered them', async () => {
    const { fixture, root, api } = setup();
    await fixture.whenStable();
    expect(api.imports).toHaveBeenCalledWith(
      {},
      { offset: 0, limit: 50, sort: 'startedAt:desc' },
      expect.anything(),
    );
    const rows = root.querySelectorAll('.imports-table tbody tr');
    expect(rows).toHaveLength(3);
    expect(rows[0].textContent).toContain('20261001.txt');
    expect(rows[0].textContent).toContain('失敗');
    expect(rows[0].textContent).toContain('第 3 行：時間格式錯誤');
    expect(rows[0].textContent).toContain('手動重匯');
    expect(rows[0].textContent).toContain('人事專員');
    expect(rows[1].textContent).toContain('成功');
    expect(rows[1].textContent).toContain('1,234');
    expect(root.querySelector('.imports-reimport')).not.toBeNull();
  });

  it('hides the reimport button without the reimport action', async () => {
    const { fixture, root } = setup({ features: ['attendance.imports'] });
    await fixture.whenStable();
    expect(root.querySelector('.imports-reimport')).toBeNull();
  });

  it('opens a current import and lists the punches it wrote oldest first', async () => {
    const { fixture, root, api } = setup();
    await fixture.whenStable();
    root.querySelectorAll<HTMLButtonElement>('.imports-table [data-row-action]')[1].click();
    await fixture.whenStable();
    expect(api.importPunches).toHaveBeenCalledWith(
      'b',
      { offset: 0, limit: 50, sort: 'punchedAt' },
      expect.anything(),
    );
    const drawer = root.querySelector('nx-detail-drawer')!;
    expect(drawer.querySelector('.ui-drawer-summary h3')?.textContent).toContain('20261001.txt');
    // 刷卡表格在基本資訊與關聯之後，吃剩下的高度到抽屜底。
    expect(drawer.querySelector('.ui-drawer-sections > :last-child')?.tagName).toBe(
      'NX-PUNCH-TABLE',
    );
    const rows = [...drawer.querySelectorAll('nx-punch-table tbody tr')];
    // 已一次讀完：依刷卡時間在本機由早到晚排。
    expect(rows.map((row) => row.querySelector('.punch-table-card')?.textContent?.trim())).toEqual([
      'A001',
      'B002',
    ]);
    expect(rows[0].textContent).toContain('無持有人');
    expect(rows[1].textContent).toContain('王小明');
    expect(drawer.textContent).toContain('2.0 KB');
  });

  it('shows why a failed import wrote nothing without reading punches', async () => {
    const { fixture, root, api } = setup();
    await fixture.whenStable();
    root.querySelector<HTMLButtonElement>('.imports-table [data-row-action]')!.click();
    await fixture.whenStable();
    const drawer = root.querySelector('nx-detail-drawer')!;
    expect(drawer.querySelector('nx-notice')?.textContent).toContain('第 3 行：時間格式錯誤');
    expect(drawer.querySelector('nx-punch-table')).toBeNull();
    expect(api.importPunches).not.toHaveBeenCalled();
  });

  it('points a replaced import to the history of its file', async () => {
    const { fixture, root, api } = setup();
    await fixture.whenStable();
    root.querySelectorAll<HTMLButtonElement>('.imports-table [data-row-action]')[2].click();
    await fixture.whenStable();
    const drawer = root.querySelector('nx-detail-drawer')!;
    expect(drawer.textContent).toContain('刷卡已換成最新一次的內容');
    drawer.querySelector<HTMLButtonElement>('.imports-replaced button')!.click();
    await fixture.whenStable();
    expect(api.importPunches).not.toHaveBeenCalled();
    expect(api.imports).toHaveBeenLastCalledWith(
      { fileName: '20260930.txt' },
      expect.anything(),
      expect.anything(),
    );
  });

  it('reimports a single file by default and reports a failure on the page', async () => {
    const { fixture, root, api, dialog, toast } = setup({
      reimport: async () => ({
        imports: [item('new', { status: 'failed', errorMessage: '第 1 行：欄位數不正確' })],
        missingFiles: [],
      }),
    });
    await fixture.whenStable();
    root.querySelector<HTMLButtonElement>('.imports-reimport')!.click();
    expect(dialog().mode()).toBe('single');
    dialog().date.set('2026-10-01');
    fixture.detectChanges();
    expect(root.querySelector('nx-reimport-dialog .form-note')?.textContent).toContain(
      '重匯 20261001.txt。',
    );
    await dialog().submit(new Event('submit'));
    await fixture.whenStable();
    expect(api.reimport).toHaveBeenCalledWith('2026-10-01', '2026-10-01');
    expect(root.querySelector('.imports-failure')?.textContent).toContain('重匯 20261001.txt 失敗');
    expect(root.querySelector('.imports-failure')?.textContent).toContain('欄位數不正確');
    expect(toast.success).not.toHaveBeenCalled();
    expect(api.imports).toHaveBeenCalledTimes(2);
  });

  it('switches to a date range, shows the files it covers and summarises the result', async () => {
    vi.useFakeTimers({ toFake: ['Date'] });
    // 台灣 10/22 上午。
    vi.setSystemTime(new Date('2026-10-22T02:00:00Z'));
    const { fixture, root, api, dialog, toast } = setup({
      reimport: async () => ({
        imports: [
          item('n1', { fileName: '20261001.txt', punchCount: 1000 }),
          item('n2', { fileName: '20261002.txt', punchCount: 234 }),
        ],
        missingFiles: ['20261003.txt'],
      }),
    });
    await fixture.whenStable();
    root.querySelector<HTMLButtonElement>('.imports-reimport')!.click();
    dialog().date.set('2026-10-01');
    dialog().switchMode('range');
    fixture.detectChanges();
    // 從單一檔案選的日期開始，到今天。
    expect([dialog().from(), dialog().to()]).toEqual(['2026-10-01', '2026-10-22']);
    expect(root.querySelectorAll('nx-reimport-dialog nx-date-time-picker')).toHaveLength(2);
    expect(root.querySelector('nx-reimport-dialog .form-note')?.textContent).toContain(
      '重匯 20261001.txt～20261022.txt，共 22 個檔案。',
    );
    await dialog().submit(new Event('submit'));
    await fixture.whenStable();
    expect(api.reimport).toHaveBeenCalledWith('2026-10-01', '2026-10-22');
    expect(toast.success).toHaveBeenCalledWith(
      '已重匯 2 個檔案',
      '1,234 筆刷卡；找不到 20261003.txt。',
    );
    expect(root.querySelector('.imports-failure')).toBeNull();
    expect(api.imports).toHaveBeenCalledTimes(2);
  });

  it('blocks a reversed or overlong range before sending it', async () => {
    const { fixture, root, api, dialog } = setup();
    await fixture.whenStable();
    root.querySelector<HTMLButtonElement>('.imports-reimport')!.click();
    dialog().switchMode('range');
    dialog().from.set('2026-09-01');
    dialog().to.set('2026-10-02');
    fixture.detectChanges();
    expect(dialog().rangeError()).toContain('一次最多重匯 31 天');
    expect(
      root.querySelector<HTMLButtonElement>('nx-reimport-dialog button[type=submit]')!.disabled,
    ).toBe(true);
    dialog().from.set('2026-10-03');
    fixture.detectChanges();
    expect(root.querySelector('nx-reimport-dialog .field-error')?.textContent).toContain(
      '結束日期不能早於開始日期',
    );
    await dialog().submit(new Event('submit'));
    expect(api.reimport).not.toHaveBeenCalled();
    dialog().to.set('2026-10-31');
    expect(dialog().range()).toEqual({ from: '2026-10-03', to: '2026-10-31', count: 29 });
  });

  it('keeps a missing file or unreadable folder inside the dialog with the reason', async () => {
    const { fixture, root, dialog, toast } = setup({
      reimport: async () => {
        throw new ApiError(
          503,
          'attendance_import_source_unavailable',
          undefined,
          'NX-' + 'B'.repeat(32),
        );
      },
    });
    await fixture.whenStable();
    root.querySelector<HTMLButtonElement>('.imports-reimport')!.click();
    await dialog().submit(new Event('submit'));
    expect(dialog().error()).toContain('讀不到打卡檔資料夾');
    expect(toast.success).not.toHaveBeenCalled();
    expect(root.querySelector('.imports-failure')).toBeNull();
  });

  it('finds every tour target on the page and in both reimport modes', async () => {
    const { fixture, root, dialog } = setup();
    await fixture.whenStable();
    expect(root.querySelector('nx-feature-page nx-product-tour-button')).not.toBeNull();
    for (const step of tourSteps(fixture.componentInstance.tour()))
      expect(
        root.querySelector(step.target.replace(/^nx-attendance-imports-page /, '')),
        step.target,
      ).not.toBeNull();
    root.querySelector<HTMLButtonElement>('.imports-reimport')!.click();
    for (const mode of ['single', 'range'] as const) {
      dialog().switchMode(mode);
      fixture.detectChanges();
      expect(root.querySelector('nx-reimport-dialog nx-product-tour-button')).not.toBeNull();
      for (const step of tourSteps(dialog().tour()))
        expect(root.querySelector(step.target), step.target).not.toBeNull();
    }
  });

  it('summarises mixed results: successes in a toast, failures and missing files on the page', () => {
    const failed = (id: string, fileName: string) =>
      item(id, { fileName, status: 'failed', punchCount: 0, errorMessage: '格式錯誤' });
    expect(
      reimportOutcome({
        imports: [
          item('a', { fileName: '20261001.txt', punchCount: 5 }),
          failed('b', '20261002.txt'),
        ],
        missingFiles: [],
      }),
    ).toEqual({
      toast: { title: '已重匯 20261001.txt', description: '5 筆刷卡' },
      failure: '重匯 20261002.txt 失敗，刷卡維持上次成功的內容。原因：格式錯誤',
    });
    expect(
      reimportOutcome({
        imports: [failed('a', '20261001.txt'), failed('b', '20261002.txt')],
        missingFiles: ['20261003.txt', '20261004.txt', '20261005.txt', '20261006.txt'],
      }),
    ).toEqual({
      toast: undefined,
      failure:
        '重匯 20261001.txt、20261002.txt 失敗，刷卡維持上次成功的內容。原因見各檔的匯入紀錄。' +
        '找不到 20261003.txt、20261004.txt、20261005.txt 等 4 個檔案。',
    });
  });
});
