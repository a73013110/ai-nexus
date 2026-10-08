import { CompactDialog } from '../../shared/ui/compact-dialog';
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { Icon } from '../../shared/ui/icon';
import { isSubmitKey } from '../../shared/browser/submit-key';

@Component({
  selector: 'nx-focus-composer',
  imports: [CompactDialog, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<dialog
    nxCompactDialog
    #dialog
    class="platform-dialog focus-composer-dialog"
    aria-labelledby="focus-composer-title"
  >
    <header class="dialog-heading">
      <div>
        <span class="panel-eyebrow">專注輸入</span>
        <h2 id="focus-composer-title">讓長篇提問也容易整理</h2>
      </div>
      <button type="button" class="icon-button" aria-label="收合輸入區" (click)="dialog.close()">
        <nx-icon name="minimize" />
      </button>
    </header>
    <textarea
      #editor
      autofocus
      aria-label="放大的訊息輸入"
      [value]="text()"
      [readOnly]="readonly()"
      [maxLength]="maxLength()"
      placeholder="輸入訊息，或整理長篇提問…"
      (input)="textChange.emit(editor.value)"
      (keydown)="keydown($event)"
      (compositionstart)="composing.set(true)"
      (compositionend)="composing.set(false)"
    ></textarea>
    <footer class="focus-composer-footer">
      <span
        >草稿同步保留 · {{ text().length.toLocaleString() }} /
        {{ maxLength().toLocaleString() }} 字元</span
      >
      <button type="button" class="secondary-button" (click)="dialog.close()">
        繼續在對話中編輯
      </button>
      <button
        type="button"
        class="primary-button"
        [disabled]="!canSend() || !text().trim()"
        (click)="submit()"
      >
        <nx-icon name="arrow" />送出訊息
      </button>
    </footer>
  </dialog>`,
})
export class FocusComposer {
  readonly text = input('');
  readonly readonly = input(false);
  readonly canSend = input(false);
  readonly enterToSend = input(true);
  readonly maxLength = input(24000);
  readonly textChange = output<string>();
  readonly sent = output<void>();
  readonly composing = signal(false);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  open() {
    this.composing.set(false);
    this.dialog().nativeElement.showModal();
  }
  keydown(event: KeyboardEvent) {
    if (isSubmitKey(event, this.composing(), this.enterToSend())) {
      event.preventDefault();
      this.submit();
    }
  }
  submit() {
    if (!this.canSend() || !this.text().trim()) return;
    this.dialog().nativeElement.close();
    this.sent.emit();
  }
}
