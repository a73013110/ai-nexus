import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { CardAssignmentDto } from '@core/api/schema';
import { routeOf } from '@core/api/catalog';
import { AuthService } from '@core/auth/auth-service';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { AttendanceApi } from '../attendance-api';
import { AttendanceUserCards } from './attendance-user-cards';

const period = (id: string, patch: Partial<CardAssignmentDto> = {}): CardAssignmentDto => ({
  id,
  cardId: 'card-' + id,
  cardNumber: '000' + id,
  userId: 'u1',
  userDisplayName: '王小明',
  holderName: null,
  startsAt: '2026-01-01T00:00:00Z',
  endsAt: null,
  note: null,
  createdAt: '2026-01-01T00:00:00Z',
  ...patch,
});

function setup({ features = ['attendance.cards'], items = [] as CardAssignmentDto[] } = {}) {
  const generation = signal(1);
  const session = {
    auth: { generation },
    load: vi.fn(async () => true),
    me: signal({ id: 'admin' }),
    has: (feature: string) => features.includes(feature),
  };
  const api = { userAssignments: vi.fn(async (_userId: string) => items) };
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      { provide: WorkspaceSession, useValue: session },
      { provide: AuthService, useValue: { generation, guard: () => () => true } },
      { provide: AttendanceApi, useValue: api },
    ],
  });
  const fixture = TestBed.createComponent(AttendanceUserCards);
  fixture.componentRef.setInput('userId', 'u1');
  fixture.componentRef.setInput('displayName', '王小明');
  const root = fixture.nativeElement as HTMLElement;
  return { fixture, root, api };
}

describe('AttendanceUserCards', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('lists the periods newest first with card number, kind and the current one marked', async () => {
    const { fixture, root, api } = setup({
      items: [
        period('1', { startsAt: '2025-03-01T00:00:00Z', endsAt: '2025-12-31T16:00:00Z' }),
        period('2', { startsAt: '2026-01-01T00:00:00Z' }),
      ],
    });
    await fixture.whenStable();
    expect(api.userAssignments).toHaveBeenCalledWith('u1', expect.anything());
    const rows = [...root.querySelectorAll('.user-cards > li')];
    expect(rows.map((row) => row.querySelector('.user-cards-number')?.textContent?.trim())).toEqual(
      ['0002', '0001'],
    );
    expect(rows[0].textContent).toContain('員工卡');
    expect(rows[0].querySelectorAll('nx-status-badge')[1]?.textContent).toContain('持有中');
    expect(rows[1].textContent).toContain('2025');
    expect(rows[1].textContent).not.toContain('持有中');
    expect(root.querySelector('.user-cards-manage')).toBeNull();
  });

  it('shows an empty state when the user never held a card', async () => {
    const { fixture, root } = setup();
    await fixture.whenStable();
    expect(root.querySelector('.user-cards-empty')?.textContent).toContain('沒有持有過出勤卡');
  });

  it('renders nothing and sends no request without attendance.cards', async () => {
    const { fixture, root, api } = setup({ features: ['identity.users'] });
    await fixture.whenStable();
    expect(api.userAssignments).not.toHaveBeenCalled();
    expect(root.querySelector('section')).toBeNull();
  });

  it('links to the cards page searching this user when the viewer can maintain cards', async () => {
    const { fixture, root } = setup({
      features: ['attendance.cards', 'attendance.cards.maintain'],
      items: [period('1')],
    });
    await fixture.whenStable();
    const link = root.querySelector<HTMLAnchorElement>('a.user-cards-manage');
    expect(link).not.toBeNull();
    const url = new URL(link!.href);
    expect(url.pathname).toBe(routeOf('attendance.cards'));
    expect(url.searchParams.get('search')).toBe('王小明');
  });
});
