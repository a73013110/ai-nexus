import { ApplicationRef, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeAll, describe, expect, it, vi } from 'vitest';
import type { DirectoryOutcomeDto, DirectoryPersonDto } from '@core/api/schema';
import { AuthService } from '@core/auth/auth-service';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { ViewScope } from '@shared/browser/view-scope';
import { tourSteps } from '@shared/ui/product-tour';
import { AdminUsersStore } from '../admin-users-store';
import { IdentityApi } from '../identity-api';
import { DirectoryImportDialog } from './directory-import-dialog';
import { DirectoryOutcomeTable } from './directory-outcome-table';

const person = (sid: string, displayName: string, state = 'new'): DirectoryPersonDto => ({
  sid,
  account: displayName.toLowerCase(),
  displayName,
  email: null,
  department: '研發處',
  title: null,
  path: '研發處 › 平台部',
  disabled: false,
  state,
  userId: null,
});

const outcome = (
  sid: string,
  value: string,
  extra: Partial<DirectoryOutcomeDto> = {},
): DirectoryOutcomeDto => ({
  sid,
  userId: null,
  account: '',
  displayName: '',
  email: null,
  path: '',
  directoryDisabled: false,
  enabled: null,
  outcome: value,
  changes: [],
  errorCode: null,
  ...extra,
});

/** 目錄依關鍵字回不同的人，用來驗證跨搜尋累積。 */
const directory: Record<string, DirectoryPersonDto[]> = {
  研發: [person('S-1', 'Carol'), person('S-2', 'Dave', 'conflict')],
  業務: [person('S-3', 'Erin', 'linked')],
};

function setup({ render = false } = {}) {
  const generation = signal(1);
  const api = {
    searchDirectory: vi.fn(async (search: string) => ({
      people: directory[search] ?? [],
      more: false,
    })),
    previewDirectoryImport: vi.fn(async ({ includeDisabled }: { includeDisabled?: boolean }) => ({
      items: [
        outcome('S-1', 'create'),
        outcome('S-3', includeDisabled ? 'update' : 'disabled', {
          directoryDisabled: true,
          changes: includeDisabled
            ? [{ field: 'displayName', before: 'Erin', after: '陳艾琳' }]
            : [],
        }),
      ],
    })),
    importDirectoryUsers: vi.fn(async () => ({
      items: [
        outcome('S-1', 'create'),
        outcome('S-3', 'failed', { errorCode: 'directory_changed' }),
      ],
    })),
  };
  const store = { readUsers: vi.fn(), toast: { success: vi.fn() } };
  TestBed.configureTestingModule({
    providers: [
      { provide: IdentityApi, useValue: api },
      { provide: AdminUsersStore, useValue: store },
      { provide: ViewScope, useValue: { later: (work: () => void) => work(), cancel: vi.fn() } },
      {
        provide: WorkspaceSession,
        useValue: { auth: { generation }, load: vi.fn(async () => true), has: () => true },
      },
      { provide: AuthService, useValue: { generation, guard: () => () => true } },
    ],
  });
  // 選取、預覽與匯入的流程在類別上驗證；結果清單的呈現另外渲染 DirectoryOutcomeTable。
  if (!render)
    TestBed.overrideComponent(DirectoryImportDialog, {
      set: { template: '<dialog #dialog></dialog>' },
    });
  const fixture = TestBed.createComponent(DirectoryImportDialog);
  fixture.detectChanges();
  const settle = async () => {
    await TestBed.inject(ApplicationRef).whenStable();
    fixture.detectChanges();
  };
  const root = fixture.nativeElement as HTMLElement;
  return { dialog: fixture.componentInstance, api, store, settle, root };
}

