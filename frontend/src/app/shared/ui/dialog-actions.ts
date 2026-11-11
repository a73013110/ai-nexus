import { ChangeDetectionStrategy, Component, booleanAttribute, input, output } from '@angular/core';
import { PendingButton } from './pending-button';

/**
 * 新增、執行類對話框的底部：取消 | 主要動作。編輯已儲存內容的表單改用 `nx-dialog-save-bar`。
 *
 * 放在 `form` 內時主要動作就是送出；不是表單的對話框設 `[submit]="false"` 並接 `(confirm)`。
 * 送出中（`pending`）主要動作轉圈、文字換成 `pendingLabel`，「取消」以 `aria-disabled` 擋下且不送出 `dismiss`：
 * 不用 `disabled`，焦點才留在原處（docs/frontend/loading.md）。`disabled` 是「還不能送出」（必填未完成），送出中不套用。
 * 左側的說明以 `<p class="dialog-note">` 投影進來。
 */
@Component({
  selector: 'nx-dialog-actions',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [PendingButton],
  host: { class: 'dialog-foot' },
  template: `<ng-content />
    <button
      type="button"
      class="secondary-button"
      [attr.aria-disabled]="pending() || null"
      (click)="pending() || dismiss.emit()"
    >
      {{ cancelLabel() }}
    </button>
    <button
      nxPending
      class="primary-button"
      [attr.type]="submit() ? 'submit' : 'button'"
      [pending]="pending()"
      [disabled]="disabled() && !pending()"
      (click)="submit() || confirm.emit()"
    >
      {{ pending() ? pendingLabel() : label() }}
    </button>`,
})
export class DialogActions {
  /** 主要動作的文字，例如「建立單位」。 */
  readonly label = input.required<string>();
  /** 送出中的文字（動詞＋中…），例如「建立中…」。 */
  readonly pendingLabel = input.required<string>();
  readonly pending = input(false);
  /** 還不能送出（例如必填欄位未完成）；取消仍可使用。 */
  readonly disabled = input(false);
  readonly cancelLabel = input('取消');
  /** 主要動作是否送出所在的表單；設為 false 時改送出 `confirm`。 */
  readonly submit = input(true, { transform: booleanAttribute });
  /** 按下取消（送出中不會送出）。 */
  readonly dismiss = output<void>();
  /** `submit` 為 false 時按下主要動作。 */
  readonly confirm = output<void>();
}
