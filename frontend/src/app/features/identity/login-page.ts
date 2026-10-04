import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import {
  FormField,
  form,
  maxLength,
  required,
  readonly as readonlyField,
} from '@angular/forms/signals';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from '../../core/auth/auth-service';
import { ChatStore } from '../chat/chat-store';
import { FourierMark } from './fourier-mark';

@Component({
  selector: 'nx-login-page',
  imports: [FormField, FourierMark],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './login-page.html',
})
export class LoginPage {
  readonly auth = inject(AuthService);
  private readonly store = inject(ChatStore);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  readonly credentials = signal({ account: '', password: '' });
  readonly loading = signal(true);
  readonly submitting = signal(false);
  readonly loginForm = form(this.credentials, (schema) => {
    readonlyField(schema.account, { when: () => this.submitting() });
    readonlyField(schema.password, { when: () => this.submitting() });
    required(schema.account);
    maxLength(schema.account, 64);
    required(schema.password);
    maxLength(schema.password, 1024);
  });
  readonly error = signal<string | null>(null);
  readonly introReady = signal(false);
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
    return /^\/(?:(?:chat|projects|artifacts|knowledge|tasks|settings|admin|design|quality|integrations|shared)(?:\/[a-z0-9-]+)?|reader(?:\/attachment)?\/[a-z0-9-]+)(?:\?[a-z0-9=&%_-]+)?$/i.test(
      url,
    )
      ? url
      : '/chat';
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
