import { Injectable, signal } from '@angular/core';

interface Draft {
  text: string;
  attachmentIds: string[];
  updatedAt: number;
}

const MAX_TEXT = 32000;
/** 每個帳號在此瀏覽器最多保留的草稿數；超過時先移除最久沒更新的。 */
const MAX_DRAFTS = 40;
const draftPrefix = (user: string) => `ai-nexus.draft.${user}.`;
/** 帳號在此瀏覽器的所有草稿鍵（含損壞的項目）。 */
const draftKeys = (user: string) =>
  Object.keys(localStorage).filter((key) => key.startsWith(draftPrefix(user)));

function decodeDraft(raw: string | null): Draft | null {
  try {
    const value: unknown = JSON.parse(raw ?? 'null');
    if (!value || typeof value !== 'object') return null;
    const draft = value as Draft;
    return typeof draft.text === 'string' &&
      draft.text.length <= MAX_TEXT &&
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
    return draftPrefix(user) + (conversation ?? 'new');
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
      if (text.length > MAX_TEXT) {
        this.available.set(false);
        return;
      }
      if (!text.trim() && !attachmentIds.length) localStorage.removeItem(key);
      else
        localStorage.setItem(
          key,
          JSON.stringify({ text, attachmentIds, updatedAt: Date.now() } satisfies Draft),
        );
      // 依帳號限制儲存空間；草稿刻意只存在於此瀏覽器中。
      const keys = draftKeys(user);
      if (keys.length > MAX_DRAFTS)
        keys
          .sort(
            (first, second) =>
              (decodeDraft(localStorage.getItem(first))?.updatedAt ?? 0) -
              (decodeDraft(localStorage.getItem(second))?.updatedAt ?? 0),
          )
          .slice(0, keys.length - MAX_DRAFTS)
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
      draftKeys(user).forEach((key) => localStorage.removeItem(key));
      this.available.set(true);
      return true;
    } catch {
      this.available.set(false);
      return false;
    }
  }
}
