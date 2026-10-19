import type { Route, Routes } from '@angular/router';

/** 沒有權限或不存在的網址：各區與各模組路由的最後一條。 */
export const unavailableRoute: Route = {
  path: '**',
  loadComponent: () => import('./unavailable-page').then((module) => module.UnavailablePage),
};

/**
 * 區路由中的一個模組，模組的路由最後補上 {@link unavailableRoute}。陷阱：模組根網址（例如
 * `/admin/audit`、`/admin/identity`）沒有剩餘路徑段時，即使 lazy 子路由全被 `featureGuard` 擋下，
 * 路由器仍視為比對成功、不會退回區的 `**`，畫面就整片空白；模組內自己的 `**` 才能說明缺少的權限。
 */
export function moduleRoute(path: string, load: () => Promise<Routes>): Route {
  return { path, loadChildren: () => load().then((routes) => [...routes, unavailableRoute]) };
}
