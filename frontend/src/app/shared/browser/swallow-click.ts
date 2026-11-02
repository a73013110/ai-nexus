/**
 * 拖曳放開後，瀏覽器會對按下與放開處的共同祖先補發 click，不可當成使用者點了那裡。
 * 在 `pointerup` 當下呼叫：click 緊接著派發，所以只等到這一輪結束，之後的點擊不受影響。
 */
export function swallowClick() {
  const swallow = (event: Event) => {
    event.stopPropagation();
    event.preventDefault();
  };
  window.addEventListener('click', swallow, { capture: true, once: true });
  setTimeout(() => window.removeEventListener('click', swallow, { capture: true }));
}
