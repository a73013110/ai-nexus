import { Notice } from '../../shared/ui/notice';
import { ApiError, safeMessage } from '../../core/errors/safe-errors';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  ElementRef,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import {
  FormField,
  form,
  maxLength,
  required,
  readonly as readonlyField,
} from '@angular/forms/signals';
import { ActivatedRoute, Router } from '@angular/router';
import { EyeOff } from 'lucide';
import { AuthService } from '../../core/auth/auth-service';
import { WORKSPACE_HOME } from '../../core/layout/workspace-home';
import { ThemeService } from '../../core/preferences/theme-service';
import { EXTRA_ICONS, Icon } from '../../shared/ui/icon';
import { LoginIntro } from './login-intro';
import { isLoginMethod, LOGIN_METHODS, type LoginMethod, offeredMethods } from './login-methods';
import { LoginSubmit, type SubmitPhase } from './login-submit';

/** Long enough for the button to dock into its check before the page cross-fades away. */
const SUCCESS_HOLD_MS = 420;

@Component({
  selector: 'nx-login-page',
  styleUrl: './login-page.scss',
  imports: [Notice, FormField, LoginIntro, LoginSubmit, Icon],
  providers: [{ provide: EXTRA_ICONS, useValue: { 'eye-off': EyeOff } }],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './login-page.html',
})
export class LoginPage {
  readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly themes = inject(ThemeService);
  private readonly account = viewChild<ElementRef<HTMLInputElement>>('account');
  readonly catalog = LOGIN_METHODS;
  readonly credentials = signal({ account: '', password: '' });
  readonly loading = signal(true);
  readonly phase = signal<SubmitPhase>('idle');
  readonly submitting = computed(() => this.phase() !== 'idle');
  readonly method = signal<LoginMethod>('ad');
  readonly methods = computed(() => offeredMethods(this.auth.session()));
  readonly spec = computed(() => LOGIN_METHODS[this.method()]);
  readonly passwordVisible = signal(false);
  readonly capsLock = signal(false);
  /** The Windows hint explains a failed challenge, so it waits for the first attempt. */
  readonly windowsFailed = signal(false);
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
    this.windowsFailed.set(false);
    try {
      const session = await this.auth.load();
      const requested = this.route.snapshot.queryParamMap.get('method');
      const switching = isLoginMethod(requested) && this.methods().includes(requested);
      if (switching) this.method.set(requested);
      if (!this.methods().includes(this.method())) this.method.set(this.methods()[0]);
      if (session.authenticated && !switching) {
        await this.router.navigateByUrl(this.returnUrl());
        return;
      }
    } catch (error) {
      this.error.set(safeMessage(error));
    } finally {
      this.loading.set(false);
    }
    // Touch keyboards would cover the form, so only pointer devices start in the account field.
    if (this.spec().credentials && matchMedia('(pointer: fine)').matches)
      this.account()?.nativeElement.focus({ preventScroll: true });
  }
  /**
   * Router navigation never leaves the app, so any app path is a safe destination; only the login
   * page itself is excluded to avoid a loop. Unknown paths fall back through the wildcard route.
   */
  private returnUrl() {
    const url = this.route.snapshot.queryParamMap.get('returnUrl');
    return url?.startsWith('/') && !url.startsWith('//') && !/^\/login(?:[/?#]|$)/.test(url)
      ? url
      : WORKSPACE_HOME;
  }
  async windowsLogin() {
    if (this.submitting()) return;
    await this.attempt(
      () => this.auth.windowsLogin(),
      (error) => {
        // A refused challenge is the expected "no Windows identity" case; the hint explains it.
        const refused = error instanceof ApiError && error.status === 401;
        this.windowsFailed.set(refused);
        return !refused;
      },
    );
  }
  async submit(event: Event) {
    event.preventDefault();
    if (this.submitting() || this.loginForm().invalid() || !this.auth.session()?.configured) return;
    const { account, password } = this.credentials();
    this.passwordVisible.set(false);
    await this.attempt(() => this.auth.login(account.trim(), password, this.method()));
    this.credentials.update((value) => ({ ...value, password: '' }));
  }
  /** `report` decides whether a failure also shows as an error; by default it does. */
  private async attempt(
    signIn: () => Promise<void>,
    report: (error: unknown) => boolean = () => true,
  ) {
    this.phase.set('loading');
    this.error.set(null);
    try {
      await signIn();
      this.phase.set('success');
      if (!this.themes.reducedMotion())
        await new Promise((resolve) => setTimeout(resolve, SUCCESS_HOLD_MS));
      await this.router.navigateByUrl(this.returnUrl());
    } catch (error) {
      if (report(error)) this.error.set(safeMessage(error));
    } finally {
      this.phase.set('idle');
    }
  }
  selectMethod(method: LoginMethod) {
    this.method.set(method);
    this.credentials.update((value) => ({ ...value, password: '' }));
    this.passwordVisible.set(false);
    this.windowsFailed.set(false);
    this.error.set(null);
  }
  /** CapsLock has no event of its own; every key in the password field reports its state. */
  readCapsLock(event: KeyboardEvent) {
    this.capsLock.set(event.getModifierState?.('CapsLock') ?? false);
  }
}
