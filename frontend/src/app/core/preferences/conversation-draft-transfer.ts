import { Injectable, inject } from '@angular/core';
import type { AttachmentDto } from '../api/schema';
import { AuthService } from '../auth/auth-service';

/** 功能為新對話準備的提示與附件。 */
export interface TransferredDraft {
  text: string;
  attachments: AttachmentDto[];
}

/** 一次性、綁定帳號的交接；私人文字不放進 URL 與歷史狀態。 */
@Injectable({ providedIn: 'root' })
export class ConversationDraftTransfer {
  private readonly auth = inject(AuthService);
  private pending: (TransferredDraft & { id: string; current: () => boolean }) | null = null;
  put(id: string, text: string, attachments: AttachmentDto[] = []) {
    this.pending = { id, text, attachments, current: this.auth.guard() };
  }
  take(id: string | null): TransferredDraft | null {
    const value = this.pending;
    if (!value || !value.current()) {
      this.pending = null;
      return null;
    }
    if (value.id !== id) return null;
    this.pending = null;
    return { text: value.text, attachments: value.attachments };
  }
}
