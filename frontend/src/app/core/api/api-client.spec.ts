import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { AuthSessionDto } from './schema';
import { ApiClient, apiUrl } from './api-client';

const json = (value: unknown) =>
  new Response(JSON.stringify(value), { headers: { 'Content-Type': 'application/json' } });
const session = (userId: string) => ({ csrfToken: 'token', userId }) as unknown as AuthSessionDto;

describe('api urls', () => {
  it('fills path parameters and skips absent query values', () => {
    expect(
      apiUrl('/api/v1/projects/{id}/templates/{key}', { path: { id: 'a b', key: 'k/1' } }),
    ).toBe('/api/v1/projects/a%20b/templates/k%2F1');
    expect(
      apiUrl('/api/v1/conversations', {
        query: { search: '', offset: 0, label: undefined, view: null, archived: false },
      }),
    ).toBe('/api/v1/conversations?search=&offset=0&archived=false');
    expect(apiUrl('/api/v1/files', { query: { type: ['a', 'b'] } })).toBe(
      '/api/v1/files?type=a&type=b',
    );
  });
  it('rejects a missing path parameter instead of calling a wrong url', () => {
    expect(() => apiUrl('/api/v1/projects/{id}')).toThrow('Missing path parameter: id');
  });
});

describe('api client reads', () => {
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
    const api = TestBed.inject(ApiClient);
    const [first, second] = await Promise.all([
      api.get('/api/v1/models'),
      api.get('/api/v1/models'),
    ]);
    expect(fetch).toHaveBeenCalledOnce();
    expect(fetch.mock.calls[0][0]).toBe('/api/v1/models');
    expect(second).toEqual(first);
    expect(second).not.toBe(first);
    await api.get('/api/v1/models');
    expect(fetch).toHaveBeenCalledTimes(2);
  });

  it('reuses reference data until a change request or another identity', async () => {
    const api = TestBed.inject(ApiClient);
    api.session(session('u1'));
    await api.reference('/api/v1/models');
    await api.reference('/api/v1/models');
    expect(fetch).toHaveBeenCalledOnce();

    await api.put('/api/v1/preferences', {
      body: { theme: 'system', reducedMotion: false, defaultModelId: null },
    });
    await api.reference('/api/v1/models');
    expect(fetch).toHaveBeenCalledTimes(3);

    api.session(session('u1'));
    await api.reference('/api/v1/models');
    expect(fetch).toHaveBeenCalledTimes(3);
    api.session(session('u2'));
    await api.reference('/api/v1/models');
    expect(fetch).toHaveBeenCalledTimes(4);
  });

  it('does not keep a failed reference read', async () => {
    fetch.mockResolvedValueOnce(new Response('{}', { status: 503 }));
    const api = TestBed.inject(ApiClient);
    await expect(api.reference('/api/v1/models')).rejects.toThrow();
    await expect(api.reference('/api/v1/models')).resolves.toEqual({ items: [1] });
    expect(fetch).toHaveBeenCalledTimes(2);
  });

  it('sends the JSON body with the CSRF token and returns nothing for 204', async () => {
    fetch.mockResolvedValueOnce(new Response(null, { status: 204 }));
    const api = TestBed.inject(ApiClient);
    api.token('csrf');
    const result = await api.patch('/api/v1/conversations/{id}/branch', {
      path: { id: 'c1' },
      body: { leafId: 'm1' },
    });
    expect(result).toBeUndefined();
    const [url, init] = fetch.mock.calls[0];
    expect(url).toBe('/api/v1/conversations/c1/branch');
    expect(init.method).toBe('PATCH');
    expect(init.headers['X-Nexus-CSRF']).toBe('csrf');
    expect(JSON.parse(init.body)).toEqual({ leafId: 'm1' });
  });
});
