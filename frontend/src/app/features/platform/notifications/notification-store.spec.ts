import { ApplicationRef, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Component } from '@angular/core';
import { provideRouter, Router } from '@angular/router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { NotificationDto } from '@core/api/schema';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { WorkspaceLayout } from '@core/layout/workspace-layout';
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

@Component({ template: '' })
class Page {}

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
    readTarget: vi.fn(async () => undefined),
  };
  TestBed.configureTestingModule({
    providers: [
      provideRouter([{ path: '**', component: Page }]),
      { provide: NotificationsApi, useValue: api },
      { provide: WorkspaceLayout, useValue: { closeMobile: vi.fn() } },
      {
        provide: WorkspaceSession,
        useValue: {
          me,
          auth: {
            generation,
            guard: () => {
              const current = generation();
              return () => current === generation();
            },
          },
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
    expect(store.items().map((item) => item.id)).toEqual(['a', 'b']);
    expect(store.unread()).toBe(1);
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
    expect(store.items().map((item) => item.id)).toEqual([]);
  });

  it('polls quietly and only shows reads the user started', async () => {
    const { store, api, settle } = setup();
    let finish!: () => void;
    api.list.mockImplementationOnce(
      () =>
        new Promise((resolve) => {
          finish = () => resolve({ items: [notification('a')], unread: 1, hasMore: false });
        }),
    );
    await settle();
    // 登入後的第一次讀取是輪詢：不轉圈、不淡化。
    expect(api.list).toHaveBeenCalledTimes(1);
    expect(store.loading()).toBe(false);
    finish();
    await settle();
    const reading = store.refresh();
    expect(store.loading()).toBe(true);
    expect(store.loadingMore()).toBe(false);
    await reading;
    expect(store.loading()).toBe(false);
  });

  it('marks only the pressed write as working', async () => {
    const { store, api, settle } = setup();
    await settle();
    let finish!: () => void;
    api.read.mockImplementationOnce(
      () => new Promise<undefined>((resolve) => (finish = () => resolve(undefined))),
    );
    const reading = store.read(store.items()[0]);
    expect(store.isWorking('read:a')).toBe(true);
    expect(store.isWorking('read:b')).toBe(false);
    finish();
    await reading;
    expect(store.isWorking('read:a')).toBe(false);
  });

  it('keeps the list when marking read fails and reports why', async () => {
    const { store, api, settle } = setup();
    await settle();
    api.read.mockRejectedValueOnce(new Error('offline'));
    expect(await store.read(store.items()[0])).toBe(false);
    expect(store.unread()).toBe(1);
    expect(store.error()).not.toBe('');
  });

  it('reads every notification about the destination once it is on screen', async () => {
    const id = '01234567-89ab-4cde-8000-0123456789ab';
    const { store, api, settle } = setup();
    const about = (key: string) =>
      ({
        ...notification(key),
        version: 1,
        target: { kind: 'conversation', id },
      }) as NotificationDto;
    api.list.mockResolvedValue({
      items: [about('x'), about('y'), notification('z')],
      unread: 3,
      hasMore: false,
    });
    await settle();
    expect(api.readTarget).not.toHaveBeenCalled();
    await TestBed.inject(Router).navigateByUrl(`/work/chat/${id}`);
    await settle();
    expect(api.readTarget).toHaveBeenCalledOnce();
    expect(api.readTarget).toHaveBeenCalledWith({ kind: 'conversation', id });
    expect(
      store
        .items()
        .filter((item) => !item.readAt)
        .map((item) => item.id),
    ).toEqual(['z']);
    expect(store.unread()).toBe(1);
  });

  it('opens a notification by going there and leaves reading to the destination', async () => {
    const id = '01234567-89ab-4cde-8000-0123456789ab';
    const { store, api, settle } = setup();
    const item = {
      ...notification('x'),
      version: 1,
      target: { kind: 'conversation', id },
    } as NotificationDto;
    api.list.mockResolvedValue({ items: [item], unread: 1, hasMore: false });
    await settle();
    await store.activate(store.items()[0]);
    await settle();
    expect(TestBed.inject(Router).url).toBe(`/work/chat/${id}`);
    expect(api.read).not.toHaveBeenCalled();
    expect(api.readTarget).toHaveBeenCalledOnce();
    expect(store.unread()).toBe(0);
  });
});
