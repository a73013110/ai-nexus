import { Notice } from '../../shared/ui/notice';
import { safeMessage } from '../../core/api/safe-errors';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
  ViewEncapsulation,
} from '@angular/core';
import {
  FormField,
  form,
  maxLength,
  required,
  readonly as readonlyField,
} from '@angular/forms/signals';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from '../../core/auth/auth-service';
import { WORKSPACE_HOME } from '../../core/workspace-home';
import { LoginIntro } from './login-intro';

@Component({
  selector: 'nx-login-page',
  // Page-only rules load with this lazy route instead of the initial stylesheet.
  encapsulation: ViewEncapsulation.None,
  styleUrl: '../../../login.scss',
  imports: [Notice, FormField, LoginIntro],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './login-page.html',
})
export class LoginPage {
  readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  readonly credentials = signal({ account: '', password: '' });
  readonly loading = signal(true);
  readonly submitting = signal(false);
  readonly method = signal('ad');
  readonly methods = computed(
    () =>
      this.auth.session()?.methods ??
      (this.auth.session()?.mode === 'Windows' ? ['windows'] : ['ad']),
  );
  readonly loginForm = form(this.credentials, (schema) => {
    readonlyField(schema.account, { when: () => this.submitting() });
    readonlyField(schema.password, { when: () => this.submitting() });
    required(schema.account);
    maxLength(schema.account, 64);
    required(schema.password);
    maxLength(schema.password, 1024);
  });
  readonly error = signal<string | null>(null);
  constructor() {
    void this.initialize();
  }
  async initialize() {
    this.loading.set(true);
    this.error.set(null);
    try {
      const session = await this.auth.load();
      const requested = this.route.snapshot.queryParamMap.get('method');
      const switching = requested !== null && this.methods().includes(requested);
      if (switching) this.method.set(requested);
      if (!this.methods().includes(this.method())) this.method.set(this.methods()[0] ?? 'ad');
      if (session.authenticated && !switching) await this.router.navigateByUrl(this.returnUrl());
    } catch (error) {
      this.error.set(safeMessage(error));
    } finally {
      this.loading.set(false);
    }
  }
  private returnUrl() {
    const url = this.route.snapshot.queryParamMap.get('returnUrl') ?? WORKSPACE_HOME;
    return /^\/(?:(?:dashboard|repositories|chat|projects|artifacts|knowledge|tasks|settings|admin|design|quality|integrations|shared)(?:\/[a-z0-9-]+)?|reader(?:\/attachment|\/share\/[a-z0-9-]+)?\/[a-z0-9-]+)(?:\?[a-z0-9=&%_-]+)?$/i.test(
      url,
    )
      ? url
      : WORKSPACE_HOME;
  }
  async windowsLogin() {
    if (this.submitting()) return;
    this.submitting.set(true);
    this.error.set(null);
    try {
      await this.auth.windowsLogin();
      await this.router.navigateByUrl(this.returnUrl());
    } catch (error) {
      this.error.set(safeMessage(error));
    } finally {
      this.submitting.set(false);
    }
  }
  async submit(event: Event) {
    event.preventDefault();
    if (this.submitting() || this.loginForm().invalid() || !this.auth.session()?.configured) return;
    this.submitting.set(true);
    this.error.set(null);
    const { account, password } = this.credentials();
    try {
      await this.auth.login(account.trim(), password, this.method());
      await this.router.navigateByUrl(this.returnUrl());
    } catch (error) {
      this.error.set(safeMessage(error));
    } finally {
      this.credentials.update((value) => ({ ...value, password: '' }));
      this.submitting.set(false);
    }
  }
  selectMethod(method: string) {
    this.method.set(method);
    this.credentials.update((value) => ({ ...value, password: '' }));
    this.error.set(null);
  }
}
