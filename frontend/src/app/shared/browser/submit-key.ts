/** Compact and focused composers share the same IME-safe submission rule. */
export function isSubmitKey(event: KeyboardEvent, composing: boolean, enterToSend: boolean) {
  return (
    event.key === 'Enter' &&
    !event.shiftKey &&
    !event.isComposing &&
    !composing &&
    event.keyCode !== 229 &&
    (enterToSend || event.ctrlKey || event.metaKey)
  );
}
