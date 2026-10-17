import { DestroyRef, inject, type Signal, signal } from '@angular/core';

/**
 * `query` 是否符合，並在呼叫端的生命週期內保持更新。需在 injection context 中呼叫；
 * 對回傳的 signal 使用 effect 即可回應變化。
 */
export function mediaQuery(query: string): Signal<boolean> {
  const list = matchMedia(query);
  const matches = signal(list.matches);
  const change = (event: MediaQueryListEvent) => matches.set(event.matches);
  list.addEventListener('change', change);
  inject(DestroyRef).onDestroy(() => list.removeEventListener('change', change));
  return matches.asReadonly();
}
