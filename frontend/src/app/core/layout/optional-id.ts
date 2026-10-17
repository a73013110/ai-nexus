import type { UrlMatcher, UrlMatchResult } from '@angular/router';

/**
 * 以單一路由比對 `/<path>` 與 `/<path>/:id`，讓路由在 id 出現或改變時重用頁面元件，
 * 而不是銷毀後重建。
 */
export function optionalId(path: string): UrlMatcher {
  return (segments): UrlMatchResult | null => {
    if (segments[0]?.path !== path || segments.length > 2) return null;
    return { consumed: segments, posParams: segments[1] ? { id: segments[1] } : {} };
  };
}
