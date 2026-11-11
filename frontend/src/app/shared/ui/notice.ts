import {
  ChangeDetectionStrategy,
  Component,
  booleanAttribute,
  computed,
  input,
  output,
} from '@angular/core';
import { Icon } from './icon';
import { IssueCode } from './issue-code';
import { PendingButton } from './pending-button';
import type { BadgeTone } from './count-badge';

/**
 * 頁面、對話框、輸入框與全域通知共用的精簡回饋表面。
 *
 * 讀取失敗要能重試時加 `retryable`：`<nx-notice [message]="error()" retryable [retrying]="refreshing()" (retry)="reload()" />`。
 * 重試鈕內建，文字與轉圈回饋全站一致；`retrying` 綁該讀取的 `refreshing()`（不是 `loading()`，見 docs/frontend/loading.md）。
 * 其他動作（前往某頁、改設定）仍以 `notice-actions` 投影。
 */
@Component({
  selector: 'nx-notice',
  imports: [Icon, IssueCode, PendingButton],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './notice.scss',
  host: {
    class: 'ui-notice',
    '[attr.data-tone]': 'tone()',
    '[attr.role]': 'tone() === "danger" ? "alert" : "status"',
  },
  template: `<nx-icon class="ui-notice-icon" [name]="resolvedIcon()" />
    <div class="ui-notice-content">
      @if (message()) {
        <span>{{ message() }}</span>
      }
      <ng-content />
    </div>
    @if (message()) {
      <nx-issue-code [message]="message()" />
    }
    <div class="ui-notice-actions">
      <ng-content select="[notice-actions]" />
      @if (retryable()) {
        <button nxPending type="button" [pending]="retrying()" (click)="retry.emit()">
          {{ retrying() ? '重試中…' : '重試' }}
        </button>
      }
      @if (dismissible()) {
        <button
          type="button"
          class="icon-button"
          [attr.aria-label]="dismissLabel()"
          [title]="dismissLabel()"
          (click)="dismissed.emit()"
        >
          <nx-icon name="close" />
        </button>
      }
    </div>`,
})
export class Notice {
  readonly tone = input<BadgeTone>('danger');
  readonly message = input<string | null | undefined>();
  readonly dismissible = input(false);
  readonly dismissLabel = input('關閉提示');
  readonly dismissed = output<void>();
  /** 顯示內建的「重試」鈕；按下時送出 `retry`。 */
  readonly retryable = input(false, { transform: booleanAttribute });
  /** 重試的讀取進行中：重試鈕轉圈並擋重複按下。 */
  readonly retrying = input(false);
  readonly retry = output<void>();
  readonly icon = input('');
  readonly resolvedIcon = computed(
    () => this.icon() || (this.tone() === 'success' ? 'check' : 'info'),
  );
}
