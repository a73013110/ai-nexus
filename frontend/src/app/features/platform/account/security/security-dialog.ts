import { ElementRef, effect, untracked, type Signal } from '@angular/core';
import type { AccountSecurityStore, SecurityDialog } from './account-security-store';

/**
 * 讓原生 `<dialog>` 跟著 `store.dialog()` 開關；使用者以 Esc 或關閉鈕關掉時，對話框的 `(close)` 要呼叫 `store.close()`。
 * 開關由 store 決定，步升完成後才能自動接續下一個對話框。
 */
export function bindSecurityDialog(
  store: AccountSecurityStore,
  name: SecurityDialog,
  dialog: Signal<ElementRef<HTMLDialogElement>>,
  onOpen: () => void,
) {
  effect(() => {
    const element = dialog().nativeElement;
    const open = store.dialog() === name;
    if (open && !element.open) {
      untracked(onOpen);
      element.showModal();
    } else if (!open && element.open) element.close();
  });
}
