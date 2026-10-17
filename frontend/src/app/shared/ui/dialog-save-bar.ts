import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  inject,
  input,
  output,
} from '@angular/core';

/**
 * 編輯已儲存內容的設定表單底部：備註 | 還原 | 儲存。按鈕保持位置；沒有任何變更時兩者都是靜止狀態，
 * 備註也會說明。
 * `note` 會取代它（例如寫入被拒絕時）。放在 `form` 內，其送出即為儲存。
 */
@Component({
  selector: 'nx-dialog-save-bar',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'dialog-foot' },
  styleUrl: './dialog-save-bar.scss',
  template: `<span class="dialog-note" role="status" [class.is-dirty]="dirty() && !note()">{{
      note() || (dirty() ? '有未儲存的變更' : '尚未變更')
    }}</span>
    <button
      type="button"
      class="secondary-button"
      [disabled]="!dirty() || saving()"
      (click)="undo()"
    >
      還原
    </button>
    <button type="submit" class="primary-button" [disabled]="!dirty() || saving() || disabled()">
      {{ saving() ? '儲存中…' : label() }}
    </button>`,
})
export class DialogSaveBar {
  readonly dirty = input.required<boolean>();
  /** The save button's text, e.g. 「儲存角色」. */
  readonly label = input.required<string>();
  readonly saving = input(false);
  /** 儲存被拒絕（例如測試其他身分時）；還原仍可使用。 */
  readonly disabled = input(false);
  readonly note = input('');
  readonly revert = output<void>();
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  /** 按鈕會自行停用，因此焦點會移到表單的第一個欄位，而不是頁面。 */
  undo() {
    this.revert.emit();
    this.host.nativeElement
      .closest('form')
      ?.querySelector<HTMLElement>('.dialog-body :is(input, select, textarea)')
      ?.focus();
  }
}
