import {
  afterNextRender,
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  ElementRef,
  inject,
  Injector,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { Icon } from '@shared/ui/icon';
import { morphSize } from '@shared/ui/morph-size';
import { PendingButton } from '@shared/ui/pending-button';
import { safeMessage } from '../errors/safe-errors';
import { AuthService } from './auth-service';
import { IdentityVeil } from './identity-veil';

/** 最後一分鐘轉成警示色並展開，提醒即將自動返回。 */
const WARNING_MS = 60_000;
/** 剛換成測試身分時展開說明多久；指標停在上面或焦點在裡面時不收合。 */
const INTRO_MS = 4500;

/**
 * 管理者以其他使用者身分測試時的提示：視窗一圈外框，頂端中央垂下一個動態島。
 * 浮在內容上、不佔版面高度，進出測試時頁面不跳動。精簡時只有名字、倒數與返回；
 * 點開（或剛切換過來、剩最後一分鐘）展開成說明卡，尺寸由 `morphSize` 量測後以彈簧過渡。
 */
@Component({
  selector: 'nx-test-identity-indicator',
  imports: [Icon, PendingButton],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './test-identity-indicator.scss',
  host: { '(keydown.escape)': 'collapse($event)' },
  template: `@if (visible()) {
    <div
      class="test-frame"
      aria-hidden="true"
      animate.enter="test-frame-enter"
      animate.leave="test-frame-leave"
      [attr.data-tone]="tone()"
    ></div>
    <div
      class="notch-shell"
      role="region"
      aria-label="管理者測試身分"
      animate.enter="notch-enter"
      animate.leave="notch-leave"
      [attr.data-tone]="tone()"
    >
      <div
        class="notch"
        [attr.data-expanded]="expanded() || null"
        [style.width.px]="size()?.width"
        [style.height.px]="size()?.height"
      >
        <div #content class="notch-content">
          <div class="notch-bar">
            <button
              #toggleButton
              type="button"
              class="notch-toggle"
              aria-controls="test-identity-details"
              [attr.aria-expanded]="expanded()"
              [attr.aria-label]="'測試身分：' + name() + '，剩餘 ' + minutesLeft() + ' 分鐘'"
              (click)="expanded.set(!expanded())"
            >
              <span class="notch-badge"><nx-icon name="shield" /></span>
              <span class="notch-name">{{ name() }}</span>
              <span class="notch-clock">{{ clock() }}</span>
              @if (expanded()) {
                <span class="notch-end"><nx-icon name="chevron-up" /></span>
              }
            </button>
            @if (!expanded()) {
              <button
                nxPending
                type="button"
                class="notch-end notch-restore"
                aria-label="返回管理者"
                title="返回管理者"
                [pending]="busy()"
                (click)="restore()"
              >
                <nx-icon name="back" />
              </button>
            }
          </div>
          @if (expanded()) {
            <div id="test-identity-details" class="notch-details" animate.enter="notch-reveal">
              <p class="notch-note">
                @if (warning()) {
                  即將自動返回 {{ administrator() }}。
                } @else {
                  由 {{ administrator() }} 啟用，操作會留下稽核。
                }
              </p>
              @if (error()) {
                <p class="notch-note notch-error" role="alert">{{ error() }}</p>
              }
              <button
                nxPending
                type="button"
                class="notch-return"
                [pending]="busy()"
                (click)="restore()"
              >
                <nx-icon name="back" />{{ busy() ? '正在返回…' : '返回管理者' }}
              </button>
            </div>
          }
        </div>
      </div>
    </div>
  }`,
})
export class TestIdentityIndicator {
  private readonly auth = inject(AuthService);
  private readonly veil = inject(IdentityVeil);
  private readonly injector = inject(Injector);
  private readonly testing = computed(() => this.auth.session()?.testing ?? null);
  /** 換身分的遮罩開始淡出才進場，外框與島的進場不會被遮罩擋掉。 */
  readonly visible = computed(() => !!this.testing() && !this.veil.covering());
  readonly name = computed(() => {
    const session = this.auth.session();
    return session?.displayName || session?.account || '';
  });
  readonly administrator = computed(() => this.testing()?.administratorName ?? '');
  private readonly now = signal(Date.now());
  private readonly remaining = computed(() => {
    const test = this.testing();
    return test ? Math.max(0, new Date(test.expiresAt).getTime() - this.now()) : 0;
  });
  readonly warning = computed(() => !!this.testing() && this.remaining() <= WARNING_MS);
  readonly tone = computed(() => (this.warning() ? 'warning' : 'test'));
  readonly minutesLeft = computed(() => Math.ceil(this.remaining() / 60_000));
  /** 分鐘補成兩位數，倒數跨過 10 分鐘時島的寬度不變。 */
  readonly clock = computed(() => {
    const seconds = Math.ceil(this.remaining() / 1000);
    const pad = (value: number) => String(value).padStart(2, '0');
    return `${pad(Math.floor(seconds / 60))}:${pad(seconds % 60)}`;
  });
  /** 剛從換身分的遮罩過來時先展開，讓人看清楚現在是誰。 */
  readonly expanded = signal(this.veil.arrived);
  readonly busy = signal(false);
  readonly error = signal('');
  private readonly content = viewChild<ElementRef<HTMLElement>>('content');
  private readonly toggleButton = viewChild<ElementRef<HTMLElement>>('toggleButton');
  readonly size = morphSize(() => this.content()?.nativeElement);

  constructor() {
    effect((onCleanup) => {
      const test = this.testing();
      if (!test) return;
      untracked(() => this.now.set(Date.now()));
      const tick = setInterval(() => this.now.set(Date.now()), 1000);
      // 到期自動返回；伺服器那端也在同一時間失效。
      const expire = setTimeout(
        () => void this.restore(),
        Math.max(0, new Date(test.expiresAt).getTime() - Date.now()),
      );
      onCleanup(() => {
        clearInterval(tick);
        clearTimeout(expire);
      });
    });
    effect(() => {
      if (this.warning()) untracked(() => this.expanded.set(true));
    });
    // 說明從島真的出現才開始計時（遮罩淡出時）；使用者正在看或操作時不收合。
    let introduced = !this.veil.arrived;
    effect((onCleanup) => {
      if (!this.visible() || introduced) return;
      introduced = true;
      const timer = setTimeout(() => {
        const shell = this.content()?.nativeElement.closest('.notch-shell');
        if (!this.warning() && !shell?.matches(':hover, :focus-within')) this.expanded.set(false);
      }, INTRO_MS);
      onCleanup(() => clearTimeout(timer));
    });
  }

  collapse(event: Event) {
    if (!this.expanded()) return;
    event.stopPropagation();
    this.expanded.set(false);
    afterNextRender(() => this.toggleButton()?.nativeElement.focus(), {
      injector: this.injector,
    });
  }

  async restore() {
    if (this.busy()) return;
    this.busy.set(true);
    this.error.set('');
    try {
      await this.auth.endTestIdentity();
    } catch (error) {
      this.error.set(safeMessage(error));
      this.expanded.set(true);
      this.busy.set(false);
    }
  }
}
