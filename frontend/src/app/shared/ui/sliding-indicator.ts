import { afterNextRender, DestroyRef, Directive, ElementRef, inject, input } from '@angular/core';

/**
 * 量測群組中被選取項目的盒子，並以 `--indicator-x/-y/-w/-h` 發布到宿主上，
 * 讓同一個晶片或底線（宿主的 `::before`）能在任意寬度的項目之間滑動。它會監看選取屬性並自行調整大小，
 * 因此不會在無關的渲染時執行。
 * `data-indicator` 標示有選取項目；放好位置後才設定 `data-indicator-ready` 開啟滑動，
 * 避免晶片從角落飛入。
 * 群組消失時（對話框關閉、面板隱藏）兩者都會被移除；下一次放置會立即完成，
 * 因為上次量測的盒子已經過時。
 * 偏移量是版面數值，所以對話框的開啟／關閉縮放不會影響它們；群組重新有盒子時，ResizeObserver
 * 會重新量測。
 */
@Directive({ selector: '[nxSlidingIndicator]' })
export class SlidingIndicator {
  /** 選取項目的選擇器；預設涵蓋切換群組、分頁與導覽連結。 */
  readonly nxSlidingIndicator = input(
    '[aria-pressed="true"], [aria-selected="true"], [aria-current="page"]',
  );

  constructor() {
    const host = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
    if (typeof ResizeObserver === 'undefined') return;
    let frame = 0,
      ready = 0;
    const place = () => {
      frame = 0;
      const active = host.querySelector<HTMLElement>(this.nxSlidingIndicator());
      if (!active?.offsetWidth) {
        cancelAnimationFrame(ready);
        ready = 0;
        host.removeAttribute('data-indicator');
        host.removeAttribute('data-indicator-ready');
        return;
      }
      host.style.setProperty('--indicator-x', `${active.offsetLeft}px`);
      host.style.setProperty('--indicator-y', `${active.offsetTop}px`);
      host.style.setProperty('--indicator-w', `${active.offsetWidth}px`);
      host.style.setProperty('--indicator-h', `${active.offsetHeight}px`);
      host.setAttribute('data-indicator', '');
      // 滑動在第一次放置完成繪製之後，才從下一幀開始。
      if (!host.hasAttribute('data-indicator-ready'))
        ready ||= requestAnimationFrame(() => {
          ready = 0;
          host.setAttribute('data-indicator-ready', '');
        });
    };
    const schedule = () => (frame ||= requestAnimationFrame(place));
    const resize = new ResizeObserver(schedule);
    const mutation = new MutationObserver((records) => {
      for (const record of records)
        if (record.type === 'childList')
          record.addedNodes.forEach((node) => node instanceof Element && resize.observe(node));
      schedule();
    });
    afterNextRender(() => {
      resize.observe(host);
      for (const child of host.children) resize.observe(child);
      mutation.observe(host, {
        subtree: true,
        childList: true,
        attributes: true,
        attributeFilter: ['aria-pressed', 'aria-selected', 'aria-current', 'class'],
      });
      place();
    });
    inject(DestroyRef).onDestroy(() => {
      cancelAnimationFrame(frame);
      cancelAnimationFrame(ready);
      resize.disconnect();
      mutation.disconnect();
    });
  }
}
