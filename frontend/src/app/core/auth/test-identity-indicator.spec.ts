import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { AuthSessionDto } from '../api/schema';
import { AuthService } from './auth-service';
import { IdentityVeil } from './identity-veil';
import { TestIdentityIndicator } from './test-identity-indicator';

const NOW = new Date('2026-10-28T08:00:00Z').getTime();

const testing = (minutes: number) =>
  ({
    authenticated: true,
    account: 'wang.xiaoming',
    displayName: '王小明',
    testing: {
      administratorId: 'admin',
      administratorName: '測試管理者',
      userId: 'user',
      expiresAt: new Date(NOW + minutes * 60_000).toISOString(),
    },
  }) as AuthSessionDto;

describe('TestIdentityIndicator', () => {
  beforeEach(() => vi.useFakeTimers({ now: NOW }));
  afterEach(() => {
    vi.useRealTimers();
    TestBed.resetTestingModule();
  });
  const setup = (session: AuthSessionDto | null, arrived = false) => {
    const auth = { session: signal(session), endTestIdentity: vi.fn(async () => undefined) };
    const veil = { arrived, covering: signal(arrived) };
    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: auth },
        { provide: IdentityVeil, useValue: veil },
      ],
    });
    const fixture = TestBed.createComponent(TestIdentityIndicator);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    const toggle = () => element.querySelector<HTMLButtonElement>('.notch-toggle');
    return { fixture, auth, veil, element, toggle };
  };

  it('stays away outside a test identity', () => {
    const { element } = setup({ authenticated: true, testing: null } as AuthSessionDto);
    expect(element.querySelector('[role=region]')).toBeNull();
  });

  it('shows who is being tested and the time left as a compact island', () => {
    const { element, toggle } = setup(testing(15));
    expect(toggle()?.getAttribute('aria-label')).toBe('測試身分：王小明，剩餘 15 分鐘');
    expect(element.querySelector('.notch-clock')?.textContent).toBe('15:00');
    expect(toggle()?.getAttribute('aria-expanded')).toBe('false');
    expect(element.querySelector('[aria-label=返回管理者]')).not.toBeNull();
  });

  it('introduces itself after an identity switch once the veil lifts', () => {
    const { fixture, veil, element, toggle } = setup(testing(15), true);
    expect(element.querySelector('[role=region]')).toBeNull();
    veil.covering.set(false);
    fixture.detectChanges();
    expect(toggle()?.getAttribute('aria-expanded')).toBe('true');
    expect(element.textContent).toContain('由 測試管理者 啟用，操作會留下稽核');
    vi.advanceTimersByTime(4500);
    fixture.detectChanges();
    expect(toggle()?.getAttribute('aria-expanded')).toBe('false');
  });

  it('warns in the last minute and returns on expiry', async () => {
    const { fixture, auth, element } = setup(testing(1.5));
    vi.advanceTimersByTime(30_000);
    fixture.detectChanges();
    expect(element.querySelector('.notch-shell')?.getAttribute('data-tone')).toBe('warning');
    expect(element.textContent).toContain('即將自動返回 測試管理者');
    await vi.advanceTimersByTimeAsync(60_000);
    expect(auth.endTestIdentity).toHaveBeenCalledOnce();
  });

  it('keeps the island open with the reason when returning fails', async () => {
    const { fixture, auth, element } = setup(testing(15));
    auth.endTestIdentity.mockRejectedValueOnce(new Error('offline'));
    element.querySelector<HTMLButtonElement>('[aria-label=返回管理者]')!.click();
    await vi.advanceTimersByTimeAsync(0);
    fixture.detectChanges();
    expect(element.querySelector('[role=alert]')).not.toBeNull();
    expect(element.querySelector('.notch-return')?.getAttribute('aria-busy')).toBeNull();
  });
});
