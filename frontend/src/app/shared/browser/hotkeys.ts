import { DestroyRef, inject } from '@angular/core';

/**
 * Normalizes a key press to a combo such as `mod+shift+l`, where `mod` is Ctrl or ⌘. Letters come
 * from the physical key, so macOS Option combinations still read as their letter.
 */
export function hotkey(event: KeyboardEvent) {
  const key = event.code.startsWith('Key') ? event.code.slice(3) : event.key;
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
 * Binds document shortcuts for the calling component's lifetime. A matched combo always cancels
 * the browser default, while held keys, IME composition and a false `enabled` skip the handler.
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
