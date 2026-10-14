import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { FormField, form, maxLength, required } from '@angular/forms/signals';
import type { ConversationDto } from '../../core/api/schema';
import { CompactDialog } from '../../shared/ui/compact-dialog';
import { DialogHeader } from '../../shared/ui/dialog-header';
import { Field } from '../../shared/ui/field';
import { ChatConversations } from './chat-conversations';

/** Confirms renaming or deleting a conversation from the history list or the topbar menu. */
@Component({
  selector: 'nx-conversation-dialog',
  imports: [CompactDialog, DialogHeader, Field, FormField],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<dialog
    nxCompactDialog
    size="sm"
    #dialog
    class="platform-dialog"
    (cancel)="busy() ? $event.preventDefault() : null"
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
        <button type="button" class="secondary-button" (click)="close()" [disabled]="busy()">
          取消
        </button>
        <button
          type="submit"
          class="primary-button"
          [class.danger-button]="mode() === 'delete'"
          [disabled]="busy() || (mode() === 'rename' && renameForm.title().invalid())"
        >
          {{ busy() ? '處理中…' : mode() === 'delete' ? '刪除對話' : '儲存標題' }}
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
