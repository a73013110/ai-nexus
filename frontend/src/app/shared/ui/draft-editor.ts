import { computed, signal } from '@angular/core';
import type { ConfirmDialog } from './confirm-dialog';

/** 編輯中的對象：開啟時給的脈絡（ID、版本、標題…）加上可修改的草稿。 */
export type Editing<D, C> = C & { readonly draft: D };

/**
 * 編輯對話框的草稿狀態：開啟時的基準、目前內容、是否有未儲存的變更、還原，以及關閉前的放棄確認。
 * 只管草稿；欄位驗證、送出與對話框的開關留在功能自己。
 *
 * `same` 決定「有沒有變更」：去掉前後空白後相同的文字通常視為沒改，由功能自己定義。
 * 儲存成功（或版本過期、草稿作廢）後先 `markSaved()` 再關閉：對話框退場動畫期間內容仍在，
 * 儲存列才不會在消失前閃出「有未儲存的變更」。
 */
export class DraftEditor<D, C extends object = Record<never, never>> {
  private readonly editing = signal<Editing<D, C> | null>(null);
  private readonly start = signal<D | null>(null);
  /** 編輯中的對象；開啟前為 `null`，關閉後保留到下一次開啟，退場動畫期間才不會清空。 */
  readonly current = this.editing.asReadonly();
  /** 開啟時（或上次儲存後）的草稿，供還原與「原為…」這類對照。 */
  readonly baseline = this.start.asReadonly();
  readonly dirty = computed(() => {
    const editing = this.editing(),
      start = this.start();
    return !!editing && !!start && !this.same(editing.draft, start);
  });

  constructor(private readonly same: (first: D, second: D) => boolean) {}

  /** 開始編輯 `draft`；`start` 是畫面上的起始內容（例如「結束持有」預填的結束時間），預設與基準相同。 */
  open(context: C, draft: D, start: D = draft) {
    this.start.set(draft);
    this.editing.set({ ...context, draft: start });
  }
  update<K extends keyof D>(key: K, value: D[K]) {
    this.patch({ [key]: value } as unknown as Partial<D>);
  }
  /** 一次改多個欄位（例如換持有人時同時清掉姓名）。 */
  patch(changes: Partial<D>) {
    this.editing.update((editing) =>
      editing ? { ...editing, draft: { ...editing.draft, ...changes } } : null,
    );
  }
  revert() {
    const start = this.start();
    this.editing.update((editing) => (editing && start ? { ...editing, draft: start } : editing));
  }
  /** 目前的草稿成為新的基準：已儲存，或草稿作廢、即將關閉。 */
  markSaved() {
    const editing = this.editing();
    if (editing) this.start.set(editing.draft);
  }
  /** 沒有變更時直接回 `true`；有變更時以 `ConfirmDialog.discard` 詢問。 */
  async confirmDiscard(confirmation: ConfirmDialog, message: string) {
    return !this.dirty() || confirmation.discard(message);
  }
}
