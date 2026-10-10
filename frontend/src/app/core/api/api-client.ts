import { inject, Injectable, signal } from '@angular/core';
import { Router } from '@angular/router';
import type { AuthSessionDto, paths } from './schema';
import { BrowserSession } from '../monitoring/browser-session';
import { ApiError } from './safe-errors';

export { ApiError } from './safe-errors';

type Method = 'get' | 'post' | 'put' | 'patch' | 'delete';
/** Every contract path that defines the given method. */
export type ApiPath<M extends Method> = {
  [P in keyof paths]: paths[P][M] extends undefined ? never : P;
}[keyof paths];
type Operation<P extends keyof paths, M extends Method> = NonNullable<paths[P][M]>;
type Parameters<O> = O extends { parameters: infer V } ? V : never;
type PathParameters<O> = Parameters<O> extends { path: infer V } ? V : never;
type QueryParameters<O> = Parameters<O> extends { query?: infer V } ? NonNullable<V> : never;
type RequestBody<O> = O extends { requestBody?: infer V } ? NonNullable<V> : never;
type JsonBody<O> = [RequestBody<O>] extends [never]
  ? never
  : RequestBody<O> extends { content: { 'application/json': infer V } }
    ? V
    : never;
type Fields<O> = ([PathParameters<O>] extends [never] ? object : { path: PathParameters<O> }) &
  ([QueryParameters<O>] extends [never]
    ? object
    : object extends QueryParameters<O>
      ? { query?: QueryParameters<O> }
      : { query: QueryParameters<O> }) &
  ([JsonBody<O>] extends [never] ? object : { body: JsonBody<O> });
/** Path, query and body exactly as the contract declares them, plus transport options. */
export type ApiRequest<O> = Fields<O> & { headers?: Record<string, string>; signal?: AbortSignal };
type Arguments<O> = object extends Fields<O> ? [request?: ApiRequest<O>] : [request: ApiRequest<O>];
type Success<O> = O extends { responses: infer R } ? R[keyof R & (200 | 201 | 202 | 204)] : never;
type Content<R> = R extends { content: { 'application/json': infer V } } ? V : undefined;
/** The query parameters an operation accepts. */
export type ApiQuery<P extends keyof paths, M extends Method> = QueryParameters<Operation<P, M>>;
/** The JSON body of an operation's success response; `undefined` for 204. */
export type ApiResponse<P extends keyof paths, M extends Method> = Content<
  Success<Operation<P, M>>
>;

interface Untyped {
  path?: Record<string, string | number>;
  query?: Record<string, unknown>;
  body?: unknown;
  headers?: Record<string, string>;
  signal?: AbortSignal;
}

const REFERENCE_MAX_AGE_MS = 30_000;
const parse = <T>(body: string): T => (body ? JSON.parse(body) : undefined) as T;
const isAuthPath = (url: string) => url.startsWith('/api/v1/auth/');

/** Fill `{name}` segments and append the query, skipping absent values and repeating arrays. */
export function apiUrl(path: string, request: Untyped = {}): string {
  const url = path.replace(/\{(\w+)\}/g, (_, name: string) => {
    const value = request.path?.[name];
    if (value === undefined) throw new Error(`Missing path parameter: ${name}`);
    return encodeURIComponent(String(value));
  });
  const query = new URLSearchParams();
  for (const [name, value] of Object.entries(request.query ?? {}))
    for (const item of Array.isArray(value) ? value : [value])
      if (item !== undefined && item !== null) query.append(name, String(item));
  const search = query.toString();
  return search ? `${url}?${search}` : url;
}

/** A typed link for the browser itself to load (`<a href>`, `<img src>`), e.g. file content. */
export function apiHref<P extends ApiPath<'get'>>(
  path: P,
  ...[request]: Arguments<Operation<P, 'get'>>
): string {
  return apiUrl(path, request as Untyped);
}

/**
 * The only way the app calls the API. Paths, parameters, bodies and responses are typed from the
 * generated contract (schema.ts), so a changed DTO fails the frontend build instead of production.
 * It also owns the session headers, CSRF token, identity checks and short-lived read caches.
 */
@Injectable({ providedIn: 'root' })
export class ApiClient {
  private readonly router = inject(Router);
  private readonly browserSession = inject(BrowserSession);
  private csrf = '';
  private identity: string | null = null;
  private readonly reads = new Map<string, Promise<string>>();
  private readonly references = new Map<string, { at: number; body: Promise<string> }>();
  readonly expired = signal(0);

