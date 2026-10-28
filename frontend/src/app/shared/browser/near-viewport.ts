import {
  DestroyRef,
  ElementRef,
  afterNextRender,
  inject,
  signal,
  type Signal,
} from '@angular/core';

/**
 * 宿主元素是否已接近可見範圍（捲動容器上下各多一個畫面高）；一旦接近就一直是 true。
 * 用在一頁上千列的清單：還沒捲到的段落先放估計高度的佔位，接近時才建立內容，DOM 只留看過的部分，
 * 版面計算也不必每次涵蓋整頁（ADR 0026）。捲動容器是最近一個會捲動的祖先（表格內捲動時是 `.ui-table-scroll`），
 * 否則是視窗。沒有 IntersectionObserver（測試環境）時直接是 true。要在元件或指令的注入環境呼叫。
 */
export function nearViewport(margin = '100%'): Signal<boolean> {
  const near = signal(typeof IntersectionObserver === 'undefined');
  if (near()) return near.asReadonly();
  const host = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
  let observer: IntersectionObserver | undefined;
  afterNextRender(() => {
    observer = new IntersectionObserver(
      (entries) => {
        if (!entries.some((entry) => entry.isIntersecting)) return;
        near.set(true);
        observer?.disconnect();
      },
      { root: scrollParent(host), rootMargin: `${margin} 0px` },
    );
    observer.observe(host);
  });
  inject(DestroyRef).onDestroy(() => observer?.disconnect());
  return near.asReadonly();
}

/** 最近一個會捲動的祖先；沒有時是 null（視窗）。 */
function scrollParent(element: HTMLElement): HTMLElement | null {
  for (let parent = element.parentElement; parent; parent = parent.parentElement) {
    const { overflowY } = getComputedStyle(parent);
    if (/(auto|scroll)/.test(overflowY) && parent.scrollHeight > parent.clientHeight) return parent;
  }
  return null;
}
