import type { ResourceDto } from '@core/api/schema';

/** 檢視者對共享資源的權限，使用每個集合頁面都用的字詞。 */
export function accessLabel(resource: Pick<ResourceDto, 'isOwner' | 'canEdit'>) {
  return resource.isOwner ? '我擁有' : resource.canEdit ? '共用 · 可編輯' : '共用 · 可閱讀';
}
