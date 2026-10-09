import { inject, Injectable, signal } from '@angular/core';
import { Router } from '@angular/router';
import type { AuthSession } from './types';
import { BrowserSession } from '../monitoring/browser-session';

export { ApiError } from './safe-errors';
import { ApiError } from './safe-errors';

const REFERENCE_MAX_AGE_MS = 30_000;
const parse = <T>(body: string): T => (body ? JSON.parse(body) : undefined) as T;

/** Shared authenticated transport for JSON, multipart uploads and SSE. */
@Injectable({ providedIn: 'root' })
export class ApiTransport {
  private readonly router = inject(Router);
  private readonly browserSession = inject(BrowserSession);
  private csrf = '';
  private identity: string | null = null;
  private readonly reads = new Map<string, Promise<string>>();
  private readonly references = new Map<string, { at: number; body: Promise<string> }>();
  readonly expired = signal(0);
  session(value: AuthSession) {
    this.csrf = value.csrfToken;
    const identity = value.userId
      ? value.userId + ':' + (value.testing?.administratorId ?? '')
      : null;
    if (identity !== this.identity) this.references.clear();
    this.identity = identity;
  }
  token(value: string) {
    this.csrf = value;
  }

  async response(
    path: string,
    method = 'GET',
    body?: unknown,
    extra?: Record<string, string>,
    signal?: AbortSignal,
  ): Promise<Response> {
    let response: Response;
    const multipart = body instanceof FormData;
    try {
      response = await fetch(`/api/v1${path}`, {
        method,
        credentials: 'same-origin',
        cache: 'no-store',
        signal,
        headers: {
          Accept: 'application/json',
          'X-Nexus-Session': this.browserSession.id,
          ...(body !== undefined && !multipart ? { 'Content-Type': 'application/json' } : {}),
          ...(method !== 'GET' ? { 'X-Nexus-CSRF': this.csrf } : {}),
          ...extra,
        },
        ...(body !== undefined ? { body: multipart ? body : JSON.stringify(body) } : {}),
      });
    } catch (error) {
      if (signal?.aborted) throw error;
      throw new ApiError(0, 'network_error', '連線中斷，請確認區網連線後重試。');
    }
    const identity = response.headers.get('X-Nexus-Identity');
    if (identity) {
      if (this.identity && identity !== this.identity && !path.startsWith('/auth/')) {
        this.csrf = '';
        this.expired.update((value) => value + 1);
        window.location.assign('/dashboard');
        throw new ApiError(409, 'identity_changed', '登入身分已變更，正在重新載入工作區。');
      }
      this.identity = identity;
    }
    if (!response.ok) {
      if (response.status === 401 && !path.startsWith('/auth/')) {
        this.csrf = '';
        this.references.clear();
        this.expired.update((value) => value + 1);
        if (!this.router.url.startsWith('/login'))
          void this.router.navigate(['/login'], { queryParams: { returnUrl: this.router.url } });
      }
      const problem = (await response.json().catch(() => ({}))) as {
        code?: string;
        title?: string;
        issueCode?: string;
      };
      throw new ApiError(
        response.status,
        problem.code ?? 'request_failed',
        problem.title ??
          (response.status === 401
            ? '登入已失效，請重新登入工作區。'
            : '服務暫時無法使用，請稍後重試。'),
        problem.issueCode,
      );
    }
    // Any change made from this browser may affect reference data, so it is read again.
    if (method !== 'GET') this.references.clear();
    return response;
  }

  async json<T>(
    path: string,
    method = 'GET',
    body?: unknown,
    extra?: Record<string, string>,
    signal?: AbortSignal,
  ): Promise<T> {
    // Identical reads in flight share one request; every caller parses its own copy.
    if (method === 'GET' && body === undefined && !extra && !signal) {
      let read = this.reads.get(path);
      if (!read) {
        read = this.text(path).finally(() => this.reads.delete(path));
        this.reads.set(path, read);
      }
      return parse<T>(await read);
    }
    const response = await this.response(path, method, body, extra, signal);
    return response.status === 204 ? (undefined as T) : (response.json() as Promise<T>);
  }

  /**
   * Slow-changing reference data (model catalog, policies) is reused for a short time. It is
   * dropped when the identity changes or after any change request from this browser.
   */
  async reference<T>(path: string, maxAge = REFERENCE_MAX_AGE_MS): Promise<T> {
    const cached = this.references.get(path);
    if (cached && Date.now() - cached.at < maxAge) return parse<T>(await cached.body);
    const body = this.text(path);
    const entry = { at: Date.now(), body };
    this.references.set(path, entry);
    body.catch(() => {
      if (this.references.get(path) === entry) this.references.delete(path);
    });
    return parse<T>(await body);
  }

  private async text(path: string) {
    const response = await this.response(path);
    return response.status === 204 ? '' : response.text();
  }
}
