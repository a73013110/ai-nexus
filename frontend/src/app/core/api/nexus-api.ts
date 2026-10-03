import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import type {
  Conversation,
  ConversationDetail,
  CreateRun,
  Me,
  Models,
  Preferences,
  Run,
  AuthSession,
  ContextPreview,
  ContextUsage,
} from './types';

export class ApiError extends Error {
  constructor(
    readonly status: number,
    readonly code: string,
    message: string,
  ) {
    super(message);
  }
}

@Injectable({ providedIn: 'root' })
export class NexusApi {
  private readonly router = inject(Router);
  private csrf = '';
  private ldap = false;
  async authSession(): Promise<AuthSession> {
    const session = await this.request<AuthSession>('/auth/session');
    this.csrf = session.csrfToken;
    this.ldap = session.mode === 'Ldap';
    return session;
  }
  async login(account: string, password: string): Promise<AuthSession> {
    const session = await this.request<AuthSession>('/auth/login', 'POST', { account, password });
    this.csrf = session.csrfToken;
    return session;
  }
  logout = () => this.request<AuthSession>('/auth/logout', 'POST');
  async me(): Promise<Me> {
    const me = await this.request<Me>('/me');
    this.csrf = me.csrfToken;
    return me;
  }
  models = () => this.request<Models>('/models');
  context = (body: ContextPreview, signal: AbortSignal) =>
    this.request<ContextUsage>('/context', 'POST', body, undefined, signal);
  conversations = (search = '', offset = 0) =>
    this.request<Conversation[]>(
      `/conversations?search=${encodeURIComponent(search)}&offset=${offset}`,
    );
  conversation = (id: string) =>
    this.request<ConversationDetail>(`/conversations/${encodeURIComponent(id)}`);
  createConversation = () => this.request<Conversation>('/conversations', 'POST', {});
  rename = (id: string, title: string) =>
    this.request<Conversation>(`/conversations/${id}`, 'PATCH', { title });
  delete = (id: string) => this.request<void>(`/conversations/${id}`, 'DELETE');
  branch = (id: string, leafId: string) =>
    this.request<void>(`/conversations/${id}/branch`, 'PATCH', { leafId });
  createRun = (request: CreateRun, key: string) =>
    this.request<Run>('/runs', 'POST', request, { 'Idempotency-Key': key });
  run = (id: string) => this.request<Run>(`/runs/${id}`);
  cancel = (id: string) => this.request<Run>(`/runs/${id}/cancel`, 'POST');
  preferences = (value: Preferences) => this.request<Preferences>('/preferences', 'PUT', value);

  private async request<T>(
    path: string,
    method = 'GET',
    body?: unknown,
    extra?: Record<string, string>,
    signal?: AbortSignal,
  ): Promise<T> {
    let response: Response;
    try {
      response = await fetch(`/api/v1${path}`, {
        method,
        credentials: 'same-origin',
        cache: 'no-store',
        signal,
        headers: {
          Accept: 'application/json',
          ...(body !== undefined ? { 'Content-Type': 'application/json' } : {}),
          ...(method !== 'GET' ? { 'X-Nexus-CSRF': this.csrf } : {}),
          ...extra,
        },
        ...(body !== undefined ? { body: JSON.stringify(body) } : {}),
      });
    } catch (error) {
      if (signal?.aborted) throw error;
      throw new ApiError(0, 'network_error', '連線中斷，請確認區網連線後重試。');
    }
    if (!response.ok) {
      if (response.status === 401 && this.ldap && !path.startsWith('/auth/'))
        void this.router.navigate(['/login'], { queryParams: { returnUrl: this.router.url } });
      const problem = (await response.json().catch(() => ({}))) as {
        code?: string;
        title?: string;
      };
      throw new ApiError(
        response.status,
        problem.code ?? 'request_failed',
        problem.title ??
          (response.status === 401
            ? 'Windows 驗證尚未完成，請使用公司電腦開啟，或由管理員確認登入設定。'
            : '服務暫時無法使用，請稍後重試。'),
      );
    }
    if (response.status === 204) return undefined as T;
    return response.json() as Promise<T>;
  }
}
