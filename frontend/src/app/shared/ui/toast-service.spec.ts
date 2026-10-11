import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { AuthService } from '../../core/auth/auth-service';
import { HISTORY_LIMIT, ToastService } from './toast-service';

describe('ToastService', () => {
  const generation = signal(0);
  const setup = () => {
    TestBed.configureTestingModule({
      providers: [{ provide: AuthService, useValue: { generation } }],
    });
    return TestBed.inject(ToastService);
  };
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => {
    vi.useRealTimers();
    TestBed.resetTestingModule();
  });

  it('stacks newest first and keeps only the most recent five', () => {
    const toasts = setup();
    for (let i = 1; i <= 6; i++) toasts.success(`第 ${i} 則`);
    expect(toasts.toasts().map((x) => x.title)).toEqual([
      '第 6 則',
      '第 5 則',
      '第 4 則',
      '第 3 則',
      '第 2 則',
    ]);
  });

  it('replaces a toast shown again with the same id and restarts its timer', () => {
    const toasts = setup();
    toasts.show({ id: 'upload', tone: 'info', title: '正在上傳', duration: 0 });
    vi.advanceTimersByTime(10_000);
    toasts.show({ id: 'upload', title: '已上傳' });
    expect(toasts.toasts()).toMatchObject([
      { id: 'upload', tone: 'success', title: '已上傳', version: 2 },
    ]);
    vi.advanceTimersByTime(4_000);
    expect(toasts.toasts()).toEqual([]);
  });

  it('holds every timer while paused', () => {
    const toasts = setup();
    toasts.success('已儲存');
    vi.advanceTimersByTime(3_000);
    toasts.pause();
    vi.advanceTimersByTime(60_000);
    expect(toasts.toasts()).toHaveLength(1);
    toasts.resume();
    vi.advanceTimersByTime(1_000);
    expect(toasts.toasts()).toEqual([]);
  });

  it('forgets the previous account after sign-out', () => {
    const toasts = setup();
    toasts.success('已儲存');
    generation.update((value) => value + 1);
    TestBed.tick();
    expect(toasts.toasts()).toEqual([]);
    expect(toasts.history()).toEqual([]);
  });

  it('keeps the latest twenty settled toasts for the session with the time shown', () => {
    vi.setSystemTime(new Date('2026-10-11T06:00:00Z'));
    const toasts = setup();
    for (let i = 1; i <= HISTORY_LIMIT + 2; i++) toasts.success(`第 ${i} 則`);
    vi.advanceTimersByTime(60_000);
    expect(toasts.toasts()).toEqual([]);
    expect(toasts.history()).toHaveLength(HISTORY_LIMIT);
    expect(toasts.history()[0]).toMatchObject({
      title: `第 ${HISTORY_LIMIT + 2} 則`,
      tone: 'success',
      at: '2026-10-11T06:00:00.000Z',
    });
    expect(toasts.history().at(-1)!.title).toBe('第 3 則');
  });

  it('records progress only once it settles, and an update in place as one entry', () => {
    const toasts = setup();
    toasts.show({ id: 'upload', tone: 'info', title: '正在上傳', duration: 0 });
    expect(toasts.history()).toEqual([]);
    toasts.show({ id: 'upload', title: '已上傳' });
    toasts.show({ id: 'upload', title: '已上傳並辨識' });
    expect(toasts.history().map((x) => x.title)).toEqual(['已上傳並辨識']);
    vi.advanceTimersByTime(10_000);
    toasts.show({ id: 'upload', title: '已上傳' });
    expect(toasts.history().map((x) => x.title)).toEqual(['已上傳', '已上傳並辨識']);
    toasts.show({ id: 'cancelled', tone: 'info', title: '正在上傳', duration: 0 });
    toasts.dismiss('cancelled');
    expect(toasts.history()).toHaveLength(2);
  });

  it('runs an action once, whether from the toast or from the history', () => {
    const toasts = setup();
    const run = vi.fn();
    const id = toasts.show({ title: '已封存', action: { label: '復原', run } });
    const key = toasts.history()[0].key;
    toasts.replay(key);
    expect(run).toHaveBeenCalledTimes(1);
    expect(toasts.toasts()).toEqual([]);
    expect(toasts.history()[0].action).toBeUndefined();
    toasts.replay(key);
    toasts.act(id);
    expect(run).toHaveBeenCalledTimes(1);

    const other = toasts.show({ title: '已移除', action: { label: '復原', run } });
    toasts.act(other);
    expect(run).toHaveBeenCalledTimes(2);
    expect(toasts.history()[0]).toMatchObject({ title: '已移除', action: undefined });
  });
});
