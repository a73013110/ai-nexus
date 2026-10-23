import { ApplicationRef, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeAll, describe, expect, it, vi } from 'vitest';
import type {
  DirectoryNodeDto,
  DirectoryOutcomeDto,
  DirectoryPersonDetailDto,
  DirectoryPersonDto,
} from '@core/api/schema';
import { AuthService } from '@core/auth/auth-service';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { ViewScope } from '@shared/browser/view-scope';
import { tourSteps } from '@shared/ui/product-tour';
import { AdminUsersStore } from '../admin-users-store';
import { IdentityApi } from '../identity-api';
import { DirectoryImportDialog } from './directory-import-dialog';
import { DirectoryOutcomeTable } from './directory-outcome-table';
import { withinDn } from './directory-outcomes';

const ROOT = 'DC=fixture,DC=internal';
const USERS = `CN=Users,${ROOT}`;
const EMPTY = `OU=空部門,${ROOT}`;
const RD = `OU=研發處,${ROOT}`;
const PLATFORM = `OU=平台部,${RD}`;

const node = (
  dn: string,
  name: string,
  people: number | null,
  hasChildren = false,
  kind = 'ou',
): DirectoryNodeDto => ({ dn, name, kind, people, hasChildren });

/** 假目錄：根下有人最多的 CN=Users、沒有人的 OU，以及有下層的研發處。 */
const tree: Record<string, { node: DirectoryNodeDto; children: DirectoryNodeDto[] }> = {
  [ROOT]: {
    node: node(ROOT, 'fixture.internal', 9, true, 'root'),
    children: [
      node(EMPTY, '空部門', 0),
      node(RD, '研發處', 3, true),
      node(USERS, 'Users', 6, false, 'container'),
    ],
  },
  [RD]: { node: node(RD, '研發處', 3, true), children: [node(PLATFORM, '平台部', 2)] },
};

const person = (
  sid: string,
  displayName: string,
  parent: string,
  state = 'new',
  extra: Partial<DirectoryPersonDto> = {},
): DirectoryPersonDto => ({
  sid,
  dn: `CN=${displayName},${parent}`,
  account: sid.toLowerCase(),
  upn: `${sid.toLowerCase()}@fixture.internal`,
  displayName,
  email: null,
  department: '研發處',
  title: null,
  path: '研發處 › 平台部',
  disabled: false,
  state,
  userId: null,
  changes: [],
  ...extra,
});

