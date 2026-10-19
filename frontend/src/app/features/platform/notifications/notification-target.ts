import { routeOf } from '@core/api/catalog';
import type { NotificationTargetDto } from '@core/api/schema';

/** 通知只能開啟有型別的內部目的地。 */
export function notificationUrl(target: NotificationTargetDto, version = 1): string | null {
  if (
    version !== 1 ||
    !/^[\da-f]{8}-[\da-f]{4}-[\da-f]{4}-[\da-f]{4}-[\da-f]{12}$/i.test(target.id)
  )
    return null;
  const id = encodeURIComponent(target.id);
  switch (target.kind) {
    case 'conversation':
      return `${routeOf('chat')}/${id}`;
    case 'share':
      return `${routeOf('sharing')}/${id}`;
    case 'task':
      return `${routeOf('jobs')}?job=${id}`;
    case 'repository-review':
      return `${routeOf('repositories')}?review=${id}`;
    default:
      return null;
  }
}
