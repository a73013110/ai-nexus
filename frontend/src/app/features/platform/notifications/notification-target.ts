import { routeOf, type RoutedId } from '@core/api/catalog';
import type { NotificationTargetDto } from '@core/api/schema';

/** 每種通知目的地屬於哪個功能、接在該功能網址後面的部分；開啟通知與側欄徽章共用這份對應。 */
const TARGETS = new Map<string, { feature: RoutedId; suffix: (id: string) => string }>([
  ['conversation', { feature: 'chat', suffix: (id) => `/${id}` }],
  ['share', { feature: 'sharing', suffix: (id) => `/${id}` }],
  ['task', { feature: 'jobs', suffix: (id) => `?job=${id}` }],
  ['repository-review', { feature: 'repositories', suffix: (id) => `?review=${id}` }],
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
