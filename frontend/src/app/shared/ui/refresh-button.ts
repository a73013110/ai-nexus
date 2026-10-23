import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  HostAttributeToken,
  computed,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { Icon } from './icon';

/** 至少轉一圈（與 `--motion-spin` 相同）：讀得很快時也看得出確實重讀過。 */
const MIN_SPIN = 1000;

/**
 * 「重新讀取」圖示鈕：`<button nxRefresh aria-label="重新讀取目錄" [busy]="…" (refresh)="…">`。
 * 讀取中轉圈並以 `aria-disabled` 擋下重複送出（不用 `disabled`，焦點才不會掉到頁首）；
 * 結果在畫面上的內容區以 `.refresh-line`／`.is-refreshing` 表示，這顆鈕只負責動作本身的回饋。
 */
@Component({
  selector: 'button[nxRefresh]',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    type: 'button',
    class: 'icon-button',
    '[title]': 'label',
    '[attr.aria-disabled]': 'spinning() || null',
    '[attr.aria-busy]': 'spinning() || null',
    '(click)': 'press()',
  },
  template: `<nx-icon [name]="spinning() ? 'loading' : 'refresh'" />`,
})
export class RefreshButton {
  /** 按鈕名稱取自靜態的 `aria-label`（例：「重新讀取目錄」），模板檢查也看得到按鈕有名稱；滑鼠停留時同一句當提示。 */
  protected readonly label = inject(new HostAttributeToken('aria-label'));
  /** 呼叫端的讀取狀態；按下之後到讀完都算在內。 */
  readonly busy = input(false);
  readonly refresh = output<void>();
  private readonly held = signal(false);
  private timer: ReturnType<typeof setTimeout> | undefined;
  readonly spinning = computed(() => this.busy() || this.held());

  constructor() {
    inject(DestroyRef).onDestroy(() => clearTimeout(this.timer));
  }

  press() {
    if (this.spinning()) return;
    this.held.set(true);
    this.timer = setTimeout(() => this.held.set(false), MIN_SPIN);
    this.refresh.emit();
  }
}
