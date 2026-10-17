import { inject, Injectable, signal } from '@angular/core';
import { Router } from '@angular/router';
import type { AuthSessionDto, paths } from './schema';
import { BrowserSession } from '../monitoring/browser-session';
import { ApiError } from '../errors/safe-errors';

type Method = 'get' | 'post' | 'put' | 'patch' | 'delete';
/** 定義了指定方法的所有契約路徑。 */
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
/** 路徑、查詢與請求主體依契約原樣保留，另加傳輸選項。 */
export type ApiRequest<O> = Fields<O> & { headers?: Record<string, string>; signal?: AbortSignal };
type Arguments<O> = object extends Fields<O> ? [request?: ApiRequest<O>] : [request: ApiRequest<O>];
type Success<O> = O extends { responses: infer R } ? R[keyof R & (200 | 201 | 202 | 204)] : never;
type Content<R> = R extends { content: { 'application/json': infer V } } ? V : undefined;
/** 操作接受的查詢參數。 */
export type ApiQuery<P extends keyof paths, M extends Method> = QueryParameters<Operation<P, M>>;
/** 操作成功回應的 JSON 主體；204 時為 `undefined`。 */
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
/** 以秒為單位的 `Retry-After`；伺服器只會送這種格式。 */
const retryAfterSeconds = (value: string | null) =>
  value && /^\d{1,5}$/.test(value) ? Number(value) : undefined;

/** 填入 `{name}` 片段並附加查詢；略過不存在的值，陣列則重複附加。 */
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

/** 給瀏覽器自己載入的型別化連結（`<a href>`、`<img src>`），例如檔案內容。 */
export function apiHref<P extends ApiPath<'get'>>(
  path: P,
  ...[request]: Arguments<Operation<P, 'get'>>
): string {
  return apiUrl(path, request as Untyped);
}

/**
 * 應用程式呼叫 API 的唯一途徑。路徑、參數、主體與回應都由產生的契約（schema.ts）決定型別，
 * 因此 DTO 變更會讓前端建置失敗，而不是等到正式環境才出錯。
 * 此處也負責工作階段標頭、CSRF token、身分檢查與短期讀取快取。
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
   * 變動緩慢的參考資料（模型目錄、政策）會短暫重用，
   * 身分改變，或此瀏覽器送出任何變更請求之後，就會丟棄。
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

  /** 多部分上傳；契約不描述表單主體，只描述 JSON 回應。 */
  async upload<P extends ApiPath<'post'>>(
    path: P,
    form: FormData,
    ...[request]: Arguments<Operation<P, 'post'>>
  ): Promise<ApiResponse<P, 'post'>> {
    const untyped = request as Untyped | undefined;
    const response = await this.send('POST', apiUrl(path, untyped), form, untyped);
    return (await response.json()) as ApiResponse<P, 'post'>;
  }

  /** 事件串流與檔案下載的原始回應，經過與 JSON 呼叫相同的檢查。 */
  open<P extends ApiPath<'get'>>(path: P, ...[request]: Arguments<Operation<P, 'get'>>) {
    const untyped = request as Untyped | undefined;
    return this.send('GET', apiUrl(path, untyped), undefined, untyped);
  }

  private async json<T>(method: string, path: string, request?: unknown): Promise<T> {
    const untyped = (request ?? {}) as Untyped;
    const url = apiUrl(path, untyped);
    // 進行中的相同讀取共用一次請求；每個呼叫端各自解析自己的副本。
    if (method === 'GET' && !untyped.headers && !untyped.signal) {
      let read = this.reads.get(url);
      if (!read) {
        const started: Promise<string> = this.text(url).finally(() => {
          if (this.reads.get(url) === started) this.reads.delete(url);
        });
        this.reads.set(url, (read = started));
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
        retryAfterSeconds(response.headers.get('Retry-After')),
      );
    }
    // 此瀏覽器的任何變更都可能影響參考資料，所以之後會重新讀取。進行中的讀取
    // 可能早於變更，因此變更之後的讀取絕不加入那次請求。
    if (method !== 'GET') {
      this.references.clear();
      this.reads.clear();
    }
    return response;
  }
}
