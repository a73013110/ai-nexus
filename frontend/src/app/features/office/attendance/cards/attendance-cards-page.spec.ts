import { ApplicationRef, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideRouter } from '@angular/router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { CardAssignmentDto, CardDto, PunchDto, TablePageOfPunchDto } from '@core/api/schema';
import { AuthService } from '@core/auth/auth-service';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { tourSteps, type ProductTourDefinition } from '@shared/ui/product-tour';
import { ToastService } from '@shared/ui/toast-service';
import { CollaborationApi } from '@features/work/collaboration/collaboration-api';
import { AttendanceApi } from '../attendance-api';
import { AssignmentDialog } from './assignment-dialog';
import { AttendanceCardsPage } from './attendance-cards-page';

const PAGE = 'nx-attendance-cards-page';
const held: CardAssignmentDto = {
  id: 'a1',
  cardId: 'c1',
  cardNumber: '0012345',
  userId: 'u1',
  userDisplayName: '王小明',
  holderName: null,
  startsAt: '2026-01-01T00:00:00Z',
  endsAt: null,
  note: null,
  createdAt: '2026-01-01T00:00:00Z',
};
const card: CardDto = {
  id: 'c1',
  cardNumber: '0012345',
  hiddenFromRecords: false,
  note: null,
  createdAt: '2026-01-01T00:00:00Z',
  current: held,
  lastPunchedAt: '2026-10-02T10:01:00Z',
  unattributedCount: 0,
  firstUnattributedAt: null,
  lastUnattributedAt: null,
};
/** 有刷卡卻沒有持有人：打卡檔出現、還沒指派的新卡號。 */
const fresh: CardDto = {
  ...card,
  id: 'c2',
  cardNumber: '0077777',
  current: null,
  createdAt: '2026-10-01T16:00:00Z',
  unattributedCount: 3,
  firstUnattributedAt: '2026-10-01T00:55:00Z',
  lastUnattributedAt: '2026-10-02T10:01:00Z',
};
/** 第 `index` 筆刷卡：2026-10-02 起每小時一筆往前推，新到舊。 */
const punch = (index: number, unattributed = false): PunchDto => ({
  id: 1000 - index,
  cardNumber: 'A001',
  punchedAt: new Date(Date.parse('2026-10-02T10:00:00Z') - index * 3_600_000).toISOString(),
  machineCode: '001',
  eventCode: '11',
  unattributed,
  userId: unattributed ? null : 'u1',
  holder: unattributed ? null : '王小明',
});
const punchPage = (items: PunchDto[]): TablePageOfPunchDto => ({
  items,
  total: items.length,
  offset: 0,
  complete: true,
});
const visitor: CardDto = { ...fresh, id: 'c3', cardNumber: 'V01', hiddenFromRecords: true };

function setup() {
  vi.stubGlobal('matchMedia', (query: string) => ({
    matches: false,
    media: query,
    addEventListener() {},
    removeEventListener() {},
  }));
  // jsdom 沒有捲動與 dialog 的開關。
  Element.prototype.scrollTo ??= function () {};
  HTMLDialogElement.prototype.showModal ??= function (this: HTMLDialogElement) {
    this.setAttribute('open', '');
  };
  HTMLDialogElement.prototype.close ??= function (this: HTMLDialogElement) {
    this.removeAttribute('open');
  };
  const generation = signal(1);
  const api = {
    cards: vi.fn(async () => [card, fresh, visitor]),
    assignments: vi.fn(async (id: string) => (id === card.id ? [held] : [])),
    updateCard: vi.fn(async () => undefined),
    cardPunches: vi.fn(async (_id: string, _table: object): Promise<TablePageOfPunchDto> =>
      punchPage([]),
    ),
  };
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      {
        provide: WorkspaceSession,
        useValue: {
          auth: { generation },
          load: vi.fn(async () => true),
          me: signal({ id: 'u1' }),
          has: (feature: string) => feature.startsWith('attendance.cards'),
          featureName: () => '卡號對應',
        },
      },
      { provide: AuthService, useValue: { generation, guard: () => () => true } },
      { provide: AttendanceApi, useValue: api },
      { provide: CollaborationApi, useValue: { directory: vi.fn(async () => []) } },
      { provide: ToastService, useValue: { success: vi.fn() } },
    ],
  });
  const fixture = TestBed.createComponent(AttendanceCardsPage);
  const host = fixture.nativeElement as HTMLElement;
  const settle = async () => {
    await TestBed.inject(ApplicationRef).whenStable();
    fixture.detectChanges();
  };
  return { fixture, page: fixture.componentInstance, host, api, settle };
}