describe('DirectoryImportDialog', () => {
  beforeAll(() => {
    HTMLDialogElement.prototype.showModal ??= function (this: HTMLDialogElement) {
      this.setAttribute('open', '');
    };
    HTMLDialogElement.prototype.close ??= function (this: HTMLDialogElement) {
      this.removeAttribute('open');
    };
  });
  afterEach(() => TestBed.resetTestingModule());

  it('keeps people picked in earlier searches and skips the ones the server would refuse', async () => {
    const { dialog, api, settle } = setup();
    dialog.open();
    dialog.find('研發');
    await settle();
    expect(api.searchDirectory).toHaveBeenLastCalledWith('研發', expect.anything());
    dialog.toggleAll(true);
    expect([...dialog.selection().keys()]).toEqual(['S-1']);
    expect(dialog.allChecked()).toBe(true);

    dialog.find('業務');
    await settle();
    expect(dialog.people().map((x) => x.sid)).toEqual(['S-3']);
    dialog.toggle(dialog.people()[0], true);
    expect(dialog.selected().map((x) => x.displayName)).toEqual(['Carol', 'Erin']);
  });

  it('previews the selection, asks again with disabled accounts and stays on the result when someone failed', async () => {
    const { dialog, api, store, settle } = setup();
    dialog.open();
    dialog.find('研發');
    await settle();
    dialog.toggle(dialog.people()[0], true);
    dialog.find('業務');
    await settle();
    dialog.toggle(dialog.people()[0], true);

    await dialog.preview();
    expect(api.previewDirectoryImport).toHaveBeenLastCalledWith({
      sids: ['S-1', 'S-3'],
      includeDisabled: false,
    });
    expect(dialog.step()).toBe('preview');
    expect(dialog.summary()).toBe('新建 1、AD 已停用，未匯入 1');
    expect(dialog.disabledCount()).toBe(1);
    expect(dialog.pending()).toBe(1);

    await dialog.preview(true);
    expect(dialog.includeDisabled()).toBe(true);
    expect(dialog.pending()).toBe(2);

    await dialog.confirm();
    expect(api.importDirectoryUsers).toHaveBeenCalledWith({
      sids: ['S-1', 'S-3'],
      includeDisabled: true,
    });
    expect(store.readUsers).toHaveBeenCalled();
    expect(dialog.step()).toBe('result');
    expect(store.toast.success).not.toHaveBeenCalled();
  });
});

describe('DirectoryImportDialog tour', () => {
  beforeAll(() => {
    HTMLDialogElement.prototype.showModal ??= function (this: HTMLDialogElement) {
      this.setAttribute('open', '');
    };
  });
  afterEach(() => {
    TestBed.resetTestingModule();
    vi.unstubAllGlobals();
  });

  it('finds every step of the selection and the preview on screen', async () => {
    vi.stubGlobal('matchMedia', (query: string) => ({
      matches: false,
      media: query,
      addEventListener() {},
      removeEventListener() {},
    }));
    const { dialog, settle, root } = setup({ render: true });
    const expectTargets = () => {
      expect(root.querySelector('nx-dialog-header nx-product-tour-button')).not.toBeNull();
      for (const step of tourSteps(dialog.tour()))
        expect(
          root.querySelector(step.target.replace(/^nx-directory-import-dialog /, '')),
          step.target,
        ).not.toBeNull();
    };
    dialog.open();
    dialog.find('研發');
    await settle();
    dialog.toggle(dialog.people()[0], true);
    await settle();
    expectTargets();

    dialog.find('業務');
    await settle();
    dialog.toggle(dialog.people()[0], true);
    await dialog.preview();
    await settle();
    expect(tourSteps(dialog.tour()).map((step) => step.title)).toContain('確認匯入');
    expectTargets();
  });
});

describe('DirectoryOutcomeTable', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('lists each person with the outcome, the fields that change and what still needs a decision', () => {
    const fixture = TestBed.createComponent(DirectoryOutcomeTable);
    fixture.componentRef.setInput('items', [
      outcome('S-1', 'update', {
        displayName: '林可蘿',
        account: 'carol.lin',
        changes: [{ field: 'displayName', before: 'Carol', after: '林可蘿' }],
      }),
      outcome('S-9', 'not_found', { enabled: true }),
    ]);
    fixture.componentRef.setInput('known', new Map([['S-9', person('S-9', '吳大衛')]]));
    fixture.detectChanges();
    const rows = [...(fixture.nativeElement as HTMLElement).querySelectorAll('tbody tr')].map(
      (row) => row.textContent?.replace(/\s+/g, ' '),
    );
    expect(rows[0]).toContain('林可蘿');
    expect(rows[0]).toContain('更新');
    expect(rows[0]).toContain('姓名：Carol → 林可蘿');
    expect(rows[1]).toContain('吳大衛');
    expect(rows[1]).toContain('AD 找不到');
    expect(rows[1]).toContain('平台帳號仍啟用');
  });
});
