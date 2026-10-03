import { inject, Injectable } from '@angular/core';
import { ApiTransport } from './api-transport';
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
export { ApiError } from './api-transport';

@Injectable({ providedIn: 'root' })
export class NexusApi {
  private readonly http = inject(ApiTransport);
  async authSession(): Promise<AuthSession> {
    const session = await this.http.json<AuthSession>('/auth/session');
    this.http.session(session);
    return session;
  }
  async login(account: string, password: string): Promise<AuthSession> {
    const session = await this.http.json<AuthSession>('/auth/login', 'POST', { account, password });
    this.http.session(session);
    return session;
  }
  logout = () => this.http.json<AuthSession>('/auth/logout', 'POST');
  async me(): Promise<Me> {
    const me = await this.http.json<Me>('/me');
    this.http.token(me.csrfToken);
    return me;
  }
  models = () => this.http.json<Models>('/models');
  context = (body: ContextPreview, signal: AbortSignal) =>
    this.http.json<ContextUsage>('/context', 'POST', body, undefined, signal);
  conversations = (search = '', offset = 0, view = 'active', label = '') =>
    this.http.json<Conversation[]>(
      `/conversations?${new URLSearchParams({ search, offset: String(offset), view, label })}`,
    );
  conversation = (id: string) =>
    this.http.json<ConversationDetail>(`/conversations/${encodeURIComponent(id)}`);
  createConversation = () => this.http.json<Conversation>('/conversations', 'POST', {});
  rename = (id: string, title: string) =>
    this.http.json<Conversation>(`/conversations/${encodeURIComponent(id)}`, 'PATCH', { title });
  delete = (id: string) =>
    this.http.json<void>(`/conversations/${encodeURIComponent(id)}`, 'DELETE');
  branch = (id: string, leafId: string) =>
    this.http.json<void>(`/conversations/${encodeURIComponent(id)}/branch`, 'PATCH', { leafId });
  createRun = (request: CreateRun, key: string) =>
    this.http.json<Run>('/runs', 'POST', request, { 'Idempotency-Key': key });
  run = (id: string, signal?: AbortSignal) =>
    this.http.json<Run>(`/runs/${encodeURIComponent(id)}`, 'GET', undefined, undefined, signal);
  events = (id: string, after: number, signal: AbortSignal) =>
    this.http.response(
      `/runs/${encodeURIComponent(id)}/events?after=${after}`,
      'GET',
      undefined,
      { Accept: 'text/event-stream' },
      signal,
    );
  cancel = (id: string) => this.http.json<Run>(`/runs/${encodeURIComponent(id)}/cancel`, 'POST');
  preferences = (value: Preferences) => this.http.json<Preferences>('/preferences', 'PUT', value);
}