/** 完整屬性：A 鎖定中且密碼永不過期，其他人只有一般帳號。 */
const detail = (sid: string): DirectoryPersonDetailDto => ({
  sid,
  objectGuid: null,
  dn: `CN=${sid},${USERS}`,
  path: 'Users',
  account: sid.toLowerCase(),
  displayName: sid,
  givenName: null,
  surname: null,
  upn: null,
  email: null,
  employeeId: null,
  description: null,
  company: '示範公司',
  department: null,
  title: null,
  manager: null,
  office: null,
  phone: null,
  mobile: null,
  accountControl: sid === 'A' ? 0x10210 : 0x200,
  accountExpires: null,
  passwordLastSet: '2026-01-05T01:00:00Z',
  passwordMustChange: false,
  passwordExpires: null,
  lockoutTime: sid === 'A' ? '2026-10-20T01:00:00Z' : null,
  badPasswordCount: 3,
  badPasswordTime: null,
  lastLogon: null,
  lastLogonTimestamp: null,
  logonCount: 0,
  whenCreated: '2025-01-06T01:00:00Z',
  whenChanged: '2026-10-13T01:30:02Z',
  groups: ['研發群組'],
  moreGroups: false,
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

/** 姓氏筆畫由少到多，清單依中文排序後就是 A–F 的順序；D 是衝突（不可選），F 在 AD 已停用。 */
const usersPeople = () => [
  person('A', '王可蘿', USERS),
  person('B', '白大衛', USERS, 'changed', {
    changes: [{ field: 'displayName', before: 'David', after: '白大衛' }],
  }),
  person('C', '朱愛麗', USERS, 'synced'),
  person('D', '李志遠', USERS, 'conflict'),
  person('E', '周美玲', USERS, 'synced'),
  person('F', '洪子豪', USERS, 'new', { disabled: true }),
];
const rdPeople = () => [
  person('G', '劉若晴', RD, 'prebuilt'),
  person('H', '蔡宗翰', PLATFORM),
  person('I', '鄭雅婷', PLATFORM, 'deleted'),
];

function setup({ render = false, users = usersPeople(), capped = false } = {}) {
  const generation = signal(1);
  const everyone = [...users, ...rdPeople()];
  const api = {
    directoryNodes: vi.fn(async (parent: string | undefined) => ({
      ...tree[parent ?? ROOT],
      writable: false,
      moreChildren: false,
    })),
    // capped：模擬節點超過上限，沒有關鍵字時回 more，有關鍵字時在目錄端依姓名篩選。
    directoryPeople: vi.fn(async (dn: string, subtree: boolean, q = '') => ({
      people: everyone.filter(
        (item) =>
          (subtree ? withinDn(item.dn, dn) : item.dn.slice(item.dn.indexOf(',') + 1) === dn) &&
          item.displayName.includes(q),
      ),
      more: capped && !q,
    })),
    directoryPerson: vi.fn(async (sid: string) => detail(sid)),
    previewDirectoryImport: vi.fn(async ({ includeDisabled }: { includeDisabled?: boolean }) => ({
      items: [
        outcome('A', 'create'),
        outcome('B', 'update', {
          changes: [{ field: 'displayName', before: 'David', after: '白大衛' }],
        }),
        outcome('F', includeDisabled ? 'create' : 'disabled', { directoryDisabled: true }),
      ],
    })),
    importDirectoryUsers: vi.fn(async () => ({
      items: [
        outcome('A', 'create'),
        outcome('B', 'update'),
        outcome('F', 'create', { directoryDisabled: true }),
      ],
    })),
  };
  const usersStore = { readUsers: vi.fn(), toast: { success: vi.fn() } };
  TestBed.configureTestingModule({
    providers: [
      { provide: IdentityApi, useValue: api },
      { provide: AdminUsersStore, useValue: usersStore },
      { provide: ViewScope, useValue: { later: (work: () => void) => work(), cancel: vi.fn() } },
      {
        provide: WorkspaceSession,
        useValue: { auth: { generation }, load: vi.fn(async () => true), has: () => true },
      },
      { provide: AuthService, useValue: { generation, guard: () => () => true } },
    ],
  });
  // 選取、預覽與匯入的流程在類別上驗證；畫面另外以 render 渲染整個對話框。
  if (!render)
    TestBed.overrideComponent(DirectoryImportDialog, {
      set: { template: '<dialog #dialog></dialog>' },
    });
  const fixture = TestBed.createComponent(DirectoryImportDialog);
  fixture.detectChanges();
  // 樹的節點以一般 promise 讀取、不算在 whenStable 等待的工作裡，先讓出 macrotask 再等人員清單。
  const settle = async () => {
    for (let i = 0; i < 3; i++) {
      await new Promise((resolve) => setTimeout(resolve));
      await TestBed.inject(ApplicationRef).whenStable();
      fixture.detectChanges();
    }
  };
  const root = fixture.nativeElement as HTMLElement;
  const dialog = fixture.componentInstance;
  const named = (name: string) => dialog.store.visible().find((x) => x.displayName === name)!;
  return { dialog, store: dialog.store, api, users: usersStore, settle, root, named, everyone };
}

const polyfillDialog = () => {
  HTMLDialogElement.prototype.showModal ??= function (this: HTMLDialogElement) {
    this.setAttribute('open', '');
  };
  HTMLDialogElement.prototype.close ??= function (this: HTMLDialogElement) {
    this.removeAttribute('open');
  };
};

describe('DirectoryImportDialog', () => {
  beforeAll(polyfillDialog);
  afterEach(() => TestBed.resetTestingModule());

  it('opens on the busiest node under the root and lists its people', async () => {
    const { dialog, store, api, settle } = setup();
    dialog.open();
    await settle();
    expect(api.directoryNodes).toHaveBeenCalledWith(undefined);
    expect(store.node()).toBe(USERS);
    expect(api.directoryPeople).toHaveBeenLastCalledWith(USERS, false, '', expect.anything());
    expect(store.visible().map((x) => x.sid)).toEqual(['A', 'B', 'C', 'D', 'E', 'F']);
    expect(store.counts()).toEqual({ all: 6, new: 2, changed: 1, synced: 2, blocked: 1 });
    expect(store.crumbs().map((x) => x.name)).toEqual(['fixture.internal', 'Users']);

    const [rootItem] = store.treeItems();
    expect(rootItem.label).toBe('fixture.internal');
    // 子節點依中文排序，順序交給 Intl.Collator，這裡只比對內容。
    expect(
      Object.fromEntries(rootItem.children!.map((x) => [x.label, [x.count, x.muted]])),
    ).toEqual({ Users: [6, false], 研發處: [3, false], 空部門: [0, true] });
  });

  it('keeps people picked on earlier nodes and counts them on every ancestor', async () => {
    const { dialog, store, api, settle, named } = setup();
    dialog.open();
    await settle();
    store.toggle(named('王可蘿'), true);

    store.choose(RD);
    await settle();
    // 有下層的節點選了就展開，一併讀下一層。
    expect(api.directoryNodes).toHaveBeenLastCalledWith(RD);
    expect(store.expanded().has(RD)).toBe(true);
    expect(store.visible().map((x) => x.displayName)).toEqual(['劉若晴']);
    store.toggle(named('劉若晴'), true);

    store.choose(PLATFORM);
    await settle();
    store.toggle(named('蔡宗翰'), true);

    expect(store.selected().map((x) => x.displayName)).toEqual(['王可蘿', '劉若晴', '蔡宗翰']);
    expect(store.sources()).toBe(3);
    expect(store.basket().new.map((x) => x.sid)).toEqual(['A', 'G', 'H']);
    const [rootItem] = store.treeItems();
    const badges = (item: typeof rootItem): [string, number | undefined][] => [
      [item.label, item.badge],
      ...(item.children ?? []).flatMap(badges),
    ];
    expect(Object.fromEntries(badges(rootItem))).toEqual({
      'fixture.internal': 3,
      Users: 1,
      研發處: 2,
      平台部: 1,
      空部門: 0,
    });
    expect(rootItem.badgeLabel).toBe('已選 3 人');
  });

  it('lists people of sub OUs only when asked', async () => {
    const { dialog, store, api, settle } = setup();
    dialog.open();
    await settle();
    store.choose(RD);
    store.setSubtree(true);
    await settle();
    expect(api.directoryPeople).toHaveBeenLastCalledWith(RD, true, '', expect.anything());
    expect(store.visible().map((x) => x.sid)).toEqual(['G', 'H', 'I']);
  });

  it('never picks people the server would refuse', async () => {
    const { dialog, store, settle, named } = setup();
    dialog.open();
    await settle();
    store.toggle(named('李志遠'), true);
    expect(store.selection().size).toBe(0);

    store.toggleAll(true);
    expect([...store.selection().keys()]).toEqual(['A', 'B', 'C', 'E', 'F']);
    expect(store.headState()).toBe('all');
  });

  it('picks a run of people with Shift and skips the ones in between that cannot be picked', async () => {
    const { dialog, store, settle, named } = setup();
    dialog.open();
    await settle();
    store.toggle(named('白大衛'), true);
    store.toggle(named('周美玲'), true, true);
    expect([...store.selection().keys()]).toEqual(['B', 'C', 'E']);

    // 往回選也行：從上一次點的人（周美玲）到王可蘿，整段設成取消。
    store.toggle(named('王可蘿'), false, true);
    expect(store.selection().size).toBe(0);

    // 換篩選後起點清掉，Shift 只影響點的那一位。
    store.toggle(named('王可蘿'), true);
    store.setFilter('synced');
    store.toggle(named('周美玲'), true, true);
    expect([...store.selection().keys()]).toEqual(['A', 'E']);
  });

  it('offers to keep only the people who need work after picking the whole list', async () => {
    const { dialog, store, settle } = setup();
    dialog.open();
    await settle();
    store.toggleAll(true);
    const banner = store.banner()!;
    expect(banner.text).toBe('已選取這份清單的 5 人，其中 2 人與平台一致，匯入時會略過。');
    expect(banner.action).toBe('只留需要處理的 3 人');
    store.remove(banner.drop);
    expect([...store.selection().keys()]).toEqual(['A', 'B', 'F']);
    expect(store.headState()).toBe('some');
    expect(store.banner()).toBeNull();

    store.setFilter('new');
    expect(store.headState()).toBe('all');
    expect(store.banner()).toMatchObject({
      text: '已選取這份清單的全部 2 人。',
      action: '取消選取',
    });
  });

  it('stops at 100 people and says how many were left out', async () => {
    const crowd = Array.from({ length: 103 }, (_, i) => person(`S${i}`, `人員${i}`, USERS));
    const { dialog, store, settle } = setup({ users: crowd });
    dialog.open();
    await settle();
    store.toggleAll(true);
    expect(store.selection().size).toBe(100);
    expect(store.limitNote()).toBe('一次最多匯入 100 人，另有 3 人沒有加入。');
    store.clear();
    expect(store.selection().size).toBe(0);
    expect(store.limitNote()).toBe('');
  });

  it('previews, asks again with disabled accounts, imports and starts over', async () => {
    const { dialog, store, api, users, settle, named } = setup();
    dialog.open();
    await settle();
    for (const name of ['王可蘿', '白大衛', '洪子豪']) store.toggle(named(name), true);

    await dialog.preview();
    expect(api.previewDirectoryImport).toHaveBeenLastCalledWith({
      sids: ['A', 'B', 'F'],
      includeDisabled: false,
    });
    expect(dialog.step()).toBe('preview');
    expect(dialog.tally()).toEqual({ created: 1, updated: 1, skipped: 1 });
    expect(dialog.summary()).toBe('新建 1、更新 1、AD 已停用，未匯入 1');
    expect(dialog.disabledCount()).toBe(1);
    expect(dialog.pending()).toBe(2);

    await dialog.preview(true);
    expect(dialog.includeDisabled()).toBe(true);
    expect(dialog.pending()).toBe(3);
    // 勾了之後停用的人變成「新建」，仍要算進來，核取方塊才不會消失。
    expect(dialog.disabledCount()).toBe(1);

    await dialog.confirm();
    expect(api.importDirectoryUsers).toHaveBeenCalledWith({
      sids: ['A', 'B', 'F'],
      includeDisabled: true,
    });
    expect(users.readUsers).toHaveBeenCalled();
    expect(dialog.step()).toBe('result');
    expect(dialog.failed()).toBe(false);
    expect(dialog.tally()).toEqual({ created: 2, updated: 1, skipped: 0 });

    const reads = api.directoryPeople.mock.calls.length;
    dialog.again();
    await settle();
    expect(dialog.step()).toBe('select');
    expect(store.selection().size).toBe(0);
    expect(dialog.outcomes()).toEqual([]);
    expect(dialog.includeDisabled()).toBe(false);
    // 剛匯入過，狀態已變，要重讀目前節點。
    expect(api.directoryPeople.mock.calls.length).toBeGreaterThan(reads);
    expect(store.node()).toBe(USERS);
  });

  it('stays on the result when someone failed and keeps the selection for another preview', async () => {
    const { dialog, store, api, settle, named } = setup();
    api.importDirectoryUsers.mockResolvedValueOnce({
      items: [outcome('A', 'create'), outcome('B', 'failed', { errorCode: 'directory_changed' })],
    });
    dialog.open();
    await settle();
    store.toggle(named('王可蘿'), true);
    store.toggle(named('白大衛'), true);
    await dialog.preview();
    await dialog.confirm();
    expect(dialog.step()).toBe('result');
    expect(dialog.failed()).toBe(true);
    dialog.back();
    expect(dialog.step()).toBe('select');
    expect([...store.selection().keys()]).toEqual(['A', 'B']);
  });

  it('shows why the preview failed and stays on the selection', async () => {
    const { dialog, store, api, settle, named } = setup();
    api.previewDirectoryImport.mockRejectedValueOnce(new Error('boom'));
    dialog.open();
    await settle();
    store.toggle(named('王可蘿'), true);
    await dialog.preview();
    expect(dialog.step()).toBe('select');
    expect(dialog.error()).not.toBe('');
    expect(dialog.busy()).toBe(false);
  });
});

describe('DirectoryImportDialog screen', () => {
  beforeAll(polyfillDialog);
  afterEach(() => {
    TestBed.resetTestingModule();
    vi.unstubAllGlobals();
  });

  const stubMedia = () =>
    vi.stubGlobal('matchMedia', (query: string) => ({
      matches: false,
      media: query,
      addEventListener() {},
      removeEventListener() {},
    }));

  it('picks rows with their boxes, a run with Shift, and marks the header box as partly picked', async () => {
    stubMedia();
    const { dialog, store, settle, root } = setup({ render: true });
    dialog.open();
    await settle();
    const rows = () => [
      ...root.querySelectorAll<HTMLTableRowElement>('.directory-people tbody tr'),
    ];
    const head = () =>
      root.querySelector<HTMLInputElement>('.directory-people thead input[type="checkbox"]')!;
    expect(rows().map((row) => row.querySelector('strong')?.textContent?.trim())).toEqual([
      '王可蘿',
      '白大衛',
      '朱愛麗',
      '李志遠',
      '周美玲',
      '洪子豪',
    ]);
    expect(rows()[1].textContent).toContain('1 欄異動');

    const box = (index: number) =>
      rows()[index].querySelector<HTMLInputElement>('input[type="checkbox"]')!;
    box(0).click();
    await settle();
    // 核取方塊的 change 不帶修飾鍵，Shift 由按下時的指標事件記下。
    box(2).dispatchEvent(new MouseEvent('pointerdown', { bubbles: true, shiftKey: true }));
    box(2).click();
    await settle();
    expect([...store.selection().keys()]).toEqual(['A', 'B', 'C']);
    expect(rows()[0].classList).toContain('is-picked');
    expect(head().indeterminate).toBe(true);
    expect(head().checked).toBe(false);
    expect(root.querySelector('nx-directory-basket')?.textContent?.replace(/\s+/g, ' ')).toContain(
      '已選 3 / 100 人',
    );

    // 點列是檢視 AD 屬性，不勾選；不可選的人也能檢視。
    rows()[3]
      .querySelector('td:nth-child(4)')!
      .dispatchEvent(new MouseEvent('click', { bubbles: true }));
    await settle();
    expect(store.selection().has('D')).toBe(false);
    expect(store.inspected()).toBe('D');
    expect(store.panel()).toBe('attributes');
    expect(rows()[3].classList).toContain('is-selected');
    expect(box(3).disabled).toBe(true);

    head().click();
    await settle();
    expect(head().checked).toBe(true);
    expect(head().indeterminate).toBe(false);
    const banner = root.querySelector<HTMLElement>('.directory-banner')!;
    expect(banner.textContent).toContain('其中 2 人與平台一致');
    banner.querySelector('button')!.click();
    await settle();
    expect([...store.selection().keys()]).toEqual(['A', 'B', 'F']);
    expect(root.querySelector('.directory-banner')).toBeNull();
  });

  it('searches the whole node on the server once the list hits the limit', async () => {
    const { dialog, store, api, settle } = setup({ capped: true });
    dialog.open();
    await settle();
    expect(store.more()).toBe(true);

    store.setQuery('洪');
    // 畫面先依已讀到的人篩選，停止輸入一下才到目錄端找。
    expect(store.visible().map((x) => x.sid)).toEqual(['F']);
    await new Promise((resolve) => setTimeout(resolve, 400));
    await settle();
    expect(api.directoryPeople).toHaveBeenLastCalledWith(USERS, false, '洪', expect.anything());
    expect(store.searchedServer()).toBe(true);
    expect(store.more()).toBe(false);

    store.clearFilters();
    await settle();
    expect(store.searchedServer()).toBe(false);
    expect(store.all()).toHaveLength(6);
  });

  it('refreshes the tree off screen once, keeping the open branches and the current node', async () => {
    const { dialog, store, api, settle } = setup();
    dialog.open();
    await settle();
    store.expand(RD, true);
    await settle();
    store.choose(PLATFORM);
    await settle();
    const nodes = store.nodes();
    const reads = api.directoryNodes.mock.calls.length;

    const pending = store.refresh();
    void store.refresh();
    expect(store.refreshing()).toBe(true);
    // 讀的期間舊樹仍在畫面上。
    expect(store.nodes()).toBe(nodes);
    await pending;
    await settle();
    expect(store.refreshing()).toBe(false);
    expect(api.directoryNodes.mock.calls.slice(reads).map(([dn]) => dn)).toEqual([undefined, RD]);
    expect(store.nodes()).not.toBe(nodes);
    expect(store.expanded().has(RD)).toBe(true);
    expect(store.node()).toBe(PLATFORM);
  });

  it('drops a picked person who can no longer be imported when the list is read again', async () => {
    const { dialog, store, settle, named, everyone } = setup();
    dialog.open();
    await settle();
    store.toggle(named('王可蘿'), true);
    store.toggle(named('白大衛'), true);
    everyone[0] = person('A', '王可蘿', USERS, 'conflict');
    store.people.reload();
    await settle();
    expect([...store.selection().keys()]).toEqual(['B']);
    expect(store.limitNote()).toContain('1 人的狀態已變成無法匯入');
  });

  it('imports only the people that were previewed', async () => {
    const { dialog, store, api, settle, named } = setup();
    dialog.open();
    await settle();
    store.toggle(named('王可蘿'), true);
    await dialog.preview();
    store.toggle(named('白大衛'), true);
    await dialog.confirm();
    expect(api.importDirectoryUsers).toHaveBeenCalledWith({ sids: ['A'], includeDisabled: false });
  });

  it('reads the full attributes only after the pointer rests on a person and keeps them', async () => {
    stubMedia();
    const { dialog, store, api, settle, root, named } = setup({ render: true });
    dialog.open();
    await settle();
    store.inspect(named('白大衛'));
    store.inspect(named('王可蘿'));
    await settle();
    expect(api.directoryPerson).not.toHaveBeenCalled();
    expect(root.querySelector('nx-directory-person-card [role="status"]')).not.toBeNull();

    await new Promise((resolve) => setTimeout(resolve, 300));
    await settle();
    expect(api.directoryPerson.mock.calls.map(([sid]) => sid)).toEqual(['A']);
    const card = root.querySelector('nx-directory-person-card')!;
    expect(
      [...card.querySelectorAll('.directory-person-badges nx-status-badge')].map((badge) =>
        badge.textContent?.trim(),
      ),
    ).toEqual(['未匯入', '已鎖定']);
    expect([...card.querySelectorAll('nx-property-list h5')].map((h) => h.textContent)).toEqual([
      '身分',
      '組織',
      '帳號狀態',
      '登入活動',
      '所屬群組',
      '物件',
    ]);
    expect(card.textContent).toContain('密碼永不過期');

    // 回到看過的人直接用快取，不再讀目錄。
    store.inspect(named('白大衛'));
    store.inspect(named('王可蘿'));
    await settle();
    expect(api.directoryPerson).toHaveBeenCalledTimes(1);
  });

  it('finds every step of the selection and the preview on screen', async () => {
    stubMedia();
    const { dialog, store, settle, root, named } = setup({ render: true });
    const expectTargets = () => {
      expect(root.querySelector('nx-dialog-header nx-product-tour-button')).not.toBeNull();
      for (const step of tourSteps(dialog.tour()))
        expect(
          root.querySelector(step.target.replace(/^nx-directory-import-dialog /, '')),
          step.target,
        ).not.toBeNull();
    };
    dialog.open();
    await settle();
    store.toggle(named('王可蘿'), true);
    await settle();
    expectTargets();

    // 預覽要有 AD 已停用的人，「仍要匯入 AD 已停用」的核取方塊才會出現。
    store.toggle(named('洪子豪'), true);
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
        displayName: '王可蘿',
        account: 'carol.lin',
        changes: [
          { field: 'displayName', before: 'Carol', after: '王可蘿' },
          { field: 'email', before: null, after: 'carol@example.test' },
        ],
      }),
      outcome('S-9', 'not_found', { enabled: true }),
    ]);
    fixture.componentRef.setInput('known', new Map([['S-9', person('S-9', '白大衛', USERS)]]));
    fixture.detectChanges();
    const rows = [...(fixture.nativeElement as HTMLElement).querySelectorAll('tbody tr')];
    const text = (element: Element | null | undefined) =>
      element?.textContent?.replace(/\s+/g, ' ').trim();

    expect(text(rows[0])).toContain('王可蘿');
    expect(text(rows[0])).toContain('更新');
    const changes = [...rows[0].querySelectorAll('nx-change-list .ui-change')];
    expect(
      changes.map((change) => [
        text(change.querySelector('dt')),
        text(change.querySelector('del')),
        text(change.querySelector('ins')),
      ]),
    ).toEqual([
      ['姓名', 'Carol', '王可蘿'],
      ['通知信箱', undefined, 'carol@example.test'],
    ]);

    expect(text(rows[1])).toContain('白大衛');
    expect(text(rows[1])).toContain('AD 找不到');
    expect(text(rows[1])).toContain('平台帳號仍啟用');
    expect(rows[1].querySelector('nx-change-list')).toBeNull();
  });
});