describe('AttendanceCardsPage', () => {
  afterEach(() => {
    TestBed.resetTestingModule();
    vi.unstubAllGlobals();
  });

  it('finds every page step and every drawer step once a card is selected', async () => {
    const { page, host, settle } = setup();
    // 元件宿主本身就是頁面標籤，查詢時去掉開頭的宿主選擇器。
    const expectTargets = (definition: ProductTourDefinition | null) => {
      expect(definition).not.toBeNull();
      for (const step of tourSteps(definition!))
        expect(host.querySelector(step.target.slice(PAGE.length + 1)), step.target).not.toBeNull();
    };
    await settle();
    expect(host.querySelector('nx-feature-page nx-product-tour-button')).not.toBeNull();
    expectTargets(page.tour());
    expect(page.drawerTour()).toBeNull();

    page.store.selectedId.set(card.id);
    await settle();
    expect(host.querySelector('nx-detail-drawer nx-product-tour-button')).not.toBeNull();
    expectTargets(page.drawerTour());
  });

  it('lists punched card numbers in the same table and flags the ones waiting for a holder', async () => {
    const { host, page, api, settle } = setup();
    await settle();
    const rows = [...host.querySelectorAll('.cards-table tbody tr')];
    expect(rows.map((row) => row.querySelector('.cards-number')?.textContent?.trim())).toEqual([
      '0012345',
      '0077777',
      'V01',
    ]);
    expect(rows[0].textContent).toContain('王小明');
    expect(rows[1].textContent).toContain('待指派 3 筆');
    // 隱藏的卡是 HR 刻意不管的，不提示待指派。
    expect(rows[2].textContent).toContain('隱藏');
    expect(rows[2].textContent).not.toContain('待指派');

    page.query.commit({ status: 'unheld' });
    await settle();
    expect(api.cards).toHaveBeenLastCalledWith(
      { search: undefined, status: 'unheld' },
      expect.anything(),
    );
  });

  it('prefills the unassigned punches before the next holding when assigning them', async () => {
    const { fixture, host, page, api, settle } = setup();
    api.assignments.mockImplementation(async (id: string) =>
      id === fresh.id
        ? [{ ...held, id: 'a2', cardId: fresh.id, startsAt: '2026-10-05T00:00:00Z' }]
        : [],
    );
    await settle();
    page.store.selectedId.set(fresh.id);
    await settle();
    await settle();
    const notice = host.querySelector('nx-detail-drawer .cards-pending')!;
    expect(notice.textContent).toContain('3 筆刷卡沒有持有人');
    (notice.querySelector('.cards-assign-pending') as HTMLButtonElement).click();
    await settle();
    const dialog = host.querySelector('nx-assignment-dialog')!;
    const editor = fixture.debugElement
      .query(By.directive(AssignmentDialog))
      .componentInstance.editor() as { draft: { startsAt: string; endsAt: string } };
    expect(dialog.querySelector('dialog')?.hasAttribute('open')).toBe(true);
    // 台北時間：開始是第一筆未歸戶刷卡，結束是下一段期間的開始。
    expect(editor.draft.startsAt).toBe('2026-10-01T08:55:00');
    expect(editor.draft.endsAt).toBe('2026-10-05T08:00:00');
  });

  it('hides a card from the records with the switch and keeps its note', async () => {
    const { host, page, api, settle } = setup();
    await settle();
    page.store.selectedId.set(card.id);
    await settle();
    const toggle = host.querySelector<HTMLButtonElement>(
      'nx-detail-drawer .cards-visibility [role="switch"]',
    )!;
    expect(toggle.getAttribute('aria-checked')).toBe('true');
    toggle.click();
    await settle();
    expect(api.updateCard).toHaveBeenCalledWith(card.id, { hiddenFromRecords: true, note: null });
  });

  it('lists the card punches newest first in a table without the card number', async () => {
    const { host, page, api, settle } = setup();
    api.cardPunches.mockResolvedValue(punchPage([punch(0), punch(1, true)]));
    await settle();
    page.store.selectedId.set(card.id);
    await settle();
    await settle();
    expect(api.cardPunches).toHaveBeenCalledWith(
      card.id,
      { offset: 0, limit: 50, sort: 'punchedAt:desc' },
      expect.anything(),
    );
    const table = host.querySelector('nx-detail-drawer .ui-drawer-sections > nx-punch-table')!;
    expect(table.querySelector('.punch-table-card')).toBeNull();
    const rows = [...table.querySelectorAll('tbody tr')];
    expect(rows[0].textContent).toContain('2026/10/02 18:00:00');
    expect(rows[0].textContent).toContain('王小明');
    expect(rows[1].textContent).toContain('無持有人');
  });
});
