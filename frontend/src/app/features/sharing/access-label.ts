import type { ResourceDto } from '../../core/api/schema';

/** The viewer's access to a shared resource, in the words every collection page uses. */
export function accessLabel(resource: Pick<ResourceDto, 'isOwner' | 'canEdit'>) {
  return resource.isOwner ? '我擁有' : resource.canEdit ? '共用 · 可編輯' : '共用 · 可閱讀';
}
