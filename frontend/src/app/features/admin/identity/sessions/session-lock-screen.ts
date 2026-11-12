import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  afterRenderEffect,
  computed,
  effect,
  inject,
  linkedSignal,
  signal,
  viewChild,
} from '@angular/core';
import { AuthService } from '@core/auth/auth-service';
import { SessionLock } from '@core/auth/session-lock';
import { ApiError, safeMessage } from '@core/errors/safe-errors';
import { nameInitial } from '@shared/browser/format';
import { Field } from '@shared/ui/field';
import { Icon } from '@shared/ui/icon';
import { morphSize } from '@shared/ui/morph-size';
import { PasswordField, PasswordInput } from '@shared/ui/password-field';
import { TopIslandLane } from '@shared/ui/top-island-lane';
import { passwordLabelFor } from '../login-methods';
import { LoginSubmit, type SubmitPhase } from '../login-submit';

/** 最後這幾秒倒數島自動展開按鈕：來不及去找滑鼠的人也按得到。 */
const EXPAND_AT_SECONDS = 20;

/**
 * 鎖定畫面與快到期的倒數島。用模態 dialog 而不是換路由：原本的頁面、草稿與進行中的串流都留在原處，解鎖後直接回到剛才的畫面。
 * 動效的階段只在播放時寫在元素上（`data-phase`），寫入當下就能讀到新的 CSS 動畫並等它結束，長度與膠囊尺寸只寫在樣式表。
 */
