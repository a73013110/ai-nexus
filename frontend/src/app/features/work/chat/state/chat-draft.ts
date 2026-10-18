import { inject, Injectable, signal } from '@angular/core';
import type { MessageDto } from '@core/api/schema';
import { DraftRepository } from '@core/preferences/draft-repository';
import { UserSettingsService } from '@core/preferences/user-settings';
import { DraftAttachments } from '@features/work/files/draft-attachments';

/** 輸入框的文字、正在編輯的提問，以及每個對話的本地草稿。 */
@Injectable({ providedIn: 'root' })
export class ChatDraft {
  readonly repository = inject(DraftRepository);
  private readonly attachments = inject(DraftAttachments);
  private readonly settings = inject(UserSettingsService);
  readonly content = signal({ text: '' });
  readonly editing = signal<MessageDto | null>(null);
  private restoring = false;

  /** 編輯永遠不會存成草稿；它們會從既有的提問開一個分支。 */
  save(user: string, conversation: string | null) {
    if (this.editing() || !this.settings.value().saveLocalDrafts || this.restoring) return;
    this.repository.save(
      user,
      conversation,
      this.content().text,
      this.attachments.files().map((file) => file.id),
    );
  }

  async restore(user: string, conversation: string | null) {
    if (!this.settings.value().saveLocalDrafts) return;
    const saved = this.repository.load(user, conversation);
    this.restoring = true;
    if (saved) {
      this.content.set({ text: saved.text });
      await this.attachments.restore(saved.attachmentIds);
    }
    this.restoring = false;
  }

  clear() {
    this.content.set({ text: '' });
    this.editing.set(null);
  }

  reset() {
    this.restoring = false;
    this.clear();
  }
}
