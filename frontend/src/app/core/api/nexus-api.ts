import { inject, Injectable } from '@angular/core';
import { ApiClient } from './api-client';
import type {
  AuthSessionDto,
  ContextPreviewRequest,
  CreateRunRequest,
  PreferencesDto,
} from './schema';
export { ApiError } from './api-client';

/** Sign-in, the conversation list and the run lifecycle used by the chat workspace. */
@Injectable({ providedIn: 'root' })
export class NexusApi {
  private readonly api = inject(ApiClient);
  private async remember(request: Promise<AuthSessionDto>) {
    const session = await request;
    this.api.session(session);
    return session;
  }
  authSession = () => this.remember(this.api.get('/api/v1/auth/session'));
  login = (account: string, password: string, method = 'ad') =>
    this.remember(this.api.post('/api/v1/auth/login', { body: { account, password, method } }));
  windowsLogin = () => this.remember(this.api.get('/api/v1/auth/windows'));
  logout = () => this.remember(this.api.post('/api/v1/auth/logout'));
  testIdentity = (userId: string, reason: string) =>
    this.remember(this.api.post('/api/v1/auth/test-identity', { body: { userId, reason } }));
  endTestIdentity = () => this.remember(this.api.post('/api/v1/auth/test-identity/end'));
  async me() {
    const me = await this.api.get('/api/v1/me');
    this.api.token(me.csrfToken);
    return me;
  }
  models = () => this.api.reference('/api/v1/models');
  webSearchStatus = () => this.api.reference('/api/v1/tools/web-search');
  context = (body: ContextPreviewRequest, signal: AbortSignal) =>
    this.api.post('/api/v1/context', { body, signal });
  conversations = (search = '', offset = 0, view = 'active', label = '') =>
    this.api.get('/api/v1/conversations', { query: { search, offset, view, label } });
  conversation = (id: string) => this.api.get('/api/v1/conversations/{id}', { path: { id } });
  createConversation = () => this.api.post('/api/v1/conversations', { body: {} });
  rename = (id: string, title: string) =>
    this.api.patch('/api/v1/conversations/{id}', { path: { id }, body: { title } });
  delete = (id: string) => this.api.delete('/api/v1/conversations/{id}', { path: { id } });
  branch = (id: string, leafId: string) =>
    this.api.patch('/api/v1/conversations/{id}/branch', { path: { id }, body: { leafId } });
  createRun = (body: CreateRunRequest, key: string) =>
    this.api.post('/api/v1/runs', { body, headers: { 'Idempotency-Key': key } });
  run = (id: string, signal?: AbortSignal) =>
    this.api.get('/api/v1/runs/{id}', { path: { id }, signal });
  events = (id: string, after: number, signal: AbortSignal) =>
    this.api.open('/api/v1/runs/{id}/events', {
      path: { id },
      query: { after },
      headers: { Accept: 'text/event-stream' },
      signal,
    });
  cancel = (id: string) => this.api.post('/api/v1/runs/{id}/cancel', { path: { id } });
  preferences = (body: PreferencesDto) => this.api.put('/api/v1/preferences', { body });
}
