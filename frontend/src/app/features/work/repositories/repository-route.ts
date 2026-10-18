import type { UrlMatcher, UrlSegment } from '@angular/router';

/**
 * `/repositories`、`/repositories/:owner/:name`，以及其檢視 `/:view` 與 `/:view/:item`，共用一條
 * 路由，因此在儲存庫的程式碼、歷史與審查之間移動時，頁面與其讀取會保留。
 */
export const repositoryRoute: UrlMatcher = (segments) => {
  if (segments[0]?.path !== 'repositories' || segments.length === 2 || segments.length > 5)
    return null;
  const [, owner, name, view, item] = segments;
  const posParams: Record<string, UrlSegment> = {};
  if (owner && name) Object.assign(posParams, { owner, name });
  if (view) posParams['view'] = view;
  if (item) posParams['item'] = item;
  return { consumed: segments, posParams };
};
