import { Notice } from '@shared/ui/notice';
import { ApiError, safeMessage } from '@core/errors/safe-errors';
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
import { AuthService } from '@core/auth/auth-service';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { ViewMorph } from '@core/layout/view-morph';
import { WORKSPACE_HOME } from '@core/layout/workspace-home';
import { ThemeService } from '@core/preferences/theme-service';
import { EXTRA_ICONS, Icon } from '@shared/ui/icon';
import { LoginIdentity } from './login-identity';
import { LoginIntro, type LoginStage } from './intro/login-intro';
import { isLoginMethod, LOGIN_METHODS, type LoginMethod, offeredMethods } from './login-methods';
import { LoginSubmit, type SubmitPhase } from './login-submit';

/** 按鈕需要夠長的時間停靠到勾號上，之後頁面才會淡出。 */
const SUCCESS_HOLD_MS = 420;
/** 帳號載入時問候語保持不變，讓工作區開啟時側邊欄已填好。 */
const WELCOME_HOLD_MS = 900;
/** 涵蓋第一個工作區頁面的交錯進場（motion.scss，`data-entrance`）。 */
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
  /** 登入 → （帳號有多個時）身分 → 歡迎畫面 → 工作區。 */
  readonly stage = signal<LoginStage>('credentials');
  readonly identities = computed(() => this.auth.session()?.identities ?? []);
  /** 只有帳號實際選過身分時，才會在歡迎畫面上顯示名稱。 */
  readonly chosenIdentity = computed(() =>
    this.identities().length > 1
      ? (this.identities().find((item) => item.id === this.auth.session()?.identity)?.name ?? null)
      : null,
  );
  readonly choosing = signal<string | null>(null);
  /** 每次拒絕都會觸發一次搖動，所以第二次輸錯密碼也會再搖一次。 */
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
  /** Windows 提示說明的是失敗的挑戰，所以要等第一次嘗試之後才出現。 */
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
    // 觸控鍵盤會遮住表單，所以只有指標裝置才會在帳號欄位開始輸入。
    if (this.spec().credentials && matchMedia('(pointer: fine)').matches)
      this.account()?.nativeElement.focus({ preventScroll: true });
  }
  /**
   * 路由導覽不會離開應用，所以任何應用內路徑都可以當成目的地；只有登入頁本身會排除，避免迴圈。
   * 未知路徑會經由萬用路由回退。
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
        // 被拒絕的挑戰是預期中的「沒有 Windows 身分」情況，提示會解釋它。
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
  /** `report` 決定失敗是否也顯示為錯誤；預設會顯示。 */
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
  /** 離開身分步驟去使用另一個帳號；登出時仍停留在這個頁面。 */
  async useAnotherAccount() {
    try {
      await this.auth.logout();
    } catch (error) {
      this.error.set(safeMessage(error));
      return;
    }
    await this.morph.run(() => this.stage.set('credentials'));
    // 登出會清掉表單讀取登入方式所依賴的工作階段。
    await this.initialize();
  }
  /**
   * 帳號載入時，品牌區會放大成問候語；工作區則從品牌字樣開啟：
   * 頁面從它們後面展開時，品牌字樣與頭像會飛進側邊欄。
   */
  private async welcome() {
    // 沒有動效時，勾選的按鈕會停留到工作區取代此頁面為止。
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
  /** 減少動態效果時會跳過所有停留。 */
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
  /** CapsLock 沒有自己的事件，所以密碼欄位中的每個按鍵都會回報其狀態。 */
  readCapsLock(event: KeyboardEvent) {
    this.capsLock.set(event.getModifierState?.('CapsLock') ?? false);
  }
}
