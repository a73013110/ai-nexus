import { DestroyRef, inject, type Signal, signal } from '@angular/core';

/**
 * Whether `query` matches, kept current for the caller's lifetime. Call it in an injection context;
 * react to a change with an effect on the returned signal.
 */
export function mediaQuery(query: string): Signal<boolean> {
  const list = matchMedia(query);
  const matches = signal(list.matches);
  const change = (event: MediaQueryListEvent) => matches.set(event.matches);
  list.addEventListener('change', change);
  inject(DestroyRef).onDestroy(() => list.removeEventListener('change', change));
  return matches.asReadonly();
}
