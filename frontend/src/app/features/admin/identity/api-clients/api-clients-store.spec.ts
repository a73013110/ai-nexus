import { ApplicationRef, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { ApiClientDto } from '@core/api/schema';
import { AuthService } from '@core/auth/auth-service';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { ApiError, STALE_WRITE_MESSAGE } from '@core/errors/safe-errors';
import { ViewScope } from '@shared/browser/view-scope';
import { ToastService } from '@shared/ui/toast-service';
import { IdentityApi } from '../identity-api';
import { ApiClientsStore } from './api-clients-store';

const client: ApiClientDto = {
  id: 'c1',
  rowVersion: 5,
  name: '公文系統',
  contact: '王小明',
  enabled: true,
  keyHint: 'Ab3x',
  keyIssuedAt: '2026-10-01T00:00:00Z',
  createdAt: '2026-10-01T00:00:00Z',
  lastUsedAt: null,
  recentCalls: 0,
};
const issue = 'NX-' + '0'.repeat(32);

function setup() {
  const generation = signal(1);
  const api = {
    apiClients: vi.fn(async () => [client]),
    createApiClient: vi.fn(async (name: string) => ({
      client: { ...client, id: 'c2', name },
      key: 'ainexus_' + 'k'.repeat(43),
    })),
    updateApiClient: vi.fn(async () => {
      throw new ApiError(409, 'admin_write_conflict', undefined, issue);
    }),
    reissueApiClientKey: vi.fn(async () => ({ client, key: 'ainexus_' + 'n'.repeat(43) })),
    deleteApiClient: vi.fn(async () => undefined),
  };
  const toast = { success: vi.fn() };
  TestBed.configureTestingModule({
    providers: [
      ApiClientsStore,
      ViewScope,
      { provide: IdentityApi, useValue: api },
      { provide: ToastService, useValue: toast },
      {
        provide: WorkspaceSession,
        useValue: {
          auth: { generation },
          load: vi.fn(async () => true),
          has: (id: string) => id === 'identity.api-clients',
        },
      },
      { provide: AuthService, useValue: { generation, guard: AuthService.prototype.guard } },
    ],
  });
  const store = TestBed.inject(ApiClientsStore);
  const settle = () => TestBed.inject(ApplicationRef).whenStable();
  return { store, api, toast, settle };
}

describe('ApiClientsStore', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('returns the one-time key from create and re-reads the list', async () => {
    const { store, api, toast, settle } = setup();
    await settle();
    const outcome = await store.create({ name: '專案管理', contact: null });
    expect(outcome.ok && outcome.value.key).toMatch(/^ainexus_/);
    expect(toast.success).toHaveBeenCalledWith('已新增外部系統「專案管理」');
    await settle();
    expect(api.apiClients).toHaveBeenCalledTimes(2);
    expect(store.busy()).toBe(false);
  });

  it('sends the row version and spins only that row while reissuing', async () => {
    const { store, api, settle } = setup();
    await settle();
    const reissuing = store.reissue(client);
    expect(store.working('client:c1')).toBe(true);
    expect(store.busy()).toBe(true);
    expect((await reissuing).ok).toBe(true);
    expect(api.reissueApiClientKey).toHaveBeenCalledWith('c1', 5);
  });

  it('disables with the row values and version, spinning only that row', async () => {
    const { store, api, toast, settle } = setup();
    api.updateApiClient.mockResolvedValueOnce(undefined as never);
    await settle();
    const disabling = store.setEnabled(client, false);
    expect(store.working('client:c1')).toBe(true);
    expect((await disabling).ok).toBe(true);
    expect(api.updateApiClient).toHaveBeenCalledWith('c1', {
      name: '公文系統',
      contact: '王小明',
      enabled: false,
      rowVersion: 5,
    });
    expect(toast.success).toHaveBeenCalledWith('已停用外部系統「公文系統」');
  });

  it('reports a stale edit as stale so the page explains it and re-reads', async () => {
    const { store, api, toast, settle } = setup();
    await settle();
    const outcome = await store.edit(client, { name: '公文系統（GD）', contact: null });
    expect(outcome).toMatchObject({ ok: false, stale: true });
    expect(toast.success).not.toHaveBeenCalled();
    store.stale();
    expect(store.notice()).toBe(STALE_WRITE_MESSAGE);
    await settle();
    expect(api.apiClients.mock.calls.length).toBeGreaterThanOrEqual(2);
  });
});
