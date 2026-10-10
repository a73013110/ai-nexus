import type { NotificationTargetDto } from '../../core/api/schema';

/** Only typed internal destinations may be opened by a notification. */
export function notificationUrl(target: NotificationTargetDto, version = 1): string | null {
  if (
    version !== 1 ||
    !/^[\da-f]{8}-[\da-f]{4}-[\da-f]{4}-[\da-f]{4}-[\da-f]{12}$/i.test(target.id)
  )
    return null;
  const id = encodeURIComponent(target.id);
  switch (target.kind) {
    case 'conversation':
      return `/chat/${id}`;
    case 'share':
      return `/shared/${id}`;
    case 'task':
      return `/tasks?job=${id}`;
    case 'repository-review':
      return `/repositories?review=${id}`;
    default:
      return null;
  }
}
