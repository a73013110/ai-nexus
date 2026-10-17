/** 精簡與聚焦的輸入框共用同一套 IME 安全的送出規則。 */
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
