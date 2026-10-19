import { inject } from '@angular/core';
import type { CanMatchFn, Route } from '@angular/router';
import { catalogFeature, type RoutedId } from '../api/catalog';
import { AuthService } from './auth-service';
import { WorkspaceSession } from './workspace-session';

/**
 * 依路由的 `data.feature` 決定能不能比對：頁面要有 `/me` 的授權，閱讀器只需登入（文件能不能讀由
 * 資源 ACL 決定）。用 `canMatch` 而非 `canActivate`，未授權時不會下載該頁的 chunk，並落到區路由最後的
 * `**`，由 `UnavailablePage` 說明缺少哪個權限。
 */
export const featureGuard: CanMatchFn = async (route) => {
  // await 之後就離開注入脈絡，所以先取得服務。
  const auth = inject(AuthService);
  const session = inject(WorkspaceSession);
  const feature = catalogFeature(String(route.data?.['feature'] ?? ''));
  if (!feature || feature.kind === 'action') return false;
  // 未登入交給殼層的 `authenticated`：它會帶著 returnUrl 轉到登入頁。已知登入時不重問伺服器，
  // 殼層的守衛每次導覽都會再確認。
  try {
    if (!auth.session()?.authenticated && !(await auth.load()).authenticated) return true;
  } catch {
    return true;
  }
  if (feature.kind === 'reader') return true;
  try {
    if (!(await session.load())) return true;
  } catch {
    // `/me` 讀取失敗不是權限問題；讓頁面自己的讀取顯示錯誤與重試。
    return true;
  }
  return session.has(feature.id);
};

/** 目錄中頁面或閱讀器的路由：寫上 `data.feature` 並掛上 {@link featureGuard}。 */
export function catalogRoute(feature: RoutedId, route: Route): Route {
  return {
    ...route,
    data: { ...route.data, feature },
    canMatch: [featureGuard, ...(route.canMatch ?? [])],
  };
}
