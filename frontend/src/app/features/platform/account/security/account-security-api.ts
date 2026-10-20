import { Injectable, inject } from '@angular/core';
import { ApiClient } from '@core/api/api-client';
import type { LocalLoginRequest, ReauthenticationRequest } from '@core/api/schema';

/** 設定 → 帳號與權限的自助維護：通知信箱、本地登入與步升驗證。 */
@Injectable({ providedIn: 'root' })
export class AccountSecurityApi {
  private readonly api = inject(ApiClient);
  get = (signal?: AbortSignal) => this.api.get('/api/v1/settings/account', { signal });
  sendCode = () =>
    this.api.post('/api/v1/settings/account/verification-codes', {
      body: { purpose: 'reauthentication' },
    });
  code = (id: string, signal?: AbortSignal) =>
    this.api.get('/api/v1/settings/account/verification-codes/{id}', { path: { id }, signal });
  reauthenticate = (body: ReauthenticationRequest) =>
    this.api.post('/api/v1/settings/account/reauthentications', { body });
  requestEmail = (email: string) =>
    this.api.post('/api/v1/settings/account/email-changes', { body: { email } });
  confirmEmail = (id: string, code: string) =>
    this.api.post('/api/v1/settings/account/email-changes/{id}/confirmation', {
      path: { id },
      body: { code },
    });
  setLocalLogin = (body: LocalLoginRequest) =>
    this.api.put('/api/v1/settings/account/local-login', { body });
  disableLocalLogin = () => this.api.delete('/api/v1/settings/account/local-login');
}
