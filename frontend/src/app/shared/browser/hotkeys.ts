import { DestroyRef, inject } from '@angular/core';

/**
 * 把按鍵正規化為組合鍵，例如 `mod+shift+l`，其中 `mod` 是 Ctrl 或 ⌘。字母取自實體按鍵，
 * 因此 macOS 的 Option 組合仍會被讀成其字母。
 */
export function hotkey(event: KeyboardEvent) {
  // Chrome 的自動填入會派送沒有 `code` 與 `key` 的裸 `keydown` 事件；它不是組合鍵。
  const { code = '', key: name = '' } = event as Partial<KeyboardEvent>;
  const key = code.startsWith('Key') ? code.slice(3) : name;
  if (!key) return '';
  return [
    (event.ctrlKey || event.metaKey) && 'mod',
    event.altKey && 'alt',
    event.shiftKey && 'shift',
    key.toLowerCase(),
  ]
    .filter(Boolean)
    .join('+');
}

/**
 * 在呼叫元件的生命週期內綁定文件快捷鍵。符合的組合鍵一律會取消瀏覽器預設行為；
 * 按住的鍵、IME 組字與 `enabled` 為 false 時則會略過處理器。
 */
export function bindHotkeys(
  keys: Record<string, (event: KeyboardEvent) => void>,
  enabled: () => boolean = () => true,
) {
  const listener = (event: KeyboardEvent) => {
    const handler = keys[hotkey(event)];
    if (!handler || event.isComposing) return;
    event.preventDefault();
    if (!event.repeat && enabled()) handler(event);
  };
  document.addEventListener('keydown', listener);
  inject(DestroyRef).onDestroy(() => document.removeEventListener('keydown', listener));
}
