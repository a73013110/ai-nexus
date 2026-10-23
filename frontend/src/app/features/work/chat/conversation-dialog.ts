import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { FormField, form, maxLength, readonly, required } from '@angular/forms/signals';
import type { ConversationDto } from '@core/api/schema';
import { CompactDialog } from '@shared/ui/compact-dialog';
import { DialogHeader } from '@shared/ui/dialog-header';
import { Field } from '@shared/ui/field';
import { PendingButton } from '@shared/ui/pending-button';
import { ChatConversations } from './state/chat-conversations';

/** 從歷史列表或頂部列選單重新命名或刪除對話前的確認。 */
@Component({
  selector: 'nx-conversation-dialog',
  imports: [CompactDialog, DialogHeader, Field, FormField, PendingButton],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<dialog
    nxCompactDialog
    size="sm"
    #dialog
    class="platform-dialog"
    [busy]="busy()"
    (close)="mode.set(null)"
  >
    <form class="dialog-form" (submit)="confirm($event)">
      <nx-dialog-header [heading]="mode() === 'delete' ? '刪除這段對話？' : '重新命名對話'" />
      <div class="dialog-body">
        @if (mode() === 'rename') {
          <label>對話標題<input nxField [formField]="renameForm.title" autofocus /></label>
        } @else {
          <p>
            「{{
              target()?.title
            }}」將從歷史移除，附件原檔仍保留在檔案庫。若想之後再接續，可先取消並使用封存。
          </p>
        }
      </div>
      <footer class="dialog-foot">
        <button
          type="button"
          class="secondary-button"
          [attr.aria-disabled]="busy() || null"
          (click)="busy() || close()"
        >
          取消
        </button>
        <button
          nxPending
          type="submit"
          class="primary-button"
          [class.danger-button]="mode() === 'delete'"
          [pending]="busy()"
          [disabled]="mode() === 'rename' && renameForm.title().invalid()"
        >
          {{
            mode() === 'delete'
              ? busy()
                ? '刪除中…'
                : '刪除對話'
              : busy()
                ? '儲存中…'
                : '儲存標題'
          }}
        </button>
      </footer>
    </form>
  </dialog>`,
})
export class ConversationDialog {
  private readonly conversations = inject(ChatConversations);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  readonly mode = signal<'rename' | 'delete' | null>(null);
  readonly target = signal<ConversationDto | null>(null);
  readonly busy = signal(false);
  readonly renameModel = signal({ title: '' });
  readonly renameForm = form(this.renameModel, (schema) => {
    required(schema.title);
    maxLength(schema.title, 120);
    // 儲存中欄位唯讀（不停用，焦點留在原處）；失敗後解除，可直接修改再送出。
    readonly(schema.title, () => this.busy());
  });

  open(mode: 'rename' | 'delete', target: ConversationDto) {
    this.target.set(target);
    this.mode.set(mode);
    this.renameModel.set({ title: target.title });
    this.dialog().nativeElement.showModal();
  }
  close() {
    this.dialog().nativeElement.close();
  }
  async confirm(event: Event) {
    event.preventDefault();
    const target = this.target(),
      rename = this.mode() === 'rename';
    if (!target || this.busy()) return;
    if (rename && (!this.renameModel().title.trim() || this.renameForm.title().invalid())) return;
    this.busy.set(true);
    const success = rename
      ? await this.conversations.rename(target.id, this.renameModel().title)
      : await this.conversations.remove(target.id);
    this.busy.set(false);
    if (success) this.close();
  }
}
