import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormField, form, maxLength, required } from '@angular/forms/signals';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from '../../core/auth/auth-service';
import { ChatStore } from '../chat/chat-store';

@Component({
  selector: 'nx-login-page',
  imports: [FormField],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './login-page.html',
})
export class LoginPage {
  readonly auth = inject(AuthService);
  private readonly store = inject(ChatStore);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  readonly credentials = signal({ account: '', password: '' });
  readonly loginForm = form(this.credentials, (schema) => {
    required(schema.account);
    maxLength(schema.account, 64);
    required(schema.password);
    maxLength(schema.password, 1024);
  });
  readonly loading = signal(true);
  readonly submitting = signal(false);
  readonly error = signal<string | null>(null);
  constructor() {
    void this.initialize();
  }
  private async initialize() {
    try {
      const session = await this.auth.load();
      if (session.authenticated || session.mode === 'Windows')
        await this.router.navigateByUrl(this.returnUrl());
    } catch (error) {
      this.error.set(error instanceof Error ? error.message : '登入服務暫時無法使用。');
    } finally {
      this.loading.set(false);
    }
  }
  private returnUrl() {
    const url = this.route.snapshot.queryParamMap.get('returnUrl') ?? '/chat';
    return /^\/chat(?:\/[a-f0-9-]+)?$/i.test(url) ? url : '/chat';
  }
  async submit(event: Event) {
    event.preventDefault();
    if (this.submitting() || this.loginForm().invalid() || !this.auth.session()?.configured) return;
    this.submitting.set(true);
    this.error.set(null);
    const { account, password } = this.credentials();
    try {
      await this.auth.login(account.trim(), password);
      await this.store.initialize(true);
      await this.router.navigateByUrl(this.returnUrl());
    } catch (error) {
      this.error.set(error instanceof Error ? error.message : '登入失敗，請稍後重試。');
    } finally {
      this.credentials.update((value) => ({ ...value, password: '' }));
      this.submitting.set(false);
    }
  }
}
