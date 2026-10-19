import type { UrlMatcher, UrlMatchResult } from '@angular/router';

/**
 * 在模組路由檔內以單一路由比對模組網址本身與 `/:id`（相對於上層路徑），讓路由在 id 出現或改變時
 * 重用頁面元件，而不是銷毀後重建。同模組的固定子路徑（`retrieval`、`reader/:id`）要排在它之前，
 * 並列在 `reserved`：固定子路徑被 `featureGuard` 擋下時，路由器會繼續往下比對，不能讓它被當成 id
 * 開成列表頁，而要落到區的 `**` 說明缺少的權限。
 */
export function optionalId(...reserved: string[]): UrlMatcher {
  return (segments): UrlMatchResult | null =>
    segments.length > 1 || (segments[0] && reserved.includes(segments[0].path))
      ? null
      : { consumed: segments, posParams: segments[0] ? { id: segments[0] } : {} };
}
