import { Injectable, signal } from '@angular/core';

interface Draft {
  text: string;
  attachmentIds: string[];
  updatedAt: number;
}

function decodeDraft(raw: string | null): Draft | null {
  try {
    const value: unknown = JSON.parse(raw ?? 'null');
    if (!value || typeof value !== 'object') return null;
    const draft = value as Draft;
    return typeof draft.text === 'string' &&
      draft.text.length <= 32000 &&
      Array.isArray(draft.attachmentIds) &&
      draft.attachmentIds.length <= 8 &&
      draft.attachmentIds.every((id) => typeof id === 'string' && id.length <= 80) &&
      typeof draft.updatedAt === 'number' &&
      Number.isFinite(draft.updatedAt)
      ? draft
      : null;
  } catch {
    return null;
  }
}

@Injectable({ providedIn: 'root' })
export class DraftRepository {
  readonly available = signal(true);
  private key(user: string, conversation: string | null) {
    return `nexus.draft.${user}.${conversation ?? 'new'}`;
  }
  load(user: string, conversation: string | null): Draft | null {
    try {
      const draft = decodeDraft(localStorage.getItem(this.key(user, conversation)));
      this.available.set(true);
      return draft;
    } catch {
      this.available.set(false);
      return null;
    }
  }
  save(user: string, conversation: string | null, text: string, attachmentIds: string[]) {
    try {
      const key = this.key(user, conversation);
      if (text.length > 32000) {
        this.available.set(false);
        return;
      }
      if (!text.trim() && !attachmentIds.length) localStorage.removeItem(key);
      else
        localStorage.setItem(
          key,
          JSON.stringify({ text, attachmentIds, updatedAt: Date.now() } satisfies Draft),
        );
      // Keep storage bounded per account; drafts are intentionally local to this browser.
      const prefix = `nexus.draft.${user}.`;
      const keys = Object.keys(localStorage).filter((key) => key.startsWith(prefix));
      if (keys.length > 40)
        keys
          .sort(
            (a, b) =>
              (decodeDraft(localStorage.getItem(a))?.updatedAt ?? 0) -
              (decodeDraft(localStorage.getItem(b))?.updatedAt ?? 0),
          )
          .slice(0, keys.length - 40)
          .forEach((key) => localStorage.removeItem(key));
      this.available.set(true);
    } catch {
      this.available.set(false);
    }
  }
  clear(user: string, conversation: string | null) {
    this.save(user, conversation, '', []);
  }
  clearAccount(user: string) {
    try {
      Object.keys(localStorage).filter(key => key.startsWith(`nexus.draft.${user}.`)).forEach(key => localStorage.removeItem(key));
      this.available.set(true); return true;
    } catch { this.available.set(false); return false; }
  }
}
