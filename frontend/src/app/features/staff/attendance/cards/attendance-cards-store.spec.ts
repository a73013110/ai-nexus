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
  kind: 'employee',
  note: null,
  createdAt: '2026-01-01T00:00:00Z',
  current: held,
};
const request: AssignRequest = {
  cardId: 'c1',
  cardNumber: '0012345',
  userId: null,
  holderName: '新進同仁',
  startsAt: '2026-10-01T00:00:00.000Z',
  endsAt: null,
  note: null,
};

function setup({ maintain = true, testing = false } = {}) {
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
    unattributed: vi.fn(async () => []),
    assignments: vi.fn(async () => [held]),
    createCard: vi.fn(async () => ({ ...card, id: 'c9', cardNumber: '0099999', current: null })),
    createAssignment: vi.fn(async (body: CardAssignmentCreateRequest) => {
      if (!body.takeOver && body.cardId === 'c1')
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
    testing: signal(testing),
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
  store.filters.set({ search: '', kind: '' });
  const settle = () => TestBed.inject(ApplicationRef).whenStable();
  return { store, api, toast, settle };
}

describe('AttendanceCardsStore', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('asks to take over a held card, naming the holder from the history, then resends with takeOver', async () => {
    const { store, api, toast, settle } = setup();
    await settle();
    const confirm = vi.fn(async () => true);
    const outcome = await store.assign(request, confirm);
    expect(confirm).toHaveBeenCalledWith('王小明');
    expect(api.assignments).toHaveBeenCalledWith('c1');
    expect(api.createAssignment).toHaveBeenCalledTimes(2);
    expect(api.createAssignment.mock.calls[0][0]).toMatchObject({ cardId: 'c1', takeOver: false });
    expect(api.createAssignment.mock.calls[1][0]).toMatchObject({
      cardId: 'c1',
      holderName: '新進同仁',
      takeOver: true,
    });
    expect(outcome.saved).toBe(true);
    expect(toast.success).toHaveBeenCalledWith('已將卡號 0012345 指派給 新進同仁');
    expect(store.busy()).toBe(false);
  });

  it('leaves the card with its holder when the take-over is declined', async () => {
    const { store, api, settle } = setup();
    await settle();
    const outcome = await store.assign(request, async () => false);
    expect(outcome).toEqual({ saved: false, error: '' });
    expect(api.createAssignment).toHaveBeenCalledOnce();
    expect(store.busy()).toBe(false);
  });

  it('registers an unknown card number as an employee card before assigning it', async () => {
    const { store, api, settle } = setup();
    await settle();
    const confirm = vi.fn(async () => true);
    const outcome = await store.assign(
      { ...request, cardId: null, cardNumber: '0099999' },
      confirm,
    );
    expect(api.createCard).toHaveBeenCalledWith({
      cardNumber: '0099999',
      kind: 'employee',
      note: null,
    });
    expect(api.createAssignment.mock.calls[0][0]).toMatchObject({ cardId: 'c9', takeOver: false });
    expect(confirm).not.toHaveBeenCalled();
    expect(outcome.saved).toBe(true);
  });

  it('turns other refusals into a reviewed message and never writes under a test identity', async () => {
    const { store, api, settle } = setup();
    await settle();
    api.createAssignment.mockRejectedValueOnce(
      new ApiError(409, 'attendance_assignment_overlap', undefined, ''),
    );
    const outcome = await store.assign({ ...request, cardId: 'c2' }, async () => true);
    expect(outcome.saved).toBe(false);
    expect(outcome.saved ? '' : outcome.error).toContain('重疊');

    TestBed.resetTestingModule();
    const readOnly = setup({ testing: true });
    await readOnly.settle();
    expect(readOnly.store.locked()).toBe(true);
    expect((await readOnly.store.assign(request, async () => true)).saved).toBe(false);
    expect(readOnly.api.createAssignment).not.toHaveBeenCalled();
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
