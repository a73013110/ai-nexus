import { ApplicationRef, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { WorkspaceSession } from '../auth/workspace-session';
import { ApiError } from '../errors/safe-errors';
import { apiResource, type ApiResourceOptions } from './api-resource';

function setup<T, P>(options: ApiResourceOptions<T, P>, features = ['chat']) {
  const generation = signal(1);
  const session = {
    auth: { generation },
    load: vi.fn(async () => true),
    has: (feature: string) => features.includes(feature),
  };
  TestBed.configureTestingModule({ providers: [{ provide: WorkspaceSession, useValue: session }] });
  const read = TestBed.runInInjectionContext(() => apiResource(options));
  const settle = () => TestBed.inject(ApplicationRef).whenStable();
  return { read, generation, settle };
}

describe('apiResource', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('reads once the account is loaded and reports the first read as loading', async () => {
    const loader = vi.fn(async () => 'value');
    const { read, settle } = setup({ loader });
    TestBed.tick();
    expect(read.loading()).toBe(true);
    await settle();
    expect(read.value()).toBe('value');
    expect(read.loading()).toBe(false);
    expect(read.error()).toBe('');
    expect(loader).toHaveBeenCalledTimes(1);
  });

  it('keeps the shown value while a new request loads and drops it when a read fails', async () => {
    const id = signal(1);
    let fail = false;
    const { read, settle } = setup({
      params: () => id(),
      loader: async (value: number) => {
        if (fail) throw new ApiError(404, 'template_not_found');
        return value * 10;
      },
    });
    await settle();
    expect(read.value()).toBe(10);
    id.set(2);
    TestBed.tick();
    expect(read.value()).toBe(10);
    await settle();
    expect(read.value()).toBe(20);
    fail = true;
    read.reload();
    await settle();
    expect(read.value()).toBeUndefined();
    expect(read.error()).not.toBe('');
  });

  it('reads again after the first load when reloaded while it is in flight', async () => {
    let version = 1;
    let release: () => void = () => undefined;
    const loader = vi.fn(async () => {
      const read = version;
      if (read === 1) await new Promise<void>((done) => (release = done));
      return read;
    });
    const { read, settle } = setup({ loader });
    TestBed.tick();
    await vi.waitFor(() => expect(loader).toHaveBeenCalledOnce());
    expect(read.loading()).toBe(true);
    // 寫入在第一次讀取仍在途中時完成。
    version = 2;
    read.reload();
    release();
    await settle();
    expect(loader).toHaveBeenCalledTimes(2);
    expect(read.value()).toBe(2);
  });

  it('keeps polled reads quiet but shows a reload pressed while a poll is in flight', async () => {
    let calls = 0;
    let release: () => void = () => undefined;
    const loader = vi.fn(async () => {
      const call = ++calls;
      if (call > 1) await new Promise<void>((done) => (release = done));
      return call;
    });
    const { read, settle } = setup({ loader, poll: (value) => (value === 1 ? 5 : null) });
    await settle();
    expect(read.value()).toBe(1);
    await vi.waitFor(() => {
      TestBed.tick();
      expect(loader).toHaveBeenCalledTimes(2);
    });
    // 輪詢中的讀取不淡化畫面。
    expect(read.refreshing()).toBe(false);
    expect(read.loading()).toBe(false);
    read.reload();
    TestBed.tick();
    expect(read.refreshing()).toBe(true);
    // 輪詢那次結束後，以使用者的要求再讀一次（輪詢可能早於某次寫入）。
    release();
    await vi.waitFor(() => {
      TestBed.tick();
      expect(loader).toHaveBeenCalledTimes(3);
    });
    expect(read.refreshing()).toBe(true);
    release();
    await settle();
    expect(read.value()).toBe(3);
    expect(read.refreshing()).toBe(false);
  });

  it('reports a user reload as refreshing but not as loading', async () => {
    let release: () => void = () => undefined;
    let calls = 0;
    const loader = vi.fn(async () => {
      const call = ++calls;
      if (call > 1) await new Promise<void>((done) => (release = done));
      return call;
    });
    const { read, settle } = setup({ loader });
    await settle();
    read.reload();
    await vi.waitFor(() => {
      TestBed.tick();
      expect(loader).toHaveBeenCalledTimes(2);
    });
    expect(read.refreshing()).toBe(true);
    expect(read.loading()).toBe(false);
    expect(read.value()).toBe(1);
    release();
    await settle();
    expect(read.refreshing()).toBe(false);
    expect(read.value()).toBe(2);
  });

  it('stays idle without a request', async () => {
    const loader = vi.fn(async () => 'value');
    const { read, settle } = setup({ params: () => undefined, loader });
    await settle();
    expect(loader).not.toHaveBeenCalled();
    expect(read.value()).toBeUndefined();
    expect(read.loading()).toBe(false);
  });

  it('refuses reads for a feature the account does not have', async () => {
    const loader = vi.fn(async () => 'value');
    const { read, settle } = setup({ feature: 'identity.users', loader });
    await settle();
    expect(loader).not.toHaveBeenCalled();
    expect(read.error()).toContain('沒有使用此功能的權限');
  });

  it('reads when the account has any one of several features', async () => {
    const loader = vi.fn(async () => 'value');
    const { read, settle } = setup(
      { feature: ['identity.users', 'access-control.roles'], loader },
      ['access-control.roles'],
    );
    await settle();
    expect(read.value()).toBe('value');
  });

  it('starts over when the signed-in identity changes', async () => {
    let account = 'first';
    const { read, generation, settle } = setup({ loader: async () => account });
    await settle();
    expect(read.value()).toBe('first');
    account = 'second';
    generation.set(2);
    TestBed.tick();
    expect(read.value()).toBeUndefined();
    await settle();
    expect(read.value()).toBe('second');
  });
});
