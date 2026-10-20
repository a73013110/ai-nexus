import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { safeMessage } from '@core/errors/safe-errors';
import { CompactDialog } from '@shared/ui/compact-dialog';
import { DialogHeader } from '@shared/ui/dialog-header';
import { Field } from '@shared/ui/field';
import { Notice } from '@shared/ui/notice';
import { MailApi } from '../mail-api';

/**
 * 寄一封測試信確認設定能寄出。信照常排隊、節流與重試，所以送出後回傳的是訊息 ID，結果要在紀錄裡看；
 * 伺服器每人每分鐘限 3 封。
 */
@Component({
  selector: 'nx-mail-test-dialog',
  imports: [CompactDialog, DialogHeader, Field, Notice],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<dialog
    nxCompactDialog
    size="sm"
    #dialog
    class="platform-dialog"
    aria-label="寄送測試信"
    (cancel)="cancel($event)"
  >
    <form class="dialog-form" (submit)="send($event)">
      <nx-dialog-header heading="寄送測試信" closeLabel="關閉測試信" />
      <div class="dialog-body">
        <p class="form-note">
          寄件地址：{{ from() || '未設定' }}。測試信會照常排隊寄出，結果在寄送紀錄查看。
        </p>
        <label
          >收件地址<input
            nxField
            #field
            type="email"
            required
            maxlength="254"
            autocomplete="email"
            placeholder="name@company.com"
            [value]="to()"
            [readOnly]="busy()"
            (input)="to.set($any($event.target).value)"
        /></label>
        @if (error()) {
          <nx-notice tone="danger" [message]="error()" />
        }
      </div>
      <footer class="dialog-foot">
        <button type="button" class="secondary-button" [disabled]="busy()" (click)="dialog.close()">
          取消
        </button>
        <button class="primary-button" [disabled]="busy() || !to().trim()">
          {{ busy() ? '正在送出…' : '寄出測試信' }}
        </button>
      </footer>
    </form>
  </dialog>`,
})
export class MailTestDialog {
  private readonly api = inject(MailApi);
  readonly from = input('');
  /** 入列成功，帶新訊息的 ID。 */
  readonly sent = output<string>();
  readonly to = signal('');
  readonly busy = signal(false);
  readonly error = signal('');
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  private readonly field = viewChild.required<ElementRef<HTMLInputElement>>('field');

  open() {
    this.error.set('');
    this.dialog().nativeElement.showModal();
    requestAnimationFrame(() => this.field().nativeElement.focus());
  }
  cancel(event: Event) {
    if (this.busy()) event.preventDefault();
  }
  async send(event: Event) {
    event.preventDefault();
    const to = this.to().trim();
    if (!to || this.busy()) return;
    this.busy.set(true);
    this.error.set('');
    try {
      const result = await this.api.test(to);
      this.dialog().nativeElement.close();
      this.sent.emit(result.messageId);
    } catch (error) {
      this.error.set(safeMessage(error));
    } finally {
      this.busy.set(false);
    }
  }
}
