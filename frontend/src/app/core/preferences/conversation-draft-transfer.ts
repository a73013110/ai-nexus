import { Injectable, inject } from '@angular/core';
import type { AttachmentDto } from '../api/schema';
import { AuthService } from '../auth/auth-service';

/** The prompt and attachments a feature prepared for a new conversation. */
export interface TransferredDraft {
  text: string;
  attachments: AttachmentDto[];
}

/** One-time, account-bound handoff. Private text is kept out of URLs and history state. */
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
