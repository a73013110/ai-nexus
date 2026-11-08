import { uniqueId } from '../browser/unique-id';
import { CompactDialog } from './compact-dialog';
import {
  afterNextRender,
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  ElementRef,
  inject,
  Injector,
  input,
  signal,
  viewChild,
} from '@angular/core';
import { DialogHeader } from './dialog-header';
import { Field } from './field';

export interface Confirmation {
  title: string;
  message: string;
  confirm: string;
  danger?: boolean;
  /**
   * 不能復原、波及範圍大的動作（例：清空整本日曆）：要照打這段文字才能確認，避免順手按下去。
   * 用對象的識別（名稱、ID 前幾碼），讓人確認自己動的是哪一個；比對區分大小寫、忽略前後空白。
   */
  typed?: string;
}
/** 以 Promise 回答的確認對話框：`ask` 等使用者選擇；再次詢問或元件銷毀時，前一個問題視為取消。 */
@Component({
  selector: 'nx-confirm-dialog',
  imports: [CompactDialog, DialogHeader, Field],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './confirm-dialog.scss',
  template: `<dialog
    [nxCompactDialog]="compact()"
    size="sm"
    #dialog
    class="platform-dialog"
    [attr.aria-label]="value()?.title"
    [attr.aria-describedby]="id + '-message'"
    (cancel)="onCancel($event)"
  >
    <nx-dialog-header [heading]="value()?.title ?? ''" closeLabel="關閉確認視窗" />
    <div class="dialog-body">
      <p class="confirmation-message" [id]="id + '-message'">{{ value()?.message }}</p>
      @if (value()?.typed; as typed) {
        <label
          >輸入 <code class="confirmation-typed">{{ typed }}</code> 以確認<input
            #typedInput
            nxField
            autocomplete="off"
            spellcheck="false"
            [value]="entered()"
            (input)="entered.set($any($event.target).value)"
        /></label>
      }
    </div>
    <footer class="dialog-foot">
      <button type="button" autofocus class="secondary-button" (click)="answer(false)">取消</button>
      <button
        type="button"
        [class]="value()?.danger ? 'danger-button' : 'primary-button'"
        [disabled]="!matches()"
        (click)="answer(true)"
      >
        {{ value()?.confirm }}
      </button>
    </footer>
  </dialog>`,
})
export class ConfirmDialog {
  readonly compact = input(true);
  readonly id = uniqueId('nx-confirm');
  readonly value = signal<Confirmation | null>(null);
  readonly entered = signal('');
  readonly matches = computed(() => {
    const typed = this.value()?.typed;
    return !typed || this.entered().trim() === typed;
  });
  readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  private readonly typedInput = viewChild<ElementRef<HTMLInputElement>>('typedInput');
  private readonly injector = inject(Injector);
  private resolve?: (answer: boolean) => void;
  constructor() {
    inject(DestroyRef).onDestroy(() => this.resolve?.(false));
  }
  ask(value: Confirmation) {
    this.resolve?.(false);
    this.value.set(value);
    this.entered.set('');
    this.dialog().nativeElement.showModal();
    // showModal 當下輸入框還沒畫出來，焦點會落在「取消」；要打字的問題等畫完再移過去。
    if (value.typed)
      afterNextRender(() => this.typedInput()?.nativeElement.focus(), { injector: this.injector });
    return new Promise<boolean>((resolve) => (this.resolve = resolve));
  }
  /** 關閉、切換或離開會捨棄未儲存的變更時，全站同一個標題與按鈕；`message` 說明捨棄什麼、之後會是什麼內容。 */
  discard(message: string) {
    return this.ask({ title: '放棄未儲存的變更？', message, confirm: '放棄變更' });
  }
  onCancel(event: Event) {
    event.preventDefault();
    this.answer(false);
  }
  answer(value: boolean) {
    if (value && !this.matches()) return;
    this.dialog().nativeElement.close();
    this.resolve?.(value);
    this.resolve = undefined;
  }
}
