import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  input,
  signal,
  viewChild,
} from '@angular/core';
import { FormField, form, maxLength } from '@angular/forms/signals';
import type { Conversation, ConversationSettings } from '../../core/api/types';
import { Icon } from '../../shared/ui/icon';

@Component({
  selector: 'nx-conversation-settings',
  imports: [Icon, FormField],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: ` <dialog
    #dialog
    class="workspace-dialog settings-dialog"
    aria-labelledby="settings-title"
  >
    <form (submit)="submit($event)">
      <div class="dialog-heading">
        <div>
          <span class="panel-eyebrow">CONVERSATION SETTINGS</span>
          <h2 id="settings-title">讓這段對話更合用</h2>
        </div>
        <button
          type="button"
          class="icon-button"
          aria-label="關閉對話設定"
          (click)="dialog.close()"
        >
          <nx-icon name="close" />
        </button>
      </div>
      <label for="conversation-instruction">對話指令</label
      ><textarea
        id="conversation-instruction"
        [formField]="fields.instruction"
        rows="5"
        placeholder="例如：先提供摘要，再列出待辦事項；專有名詞保留英文。"
      ></textarea>
      <p class="panel-note">套用至這段對話後續的提問；已生成的回答與其他對話保留原樣。</p>
      <label for="conversation-labels">標籤</label
      ><input id="conversation-labels" [formField]="fields.labels" placeholder="工作, 企劃, 會議" />
      <p class="panel-note">以逗號分隔，最多 5 個標籤，每個最多 24 字元。</p>
      @if (error()) {
        <p class="inline-error" role="alert">{{ error() }}</p>
      }
      <div class="dialog-actions">
        <button type="button" class="secondary-button" (click)="dialog.close()">取消</button
        ><button type="submit" class="primary-button" [disabled]="busy() || fields().invalid()">
          {{ busy() ? '儲存中…' : '儲存設定' }}
        </button>
      </div>
    </form>
  </dialog>`,
})
export class ConversationSettingsDialog {
  readonly save =
    input.required<
      (conversation: Conversation, settings: ConversationSettings) => Promise<boolean>
    >();
  readonly model = signal({ instruction: '', labels: '' });
  readonly fields = form(this.model, (schema) => {
    maxLength(schema.instruction, 4000);
    maxLength(schema.labels, 150);
  });
  readonly busy = signal(false);
  readonly error = signal('');
  private target: Conversation | null = null;
  private readonly dialog = viewChild<ElementRef<HTMLDialogElement>>('dialog');
  open(conversation: Conversation) {
    this.target = conversation;
    this.error.set('');
    this.model.set({
      instruction: conversation.systemInstruction,
      labels: (conversation.labels ?? []).join(', '),
    });
    this.dialog()?.nativeElement.showModal();
  }
  async submit(event: Event) {
    event.preventDefault();
    if (this.busy() || !this.target || this.fields().invalid()) return;
    const labels = this.model()
      .labels.split(/[,，]/)
      .map((label) => label.trim())
      .filter(Boolean);
    if (labels.length > 5 || labels.some((label) => label.length > 24)) {
      this.error.set('最多 5 個標籤，每個最多 24 字元。');
      return;
    }
    this.busy.set(true);
    this.error.set('');
    try {
      if (await this.save()(this.target, { systemInstruction: this.model().instruction, labels }))
        this.dialog()?.nativeElement.close();
      else this.error.set('未能儲存設定，請確認連線後重試。');
    } finally {
      this.busy.set(false);
    }
  }
}
