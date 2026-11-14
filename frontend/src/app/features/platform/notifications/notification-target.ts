import { routeOf, type RoutedId } from '@core/api/catalog';
import type { NotificationTargetDto } from '@core/api/schema';

/** 每種通知目的地屬於哪個功能、接在該功能網址後面的部分；開啟通知與側欄徽章共用這份對應。 */
const TARGETS = new Map<string, { feature: RoutedId; suffix: (id: string) => string }>([
  ['conversation', { feature: 'chat', suffix: (id) => `/${id}` }],
  ['share', { feature: 'sharing', suffix: (id) => `/${id}` }],
  ['task', { feature: 'jobs', suffix: (id) => `?job=${id}` }],
  ['repository-review', { feature: 'repositories', suffix: (id) => `?review=${id}` }],
  // 結構異動是整個資料庫的狀態，id 不使用（伺服器送空的 GUID）。
  ['schema-changes', { feature: 'releases', suffix: () => '' }],
  // 排程任務連續失敗：id 由任務 key 雜湊而來，頁面不必反查；失敗的任務在背景作業頁本來就排在最前面。
  ['scheduled-task', { feature: 'scheduling', suffix: () => '' }],
  // 簽核的指派、提醒與結果：開啟申請詳情。
  ['flow', { feature: 'flow.approvals', suffix: (id) => `?instance=${id}` }],
  // 儲存庫權限的授予、移除與到期：開啟該儲存庫的權限詳情。
  ['repository-access', { feature: 'repositories.access', suffix: (id) => `?repository=${id}` }],
]);

/** 通知指向的功能；不認得的種類為 `null`。 */
export function notificationFeature(target: NotificationTargetDto): RoutedId | null {
  return TARGETS.get(target.kind)?.feature ?? null;
}

/** 通知只能開啟有型別的內部目的地。 */
export function notificationUrl(target: NotificationTargetDto, version = 1): string | null {
  const destination = TARGETS.get(target.kind);
  if (
    !destination ||
    version !== 1 ||
    !/^[\da-f]{8}-[\da-f]{4}-[\da-f]{4}-[\da-f]{4}-[\da-f]{12}$/i.test(target.id)
  )
    return null;
  return routeOf(destination.feature) + destination.suffix(encodeURIComponent(target.id));
}

/**
 * 這個網址正顯示通知的目的地：路徑相同，且目的地要求的查詢參數都在（頁面可以另有自己的參數）。
 * 使用者正在看的目的地，通知就算讀過。
 */
export function showsTarget(url: string, target: NotificationTargetDto, version = 1): boolean {
  const destination = notificationUrl(target, version);
  if (!destination) return false;
  // 只借 URL 解析路徑與查詢，網域無關。
  const wanted = new URL(destination, 'http://app'),
    current = new URL(url, 'http://app');
  return (
    wanted.pathname === current.pathname &&
    [...wanted.searchParams].every(([key, value]) => current.searchParams.get(key) === value)
  );
}
