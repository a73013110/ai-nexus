import { Injectable, inject } from '@angular/core';
import { AuthService } from '../auth/auth-service';

/** One-time, account-bound handoff. Private text is kept out of URLs and history state. */
@Injectable({ providedIn: 'root' })
export class ConversationDraftTransfer {
  private readonly auth = inject(AuthService);
  private pending: { id: string; text: string; current: () => boolean } | null = null;
  put(id: string, text: string) {
    this.pending = { id, text, current: this.auth.guard() };
  }
  take(id: string | null) {
    const value = this.pending;
    if (!value || !value.current()) {
      this.pending = null;
      return null;
    }
    if (value.id !== id) return null;
    this.pending = null;
    return value.text;
  }
}
