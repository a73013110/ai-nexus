import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { AuthService } from '../../core/auth/auth-service';
import { ToastService } from './toast-service';

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
  });
});
