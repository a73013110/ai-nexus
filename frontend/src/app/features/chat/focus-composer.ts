import { CompactDialog } from '../../shared/ui/compact-dialog';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  ElementRef,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { DialogHeader } from '../../shared/ui/dialog-header';
import { Icon } from '../../shared/ui/icon';
import { isSubmitKey } from '../../shared/browser/submit-key';

@Component({
  selector: 'nx-focus-composer',
  imports: [CompactDialog, DialogHeader, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './focus-composer.scss',
  template: `<dialog
    nxCompactDialog
    size="lg"
    #dialog
    class="platform-dialog focus-composer-dialog"
  >
    <nx-dialog-header heading="讓長篇提問也容易整理" closeLabel="收合輸入區" />
    <div class="dialog-body">
      <textarea
        #editor
        autofocus
        aria-label="放大的訊息輸入"
        [value]="text()"
        [readOnly]="readonly()"
        placeholder="輸入訊息，或整理長篇提問…"
        (input)="textChange.emit(editor.value)"
        (keydown)="keydown($event)"
        (compositionstart)="composing.set(true)"
        (compositionend)="composing.set(false)"
      ></textarea>
    </div>
    <footer class="dialog-foot">
      <p class="dialog-note">
        草稿同步保留 ·
        <span class="focus-composer-count" [class.is-over]="over()"
          >{{ text().length.toLocaleString() }} / {{ limit().toLocaleString() }} 字元</span
        >
      </p>
      <button type="button" class="secondary-button" (click)="dialog.close()">
        繼續在對話中編輯
      </button>
      <button type="button" class="primary-button" [disabled]="!canSend()" (click)="submit()">
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
  /** Longer text stays editable, like the main composer, and only blocks sending. */
  readonly limit = input(24000);
  readonly over = computed(() => this.text().length > this.limit());
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
    if (!this.canSend()) return;
    this.dialog().nativeElement.close();
    this.sent.emit();
  }
}
