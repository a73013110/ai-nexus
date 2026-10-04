import { Injectable, inject } from '@angular/core';
import { AuthService } from '../auth/auth-service';

/** One-time, account-bound handoff. Private text is kept out of URLs and history state. */
@Injectable({ providedIn: 'root' })
export class ConversationDraftTransfer {
  private readonly auth = inject(AuthService);
  private pending: { id: string; text: string; generation: number } | null = null;
  put(id: string, text: string) {
    this.pending = { id, text, generation: this.auth.generation() };
  }
  take(id: string | null) {
    const value = this.pending;
    if (!value || value.generation !== this.auth.generation()) {
      this.pending = null;
      return null;
    }
    if (value.id !== id) return null;
    this.pending = null;
    return value.text;
  }
}
