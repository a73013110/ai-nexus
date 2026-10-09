import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { AuthSession } from './types';
import { ApiTransport } from './api-transport';

const json = (value: unknown) =>
  new Response(JSON.stringify(value), { headers: { 'Content-Type': 'application/json' } });
const session = (userId: string) => ({ csrfToken: 'token', userId }) as unknown as AuthSession;

describe('api transport reads', () => {
  let fetch: ReturnType<typeof vi.fn>;
  beforeEach(() => {
    fetch = vi.fn(async () => json({ items: [1] }));
    vi.stubGlobal('fetch', fetch);
    TestBed.configureTestingModule({ providers: [provideRouter([])] });
  });
  afterEach(() => {
    TestBed.resetTestingModule();
    vi.unstubAllGlobals();
  });

  it('shares one request between identical reads in flight, each with its own copy', async () => {
    const http = TestBed.inject(ApiTransport);
    const [first, second] = await Promise.all([
      http.json<{ items: number[] }>('/models'),
      http.json<{ items: number[] }>('/models'),
    ]);
    expect(fetch).toHaveBeenCalledOnce();
    expect(second).toEqual(first);
    expect(second).not.toBe(first);
    await http.json('/models');
    expect(fetch).toHaveBeenCalledTimes(2);
  });

  it('reuses reference data until a change request or another identity', async () => {
    const http = TestBed.inject(ApiTransport);
    http.session(session('u1'));
    await http.reference('/models');
    await http.reference('/models');
    expect(fetch).toHaveBeenCalledOnce();

    await http.json('/preferences', 'PUT', {});
    await http.reference('/models');
    expect(fetch).toHaveBeenCalledTimes(3);

    http.session(session('u1'));
    await http.reference('/models');
    expect(fetch).toHaveBeenCalledTimes(3);
    http.session(session('u2'));
    await http.reference('/models');
    expect(fetch).toHaveBeenCalledTimes(4);
  });

  it('does not keep a failed reference read', async () => {
    fetch.mockResolvedValueOnce(new Response('{}', { status: 503 }));
    const http = TestBed.inject(ApiTransport);
    await expect(http.reference('/models')).rejects.toThrow();
    await expect(http.reference('/models')).resolves.toEqual({ items: [1] });
    expect(fetch).toHaveBeenCalledTimes(2);
  });
});
