import { inject, Injectable, signal } from '@angular/core';
import { Router } from '@angular/router';
import type { AuthSession } from './types';

export { ApiError } from './safe-errors';
import { ApiError } from './safe-errors';

/** Shared authenticated transport for JSON, multipart uploads and SSE. */
@Injectable({ providedIn: 'root' })
export class ApiTransport {
  private readonly router = inject(Router);
  private csrf = '';
  private identity: string | null = null;
  readonly expired = signal(0);
  session(value: AuthSession) {
    this.csrf = value.csrfToken;
    this.identity = value.userId
      ? value.userId + ':' + (value.testing?.administratorId ?? '')
      : null;
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
    return response;
  }

  async json<T>(
    path: string,
    method = 'GET',
    body?: unknown,
    extra?: Record<string, string>,
    signal?: AbortSignal,
  ): Promise<T> {
    const response = await this.response(path, method, body, extra, signal);
    return response.status === 204 ? (undefined as T) : (response.json() as Promise<T>);
  }
}
