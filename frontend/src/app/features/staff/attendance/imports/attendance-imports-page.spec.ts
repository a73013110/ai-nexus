import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { afterEach, beforeAll, describe, expect, it, vi } from 'vitest';
import type { PunchImportDto } from '@core/api/schema';
import { AuthService } from '@core/auth/auth-service';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { ApiError } from '@core/errors/safe-errors';
import { ToastService } from '@shared/ui/toast-service';
import { AttendanceApi } from '../attendance-api';
import { AttendanceImportsPage } from './attendance-imports-page';
import { ReimportDialog } from './reimport-dialog';

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
  ...patch,
});

function setup({
  features = ['attendance.imports', 'attendance.imports.reimport'],
  reimport = async (_date: string): Promise<PunchImportDto> => item('new'),
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
    testing: signal(false),
    testingNote: signal(''),
  };
  const api = {
    imports: vi.fn(async (_filters: object, _take: number, _before?: string) => [
      item('a', {
        status: 'failed',
        punchCount: 0,
        errorMessage: '第 3 行：時間格式錯誤',
        triggerKind: 'manual',
        requestedByName: '人事專員',
      }),
      item('b'),
    ]),
    reimport: vi.fn(reimport),
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
  });

  it('lists imports with file, result, count and who triggered them', async () => {
    const { fixture, root, api } = setup();
    await fixture.whenStable();
    expect(api.imports).toHaveBeenCalledWith({}, 50, undefined, expect.anything());
    const rows = root.querySelectorAll('.imports-table tbody tr');
    expect(rows).toHaveLength(2);
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

  it('reports a failed reimport on the page and rereads the first page', async () => {
    const { fixture, root, api, dialog } = setup({
      reimport: async () =>
        item('new', { status: 'failed', errorMessage: '第 1 行：欄位數不正確' }),
    });
    await fixture.whenStable();
    root.querySelector<HTMLButtonElement>('.imports-reimport')!.click();
    dialog().date.set('2026-10-01');
    await dialog().submit(new Event('submit'));
    await fixture.whenStable();
    expect(api.reimport).toHaveBeenCalledWith('2026-10-01');
    expect(root.querySelector('.imports-failure')?.textContent).toContain('重匯 20261001.txt 失敗');
    expect(root.querySelector('.imports-failure')?.textContent).toContain('欄位數不正確');
    expect(api.imports).toHaveBeenCalledTimes(2);
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
});
