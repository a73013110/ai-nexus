import { ApplicationRef, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { AccountSecurityDto } from '@core/api/schema';
import { AuthService } from '@core/auth/auth-service';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { ApiError } from '@core/errors/safe-errors';
import { ToastService } from '@shared/ui/toast-service';
import { AccountSecurityApi } from './account-security-api';
import { AccountSecurityStore, emailBadge } from './account-security-store';
import { localPasswordProblem } from './local-login-dialog';
import { deliveryText } from './verification-code-field';

const security = (patch: Partial<AccountSecurityDto> = {}): AccountSecurityDto => ({
  displayName: '陳',
  account: 'chen',
  adAccount: 'chen',
  email: null,
  emailSource: null,
  emailVerifiedAt: null,
  emailEditable: true,
  localLogin: {
    allowed: true,
    enabled: false,
    account: null,
    passwordChangedAt: null,
    required: false,
  },
  currentMethod: 'ad',
  reauthMethods: ['password'],
  reauthenticationRequired: true,
  reauthenticatedUntil: null,
  mailAvailable: true,
  readOnly: false,
  ...patch,
});

function setup(initial = security()) {
  const generation = signal(1);
  const api = {
    get: vi.fn(async () => initial),
    reauthenticate: vi.fn(async () => ({
      security: security({ reauthenticationRequired: false }),
      csrfToken: 'renewed-token',
      signedOut: false,
    })),
    setLocalLogin: vi.fn(async () => ({
      security: security({ reauthenticationRequired: false }),
      csrfToken: 'after-change',
      signedOut: false,
    })),
    disableLocalLogin: vi.fn(),
  };
  const auth = { generation, guard: AuthService.prototype.guard, renewed: vi.fn(), load: vi.fn() };
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      AccountSecurityStore,
      { provide: AccountSecurityApi, useValue: api },
      { provide: WorkspaceSession, useValue: { auth, load: vi.fn(async () => true) } },
      { provide: AuthService, useValue: auth },
      { provide: ToastService, useValue: { success: vi.fn(), show: vi.fn() } },
    ],
  });
  const settle = () => TestBed.inject(ApplicationRef).whenStable();
  return { store: TestBed.inject(AccountSecurityStore), api, auth, settle };
}

describe('account security store', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('asks for a step-up first and then continues to the requested dialog', async () => {
    const { store, api, auth, settle } = setup();
    await settle();
    store.open('local-login');
    expect(store.dialog()).toBe('reauthenticate');
    await store.reauthenticate({ password: 'secret' });
    expect(api.reauthenticate).toHaveBeenCalledWith({ password: 'secret' });
    expect(auth.renewed).toHaveBeenCalledWith('renewed-token');
    expect(store.dialog()).toBe('local-login');
    expect(store.security()?.reauthenticationRequired).toBe(false);
  });

  it('opens dialogs directly while the step-up is fresh, and never for read-only sessions', async () => {
    const fresh = setup(security({ reauthenticationRequired: false }));
    await fresh.settle();
    fresh.store.open('email');
    expect(fresh.store.dialog()).toBe('email');
    TestBed.resetTestingModule();
    const testing = setup(security({ readOnly: true }));
    await testing.settle();
    testing.store.open('email');
    expect(testing.store.dialog()).toBeNull();
    expect(testing.store.editable()).toBe(false);
  });

  it('returns to the step-up when it expired on the server and renews the token after saving', async () => {
    const { store, api, auth, settle } = setup(security({ reauthenticationRequired: false }));
    await settle();
    api.setLocalLogin.mockRejectedValueOnce(new ApiError(403, 'reauthentication_required'));
    store.open('local-login');
    await store.setLocalLogin({ account: 'chen', password: 'a long passphrase' });
    expect(store.dialog()).toBe('reauthenticate');
    await store.reauthenticate({ password: 'secret' });
    expect(store.dialog()).toBe('local-login');
    await store.setLocalLogin({ account: 'chen', password: 'a long passphrase' });
    expect(auth.renewed).toHaveBeenLastCalledWith('after-change');
    expect(store.dialog()).toBeNull();
  });
});

describe('account security presentation', () => {
  it('labels the email source and verification', () => {
    expect(emailBadge(security()).label).toBe('尚未設定');
    expect(emailBadge(security({ email: 'a@b.com', emailSource: 'directory' })).label).toBe(
      'AD 提供',
    );
    expect(
      emailBadge(
        security({
          email: 'a@b.com',
          emailSource: 'user',
          emailVerifiedAt: '2026-10-20T00:00:00Z',
        }),
      ).tone,
    ).toBe('success');
    expect(emailBadge(security({ email: 'a@b.com', emailSource: 'admin' })).label).toBe('未驗證');
  });

  it('checks the local password like the server before sending it', () => {
    expect(localPasswordProblem('short', 'short')).toContain('12–128');
    expect(localPasswordProblem('a long passphrase', 'a long passphrase!')).toContain('不一致');
    expect(localPasswordProblem('a long passphrase', 'a long passphrase')).toBe('');
  });

  it('explains every delivery state', () => {
    expect(deliveryText('sent')).toContain('已寄出');
    expect(deliveryText('failed')).toContain('失敗');
    expect(deliveryText('unknown')).toContain('不明');
  });
});