  session(value: AuthSessionDto) {
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

  get<P extends ApiPath<'get'>>(path: P, ...[request]: Arguments<Operation<P, 'get'>>) {
    return this.json<ApiResponse<P, 'get'>>('GET', path, request);
  }
  post<P extends ApiPath<'post'>>(path: P, ...[request]: Arguments<Operation<P, 'post'>>) {
    return this.json<ApiResponse<P, 'post'>>('POST', path, request);
  }
  put<P extends ApiPath<'put'>>(path: P, ...[request]: Arguments<Operation<P, 'put'>>) {
    return this.json<ApiResponse<P, 'put'>>('PUT', path, request);
  }
  patch<P extends ApiPath<'patch'>>(path: P, ...[request]: Arguments<Operation<P, 'patch'>>) {
    return this.json<ApiResponse<P, 'patch'>>('PATCH', path, request);
  }
  delete<P extends ApiPath<'delete'>>(path: P, ...[request]: Arguments<Operation<P, 'delete'>>) {
    return this.json<ApiResponse<P, 'delete'>>('DELETE', path, request);
  }

  /**
   * Slow-changing reference data (model catalog, policies) is reused for a short time. It is
   * dropped when the identity changes or after any change request from this browser.
   */
  async reference<P extends ApiPath<'get'>>(
    path: P,
    ...[request]: Arguments<Operation<P, 'get'>>
  ): Promise<ApiResponse<P, 'get'>> {
    const url = apiUrl(path, request as Untyped);
    const cached = this.references.get(url);
    if (cached && Date.now() - cached.at < REFERENCE_MAX_AGE_MS) return parse(await cached.body);
    const body = this.text(url);
    const entry = { at: Date.now(), body };
    this.references.set(url, entry);
    body.catch(() => {
      if (this.references.get(url) === entry) this.references.delete(url);
    });
    return parse(await body);
  }

  /** Multipart upload; the contract does not describe form bodies, only the JSON response. */
  async upload<P extends ApiPath<'post'>>(
    path: P,
    form: FormData,
    ...[request]: Arguments<Operation<P, 'post'>>
  ): Promise<ApiResponse<P, 'post'>> {
    const untyped = request as Untyped | undefined;
    const response = await this.send('POST', apiUrl(path, untyped), form, untyped);
    return (await response.json()) as ApiResponse<P, 'post'>;
  }

  /** The raw response for event streams and file downloads, after the same checks as JSON calls. */
  open<P extends ApiPath<'get'>>(path: P, ...[request]: Arguments<Operation<P, 'get'>>) {
    const untyped = request as Untyped | undefined;
    return this.send('GET', apiUrl(path, untyped), undefined, untyped);
  }

  private async json<T>(method: string, path: string, request?: unknown): Promise<T> {
    const untyped = (request ?? {}) as Untyped;
    const url = apiUrl(path, untyped);
    // Identical reads in flight share one request; every caller parses its own copy.
    if (method === 'GET' && !untyped.headers && !untyped.signal) {
      let read = this.reads.get(url);
      if (!read) {
        read = this.text(url).finally(() => this.reads.delete(url));
        this.reads.set(url, read);
      }
      return parse<T>(await read);
    }
    const response = await this.send(method, url, untyped.body, untyped);
    return response.status === 204 ? (undefined as T) : ((await response.json()) as T);
  }

  private async text(url: string) {
    const response = await this.send('GET', url);
    return response.status === 204 ? '' : response.text();
  }

  private async send(
    method: string,
    url: string,
    body?: unknown,
    request: Untyped = {},
  ): Promise<Response> {
    let response: Response;
    const multipart = body instanceof FormData;
    try {
      response = await fetch(url, {
        method,
        credentials: 'same-origin',
        cache: 'no-store',
        signal: request.signal,
        headers: {
          Accept: 'application/json',
          'X-Nexus-Session': this.browserSession.id,
          ...(body !== undefined && !multipart ? { 'Content-Type': 'application/json' } : {}),
          ...(method !== 'GET' ? { 'X-Nexus-CSRF': this.csrf } : {}),
          ...request.headers,
        },
        ...(body !== undefined ? { body: multipart ? body : JSON.stringify(body) } : {}),
      });
    } catch (error) {
      if (request.signal?.aborted) throw error;
      throw new ApiError(0, 'network_error', '連線中斷，請確認區網連線後重試。');
    }
    const identity = response.headers.get('X-Nexus-Identity');
    if (identity) {
      if (this.identity && identity !== this.identity && !isAuthPath(url)) {
        this.csrf = '';
        this.expired.update((value) => value + 1);
        window.location.assign('/dashboard');
        throw new ApiError(409, 'identity_changed', '登入身分已變更，正在重新載入工作區。');
      }
      this.identity = identity;
    }
    if (!response.ok) {
      if (response.status === 401 && !isAuthPath(url)) {
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
}
