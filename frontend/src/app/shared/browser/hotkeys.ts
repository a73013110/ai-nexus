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

/** Apple 平台（macOS、iPadOS）的 `mod` 是 ⌘；其他平台是 Ctrl。只影響顯示，比對一律用 `hotkey()`。 */
export function applePlatform() {
  const nav = navigator as Navigator & { userAgentData?: { platform?: string } };
  return /mac|iphone|ipad|ipod/i.test(nav.userAgentData?.platform || nav.platform || '');
}

const MODIFIERS = {
  apple: { mod: '⌘', alt: '⌥', shift: '⇧' },
  other: { mod: 'Ctrl', alt: 'Alt', shift: 'Shift' },
} as const;
const NAMED: Record<string, string> = {
  escape: 'Esc',
  enter: 'Enter',
  arrowup: '↑',
  arrowdown: '↓',
  arrowleft: '←',
  arrowright: '→',
};

/**
 * `hotkey()` 格式的組合鍵（`mod+shift+l`）拆成顯示用的鍵名：修飾鍵依平台顯示 ⌘／⌥／⇧ 或
 * Ctrl／Alt／Shift，字母大寫，`escape` 顯示為 Esc。
 */
export function keyCaps(keys: string, apple = applePlatform()) {
  const modifiers: Record<string, string> = apple ? MODIFIERS.apple : MODIFIERS.other;
  return keys
    .split('+')
    .map((key) => modifiers[key] ?? NAMED[key] ?? key.charAt(0).toUpperCase() + key.slice(1));
}

/** 放在 `title` 等純文字處的組合鍵，例如「Ctrl+K」或「⌘K」。 */
export function keyText(keys: string, apple = applePlatform()) {
  return keyCaps(keys, apple).join(apple ? '' : '+');
}
