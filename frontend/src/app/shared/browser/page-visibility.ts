import { DestroyRef, inject } from '@angular/core';

/** 分頁切到前景或背景時呼叫 `handler`，呼叫端銷毀時自動解除；不在注入環境時要傳 `destroyRef`。 */
export function onVisibilityChange(handler: () => void, destroyRef = inject(DestroyRef)) {
  document.addEventListener('visibilitychange', handler);
  destroyRef.onDestroy(() => document.removeEventListener('visibilitychange', handler));
}
