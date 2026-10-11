import { ApplicationRef, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { NotificationDto } from '../../core/api/schema';
import { WorkspaceSession } from '../../core/auth/workspace-session';
import { WorkspaceLayout } from '../../core/layout/workspace-layout';
import { NotificationStore } from './notification-store';
import { NotificationsApi } from './notifications-api';

const notification = (id: string, readAt: string | null = null) =>
  ({
    id,
    type: 'conversation.completed',
    title: '回答完成',
    body: '',
    target: { kind: 'conversation', id: `c-${id}` },
    version: null,
    createdAt: '2026-10-10T08:00:00Z',
    readAt,
  }) as unknown as NotificationDto;

function setup() {
  const me = signal<{ id: string } | null>({ id: 'u1' });
  const generation = signal(1);
  const api = {
    list: vi.fn(async () => ({
      items: [notification('a'), notification('b', '2026-10-10T08:01:00Z')],
      unread: 1,
      hasMore: false,
    })),
    read: vi.fn(async () => undefined),
  };
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      { provide: NotificationsApi, useValue: api },
      { provide: WorkspaceLayout, useValue: { closeMobile: vi.fn() } },
      {
        provide: WorkspaceSession,
        useValue: {
          me,
          auth: { generation },
          settings: { value: () => ({ notifyOnCompletion: false }) },
        },
      },
    ],
  });
  const store = TestBed.inject(NotificationStore);
  const settle = async () => {
    await TestBed.inject(ApplicationRef).whenStable();
    await Promise.resolve();
  };
  return { store, api, me, settle };
}

describe('notification store', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('reads notifications for the signed-in user and clears them for the next one', async () => {
    const { store, api, me, settle } = setup();
    await settle();
    expect(api.list).toHaveBeenCalledTimes(1);
    expect(store.items().map((x) => x.id)).toEqual(['a', 'b']);
    expect(store.unread()).toBe(1);
    expect(store.completed('c-a')).toBe(true);
    expect(store.completed('c-b')).toBe(false);
    me.set(null);
    await settle();
    expect(store.items()).toEqual([]);
    expect(store.unread()).toBe(0);
  });

  it('marks a notification read once and drops it from the unread-only list', async () => {
    const { store, api, settle } = setup();
    await settle();
    store.unreadOnly.set(true);
    await store.read(store.items()[0]);
    expect(api.read).toHaveBeenCalledWith('a');
    expect(store.unread()).toBe(0);
    expect(store.items().map((x) => x.id)).toEqual([]);
  });

  it('keeps the list when marking read fails and reports why', async () => {
    const { store, api, settle } = setup();
    await settle();
    api.read.mockRejectedValueOnce(new Error('offline'));
    await expect(store.read(store.items()[0])).rejects.toThrow();
    expect(store.unread()).toBe(1);
    expect(store.error()).not.toBe('');
  });
});
