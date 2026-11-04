import { ApplicationRef, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { CardAssignmentCreateRequest, CardAssignmentDto, CardDto } from '@core/api/schema';
import { AuthService } from '@core/auth/auth-service';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { ApiError } from '@core/errors/safe-errors';
import { ViewScope } from '@shared/browser/view-scope';
import { ToastService } from '@shared/ui/toast-service';
import { AttendanceApi } from '../attendance-api';
import { AttendanceCardsStore, type AssignRequest } from './attendance-cards-store';

const held: CardAssignmentDto = {
  id: 'a1',
  rowVersion: 11,
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
  rowVersion: 7,
  cardNumber: '0012345',
  hiddenFromRecords: false,
  note: null,
  createdAt: '2026-01-01T00:00:00Z',
  current: held,
  lastPunchedAt: null,
  unattributedCount: 0,
  firstUnattributedAt: null,
  lastUnattributedAt: null,
};
const request: AssignRequest = {
  cardId: 'c1',
  userId: null,
  holderName: '新進同仁',
  startsAt: '2026-10-01T00:00:00.000Z',
  endsAt: null,
  note: null,
};

function setup({ maintain = true } = {}) {
  const generation = signal(1);
  const created = (body: CardAssignmentCreateRequest): CardAssignmentDto => ({
    ...held,
    id: 'a2',
    cardId: body.cardId,
    userId: body.userId,
    userDisplayName: null,
    holderName: body.holderName,
    startsAt: body.startsAt,
  });
  const api = {
    cards: vi.fn(async () => [card]),
    assignments: vi.fn(async () => [held]),
    updateCard: vi.fn(async () => undefined),
    createAssignment: vi.fn(async (body: CardAssignmentCreateRequest) => {
      if (body.takeOverRowVersion === null && body.cardId === 'c1')
        throw new ApiError(409, 'attendance_card_held', undefined, 'NX-' + '0'.repeat(32));
      return created(body);
    }),
    updateAssignment: vi.fn(async () => held),
    deleteAssignment: vi.fn(async () => undefined),
    deleteCard: vi.fn(async () => undefined),
  };
  const toast = { success: vi.fn() };
  const session = {
    auth: { generation },
    load: vi.fn(async () => true),
    has: (feature: string) =>
      feature === 'attendance.cards' || (maintain && feature === 'attendance.cards.maintain'),
  };
  TestBed.configureTestingModule({
    providers: [
      AttendanceCardsStore,
      ViewScope,
      { provide: AttendanceApi, useValue: api },
      { provide: ToastService, useValue: toast },
      { provide: WorkspaceSession, useValue: session },
      { provide: AuthService, useValue: { generation, guard: AuthService.prototype.guard } },
    ],
  });
  const store = TestBed.inject(AttendanceCardsStore);
  store.filters.set({ search: '', status: '' });
  const settle = () => TestBed.inject(ApplicationRef).whenStable();
  return { store, api, toast, settle };
}

describe('AttendanceCardsStore', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('asks to take over a held card, naming the holder from the history, then resends with its version', async () => {
    const { store, api, toast, settle } = setup();
    await settle();
    const confirm = vi.fn(async () => true);
    const outcome = await store.assign(request, confirm);
    expect(confirm).toHaveBeenCalledWith('王小明');
    expect(api.assignments).toHaveBeenCalledWith('c1');
    expect(api.createAssignment).toHaveBeenCalledTimes(2);
    expect(api.createAssignment.mock.calls[0][0]).toMatchObject({
      cardId: 'c1',
      takeOverRowVersion: null,
    });
    expect(api.createAssignment.mock.calls[1][0]).toMatchObject({
      cardId: 'c1',
      holderName: '新進同仁',
      takeOverRowVersion: held.rowVersion,
    });
    expect(outcome.ok).toBe(true);
    expect(toast.success).toHaveBeenCalledWith('已將卡號 0012345 指派給 新進同仁');
    await settle();
    expect(store.busy()).toBe(false);
  });

  it('leaves the card with its holder when the take-over is declined', async () => {
    const { store, api, settle } = setup();
    await settle();
    const outcome = await store.assign(request, async () => false);
    expect(outcome).toEqual({ ok: false, error: '', stale: false });
    expect(api.createAssignment).toHaveBeenCalledOnce();
    await settle();
    expect(store.busy()).toBe(false);
  });

  it('saves the hidden flag with the current note and spins only the switch', async () => {
    const { store, api, toast, settle } = setup();
    await settle();
    const saving = store.setHidden({ ...card, note: '訪客' }, true);
    expect(store.working('hidden:c1')).toBe(true);
    expect((await saving).ok).toBe(true);
    expect(api.updateCard).toHaveBeenCalledWith('c1', {
      hiddenFromRecords: true,
      note: '訪客',
      rowVersion: card.rowVersion,
    });
    expect(toast.success).toHaveBeenCalledWith('卡號 0012345 已在出勤紀錄隱藏');
    expect(store.working('hidden:c1')).toBe(false);
    // 重讀完才能再寫：重讀前畫面上的版本是舊的。
    expect(store.busy()).toBe(true);
    await settle();
    expect(store.busy()).toBe(false);
    // 改備註時隱藏旗標照舊送回。
    await store.saveNote({ ...card, hiddenFromRecords: true }, '董事');
    expect(api.updateCard).toHaveBeenLastCalledWith('c1', {
      hiddenFromRecords: true,
      note: '董事',
      rowVersion: card.rowVersion,
    });
  });

  it('sends the version it read when deleting, so a stale delete is refused', async () => {
    const { store, api, settle } = setup();
    await settle();
    await store.deleteCard(card);
    expect(api.deleteCard).toHaveBeenCalledWith('c1', card.rowVersion);
    await settle();
    await store.deleteAssignment(held);
    expect(api.deleteAssignment).toHaveBeenCalledWith('a1', held.rowVersion);
  });

  it('shows the reviewed message and reloads when someone else changed the card first', async () => {
    const { store, api, settle } = setup();
    await settle();
    api.updateCard.mockRejectedValueOnce(new ApiError(409, 'admin_write_conflict', undefined, ''));
    const reads = api.cards.mock.calls.length;
    const outcome = await store.saveNote(card, '後存的');
    expect(outcome).toMatchObject({ ok: false, stale: true });
    expect(outcome.ok ? '' : outcome.error).toContain('畫面已更新');
    expect(store.busy()).toBe(true);
    await settle();
    expect(api.cards.mock.calls.length).toBeGreaterThan(reads);
    expect(store.busy()).toBe(false);
  });

  it('turns other refusals into a reviewed message', async () => {
    const { store, api, settle } = setup();
    await settle();
    api.createAssignment.mockRejectedValueOnce(
      new ApiError(409, 'attendance_assignment_overlap', undefined, ''),
    );
    const outcome = await store.assign({ ...request, cardId: 'c2' }, async () => true);
    expect(outcome.ok).toBe(false);
    expect(outcome.ok ? '' : outcome.error).toContain('重疊');
  });

  it('shows write actions only with the maintain action', async () => {
    expect(setup().store.canMaintain()).toBe(true);
    TestBed.resetTestingModule();
    expect(setup({ maintain: false }).store.canMaintain()).toBe(false);
  });

  it('reads the history of the selected card only', async () => {
    const { store, api, settle } = setup();
    await settle();
    expect(store.assignments()).toBeNull();
    store.selectedId.set('c1');
    await settle();
    expect(api.assignments).toHaveBeenCalledWith('c1', expect.anything());
    expect(store.assignments()?.map((item) => item.id)).toEqual(['a1']);
    expect(store.selected()?.cardNumber).toBe('0012345');
  });
});
