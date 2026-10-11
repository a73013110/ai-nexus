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
    const { read, settle } = setup({ feature: 'admin', loader });
    await settle();
    expect(loader).not.toHaveBeenCalled();
    expect(read.error()).toContain('沒有使用此功能的權限');
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
