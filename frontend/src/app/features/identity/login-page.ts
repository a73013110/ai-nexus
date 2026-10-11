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
import { WorkspaceSession } from '../../core/auth/workspace-session';
import { ViewMorph } from '../../core/layout/view-morph';
import { WORKSPACE_HOME } from '../../core/layout/workspace-home';
import { ThemeService } from '../../core/preferences/theme-service';
import { EXTRA_ICONS, Icon } from '../../shared/ui/icon';
import { LoginIdentity } from './login-identity';
import { LoginIntro, type LoginStage } from './login-intro';
import { isLoginMethod, LOGIN_METHODS, type LoginMethod, offeredMethods } from './login-methods';
import { LoginSubmit, type SubmitPhase } from './login-submit';

/** Long enough for the button to dock into its check before the page cross-fades away. */
const SUCCESS_HOLD_MS = 420;
/** The greeting stays while the account loads, so the workspace opens with its sidebar filled. */
const WELCOME_HOLD_MS = 900;
/** Covers the staggered arrival of the first workspace page (motion.scss, `data-entrance`). */
const ENTRANCE_MS = 2400;

@Component({
  selector: 'nx-login-page',
  styleUrl: './login-page.scss',
  imports: [Notice, FormField, LoginIntro, LoginIdentity, LoginSubmit, Icon],
  providers: [{ provide: EXTRA_ICONS, useValue: { 'eye-off': EyeOff } }],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './login-page.html',
})
export class LoginPage {
  readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly themes = inject(ThemeService);
  private readonly morph = inject(ViewMorph);
  private readonly workspace = inject(WorkspaceSession);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly account = viewChild<ElementRef<HTMLInputElement>>('account');
  /** Sign-in → (identity, when the account offers several) → welcome → the workspace. */
  readonly stage = signal<LoginStage>('credentials');
  readonly identities = computed(() => this.auth.session()?.identities ?? []);
  /** Named on the welcome screen only when the account actually chose one. */
  readonly chosenIdentity = computed(() =>
    this.identities().length > 1
      ? (this.identities().find((x) => x.id === this.auth.session()?.identity)?.name ?? null)
      : null,
  );
  readonly choosing = signal<string | null>(null);
  /** Each rejection flips the shake, so a second wrong password shakes again. */
  readonly rejections = signal(0);
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
      await this.pause(SUCCESS_HOLD_MS);
    } catch (error) {
      if (report(error)) {
        this.error.set(safeMessage(error));
        this.rejections.update((value) => value + 1);
      }
      this.phase.set('idle');
      return;
    }
    if (this.identities().length > 1) {
      await this.morph.run(() => {
        this.stage.set('identity');
        this.phase.set('idle');
      });
      this.host.nativeElement.querySelector<HTMLElement>('.identity-option')?.focus();
      return;
    }
    await this.welcome();
    this.phase.set('idle');
  }
  async chooseIdentity(identity: string) {
    if (this.choosing()) return;
    this.choosing.set(identity);
    this.error.set(null);
    try {
      if (identity !== this.auth.session()?.identity) await this.auth.selectIdentity(identity);
      await this.welcome();
    } catch (error) {
      this.error.set(safeMessage(error));
    } finally {
      this.choosing.set(null);
    }
  }
  /** Leaves the identity step for another account; the sign-out keeps this page. */
  async useAnotherAccount() {
    try {
      await this.auth.logout();
    } catch (error) {
      this.error.set(safeMessage(error));
      return;
    }
    await this.morph.run(() => this.stage.set('credentials'));
    // Sign-out clears the session the form reads its methods from.
    await this.initialize();
  }
  /**
   * The brand side widens into a greeting while the account loads, then the workspace opens from
   * the wordmark: it and the avatar fly into the sidebar as the page widens out from behind them.
   */
  private async welcome() {
    // Without motion the checked button stays until the workspace replaces the page.
    if (!this.morph.enabled) {
      await this.router.navigateByUrl(this.returnUrl());
      return;
    }
    this.morph.next('sign-in-welcome');
    await this.morph.run(() => this.stage.set('welcome'));
    await Promise.all([
      this.workspace.load().catch(() => null),
      this.pause(WELCOME_HOLD_MS),
      this.morph.whenSettled(),
    ]);
    const mark = this.host.nativeElement
      .querySelector('.login-brand .brand-symbol')
      ?.getBoundingClientRect();
    if (mark)
      this.morph.next('sign-in', { x: mark.x + mark.width / 2, y: mark.y + mark.height / 2 });
    const root = document.documentElement;
    root.dataset['entrance'] = 'sign-in';
    setTimeout(() => delete root.dataset['entrance'], ENTRANCE_MS);
    await this.router.navigateByUrl(this.returnUrl());
    this.morph.discard();
  }
  /** Reduced motion skips every hold. */
  private pause(ms: number) {
    return this.themes.reducedMotion()
      ? Promise.resolve()
      : new Promise((resolve) => setTimeout(resolve, ms));
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