@Component({
  selector: 'nx-session-lock-screen',
  styleUrls: [
    './session-lock-screen.scss',
    './session-lock-island.scss',
    './session-lock-motion.scss',
  ],
  templateUrl: './session-lock-screen.html',
  imports: [Field, Icon, LoginSubmit, PasswordField, PasswordInput],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SessionLockScreen {
  protected readonly auth = inject(AuthService);
  protected readonly lock = inject(SessionLock);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  private readonly wrap = viewChild.required<ElementRef<HTMLElement>>('wrap');
  private readonly input = viewChild.required<ElementRef<HTMLInputElement>>('input');
  private readonly island = viewChild<ElementRef<HTMLElement>>('island');
  private readonly islandSize = morphSize(() => this.island()?.nativeElement);
  private readonly lane = inject(TopIslandLane);
  private syncing = false;

  protected readonly initial = nameInitial;
  protected readonly password = signal('');
  protected readonly error = signal('');
  /** 每次拒絕換一個動畫名稱，第二次輸錯才會再抖一次。 */
  protected readonly rejections = signal(0);
  protected readonly submitPhase = signal<SubmitPhase>('idle');
  protected readonly submitting = computed(() => this.submitPhase() !== 'idle');
  protected readonly name = computed(
    () => this.auth.session()?.displayName || this.auth.session()?.account || '使用者',
  );
  protected readonly passwordLabel = computed(() => passwordLabelFor(this.auth.session()?.method));
  protected readonly reasonText = computed(() => {
    const lifetime = this.auth.session()?.lifetime;
    if (!lifetime) return '';
    const reason = this.lock.reason();
    return reason === 'manual'
      ? '你鎖定了工作區。'
      : reason === 'reauthentication'
        ? `距上次驗證已滿 ${lifetime.reauthenticationHours} 小時。`
        : `閒置超過 ${lifetime.idleLockMinutes} 分鐘。`;
  });

  /** 鎖定時維持原值：倒數島要留在畫面上，`show` 量到寬度、把位置交給卡片之後才收掉。 */
  protected readonly islandOn = linkedSignal<{ warning: boolean; reason: string | null }, boolean>({
    source: () => ({ warning: this.lock.warning(), reason: this.lock.reason() }),
    computation: (source, previous) =>
      source.reason ? (previous?.value ?? false) : source.warning,
  });
  protected readonly islandEngaged = signal(false);
  protected readonly islandOpen = computed(
    () => this.islandEngaged() || (this.lock.countdown()?.seconds ?? Infinity) <= EXPAND_AT_SECONDS,
  );
  /** 已經過的比例，直接當環的 `stroke-dashoffset`（圓周以 `pathLength` 正規化為 1）。 */
  protected readonly islandElapsed = computed(() => {
    const countdown = this.lock.countdown();
    return countdown ? Math.min(1, 1 - countdown.seconds / countdown.warnFrom) : 1;
  });

  constructor() {
    afterRenderEffect(() => {
      this.lock.reason();
      void this.sync();
    });
    // 進 top layer 才蓋得過展開的助理面板；登記寬度讓助理的圓鈕讓到旁邊。離開時留在原圖層播完升走。
    afterRenderEffect(() => {
      const island = this.island()!.nativeElement;
      if (this.islandOn() !== island.matches(':popover-open'))
        island.togglePopover(this.islandOn());
    });
    effect(() => this.lane.width.set(this.islandOn() ? (this.islandSize()?.width ?? 0) : 0));
    inject(DestroyRef).onDestroy(() => this.lane.width.set(0));
  }

  protected keepUsing() {
    this.islandEngaged.set(false);
    this.lock.keepActive();
  }

  /** 開關要等動效播完，期間狀態可能又變（別的分頁解鎖後馬上又鎖定），所以追到畫面與狀態一致為止。 */
  private async sync() {
    if (this.syncing) return;
    this.syncing = true;
    try {
      const dialog = this.dialog().nativeElement;
      while (!!this.lock.reason() !== dialog.open) {
        if (dialog.open) await this.hide(dialog);
        else await this.show(dialog);
      }
    } finally {
      this.syncing = false;
    }
  }

  private async show(dialog: HTMLDialogElement) {
    const islandWidth = this.islandOn() ? this.island()!.nativeElement.offsetWidth : 0;
    dialog.showModal();
    this.islandOn.set(false);
    this.islandEngaged.set(false);
    if (islandWidth) {
      dialog.style.setProperty('--lock-pill-width', `${islandWidth}px`);
      dialog.dataset['from'] = 'island';
    }
    await this.play(dialog, 'locking');
    dialog.style.removeProperty('--lock-pill-width');
    delete dialog.dataset['from'];
    // 觸控裝置不主動聚焦：鍵盤會擋住卡片，使用者準備好才點欄位。
    if (matchMedia('(pointer: fine)').matches)
      this.input().nativeElement.focus({ preventScroll: true });
  }

  private async hide(dialog: HTMLDialogElement) {
    await this.play(dialog, 'unlocking');
    dialog.close();
    this.submitPhase.set('idle');
    this.rejections.set(0);
    this.error.set('');
    this.password.set('');
  }

  private async play(dialog: HTMLDialogElement, phase: 'locking' | 'unlocking') {
    // 背景分頁看不到動畫，也不該留到切回來才播：鎖定要一回來就是蓋住的，不能讓畫面淡出時再露一下。
    if (document.hidden) return;
    const box = this.wrap().nativeElement.getBoundingClientRect();
    dialog.style.setProperty('--lock-card-center', `${box.top + box.height / 2}px`);
    dialog.style.setProperty('--lock-card-width', `${box.width}px`);
    dialog.style.setProperty('--lock-card-height', `${box.height}px`);
    dialog.dataset['phase'] = phase;
    await Promise.allSettled(dialog.getAnimations({ subtree: true }).map((item) => item.finished));
    // 留著的話，階段的 animation 會一直蓋過輸錯密碼的抖動。
    delete dialog.dataset['phase'];
  }

  /** 全站快捷鍵掛在 document：鎖定時不能讓它們在畫面後方開面板。Esc 也不能關掉這個對話框。 */
  protected contain(event: KeyboardEvent) {
    event.stopPropagation();
    if (event.key === 'Escape') event.preventDefault();
  }

  protected async submit(event: Event) {
    event.preventDefault();
    if (this.submitting()) return;
    if (!this.password()) {
      this.error.set('請輸入密碼。');
      this.input().nativeElement.focus({ preventScroll: true });
      return;
    }
    this.error.set('');
    this.submitPhase.set('loading');
    try {
      const session = await this.lock.verify(this.password());
      this.password.set('');
      this.rejections.set(0);
      this.submitPhase.set('success');
      this.lock.finish(session);
    } catch (error) {
      // 超過硬上限：cookie 已失效，只能重新登入。
      if (error instanceof ApiError && error.status === 401) {
        window.location.assign('/login');
        return;
      }
      this.error.set(safeMessage(error));
      this.rejections.update((value) => value + 1);
      this.password.set('');
      this.submitPhase.set('idle');
      this.input().nativeElement.focus({ preventScroll: true });
    }
  }
}
