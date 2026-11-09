import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiClient } from '../api/api-client';
import type { AuthSessionDto } from '../api/schema';
import { AuthService } from './auth-service';
import { SessionLock } from './session-lock';

const session = (
  lockedBy: string | null,
  secondsUntilLock = 1800,
  idleLockMinutes = 30,
  secondsUntilReauthentication = 28_800,
) =>
  ({
    authenticated: true,
    account: 'fixture',
    csrfToken: 'token',
    userId: 'u1',
    lifetime: {
      lockedBy,
      secondsUntilLock,
      idleLockMinutes,
      reauthenticationHours: 8,
      secondsUntilReauthentication,
    },
  }) as unknown as AuthSessionDto;
const json = (value: unknown) =>
  new Response(JSON.stringify(value), { headers: { 'Content-Type': 'application/json' } });

describe('session lock', () => {
  let fetch: ReturnType<typeof vi.fn>;
  let current: AuthSessionDto;
  const calls = (path: string) =>
    fetch.mock.calls.filter(([url]) => String(url).endsWith(path)).length;
  const settle = async () => {
    await vi.advanceTimersByTimeAsync(0);
    TestBed.tick();
    await vi.advanceTimersByTimeAsync(0);
  };

  beforeEach(() => {
    vi.useFakeTimers();
    current = session(null);
    fetch = vi.fn(async () => json(current));
    vi.stubGlobal('fetch', fetch);
    vi.stubGlobal('matchMedia', () => ({
      matches: false,
      addEventListener: () => undefined,
      removeEventListener: () => undefined,
    }));
    TestBed.configureTestingModule({ providers: [provideRouter([])] });
    TestBed.inject(SessionLock);
  });
  afterEach(() => {
    TestBed.resetTestingModule();
    vi.unstubAllGlobals();
    vi.useRealTimers();
  });

  it('closes the request gate only when the server reports a lock', async () => {
    const auth = TestBed.inject(AuthService);
    const client = TestBed.inject(ApiClient);
    auth.adopt(session(null));
    await settle();
    expect(client.locked()).toBe(false);
    auth.adopt(session('idle', 0));
    await settle();
    expect(client.locked()).toBe(true);
    TestBed.inject(SessionLock).finish(session(null));
    await settle();
    expect(client.locked()).toBe(false);
  });

  it('reports activity at most once a minute, with the latest interaction trailing', async () => {
    TestBed.inject(AuthService).adopt(session(null));
    await settle();
    document.dispatchEvent(new Event('keydown'));
    await settle();
    expect(calls('/auth/activity')).toBe(1);
    document.dispatchEvent(new Event('keydown'));
    document.dispatchEvent(new Event('pointerdown'));
    await settle();
    expect(calls('/auth/activity')).toBe(1);
    await vi.advanceTimersByTimeAsync(60_000);
    expect(calls('/auth/activity')).toBe(2);
  });

  it('reports within half of a very short idle limit so active users stay unlocked', async () => {
    current = session(null, 60, 1);
    TestBed.inject(AuthService).adopt(current);
    await settle();
    document.dispatchEvent(new Event('keydown'));
    await settle();
    expect(calls('/auth/activity')).toBe(1);
    document.dispatchEvent(new Event('keydown'));
    await vi.advanceTimersByTimeAsync(30_000);
    expect(calls('/auth/activity')).toBe(2);
  });

  it('asks the server at the deadline instead of locking on its own', async () => {
    const lock = TestBed.inject(SessionLock);
    TestBed.inject(AuthService).adopt(session(null, 5));
    await settle();
    fetch.mockClear();
    current = session(null, 1800);
    await vi.advanceTimersByTimeAsync(5000);
    expect(calls('/auth/session')).toBe(1);
    await settle();
    expect(lock.reason()).toBeNull();
    current = session('idle', 0);
    await vi.advanceTimersByTimeAsync(1800_000);
    await settle();
    expect(lock.reason()).toBe('idle');
  });

  it('counts down from the server deadline and warns near the end', async () => {
    const lock = TestBed.inject(SessionLock);
    TestBed.inject(AuthService).adopt(session(null, 125));
    await settle();
    expect(lock.countdownText()).toBe('2:05 後鎖定');
    expect(lock.warning()).toBe(false);
    await vi.advanceTimersByTimeAsync(6000);
    expect(lock.countdownText()).toBe('1:59 後鎖定');
    expect(lock.warning()).toBe(true);
  });

  it('resets the idle countdown on interaction before the throttled report arrives', async () => {
    const lock = TestBed.inject(SessionLock);
    TestBed.inject(AuthService).adopt(session(null, 100));
    await settle();
    document.dispatchEvent(new Event('pointerdown'));
    expect(lock.countdown()?.seconds).toBe(1800);
    expect(lock.warning()).toBe(false);
  });

  it('does not let interaction extend the reauthentication deadline', async () => {
    const lock = TestBed.inject(SessionLock);
    TestBed.inject(AuthService).adopt(session(null, 90, 30, 90));
    await settle();
    document.dispatchEvent(new Event('pointerdown'));
    expect(lock.countdown()).toMatchObject({ seconds: 90, by: 'reauthentication' });
    expect(lock.countdownText()).toBe('1:30 後需要驗證');
  });

  it('warns only in the second half of a very short idle limit', async () => {
    const lock = TestBed.inject(SessionLock);
    TestBed.inject(AuthService).adopt(session(null, 60, 1));
    await settle();
    expect(lock.warning()).toBe(false);
    await vi.advanceTimersByTimeAsync(30_000);
    expect(lock.warning()).toBe(true);
  });

  it('locks through the server after an in-flight report settles', async () => {
    const lock = TestBed.inject(SessionLock);
    TestBed.inject(AuthService).adopt(session(null));
    await settle();
    let finishReport = (_: Response) => {};
    fetch.mockImplementationOnce(
      () => new Promise<Response>((resolve) => (finishReport = resolve)),
    );
    document.dispatchEvent(new Event('keydown'));
    await settle();
    current = session('manual', 0);
    const locking = lock.lockNow();
    await settle();
    expect(calls('/auth/lock')).toBe(0);
    finishReport(json(session(null)));
    await locking;
    await settle();
    expect(calls('/auth/lock')).toBe(1);
    expect(lock.reason()).toBe('manual');
    expect(lock.countdown()).toBeNull();
  });

  it('hides the countdown and manual lock for a test identity', async () => {
    const lock = TestBed.inject(SessionLock);
    TestBed.inject(AuthService).adopt({
      ...session(null),
      testing: { userId: 'u2' },
    } as unknown as AuthSessionDto);
    await settle();
    expect(lock.canLock()).toBe(false);
    expect(lock.countdown()).toBeNull();
  });
});
