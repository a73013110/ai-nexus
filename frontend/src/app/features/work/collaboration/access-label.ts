import type { ResourceDto } from '@core/api/schema';
import type { ViewOption } from '@shared/ui/view-switch';

/** 檢視者對共享資源的權限，使用每個集合頁面都用的字詞。 */
export function accessLabel(resource: Pick<ResourceDto, 'isOwner' | 'canEdit'>) {
  return resource.isOwner ? '我擁有' : resource.canEdit ? '共用 · 可編輯' : '共用 · 可閱讀';
}

/** 集合頁 `ResourceGallery` 的範圍篩選；值交給 {@link filterResources}。 */
export const OWNERSHIP_FILTERS: readonly ViewOption[] = [
  { value: 'all', label: '全部' },
  { value: 'mine', label: '我擁有' },
  { value: 'shared', label: '共用給我' },
];

/** 依範圍（`OWNERSHIP_FILTERS` 的值）與名稱關鍵字（不分大小寫）篩選集合頁的項目。 */
export function filterResources<T extends { resource: Pick<ResourceDto, 'isOwner' | 'name'> }>(
  items: readonly T[],
  ownership: string,
  term: string,
): T[] {
  const needle = term.toLowerCase();
  return items.filter(
    ({ resource }) =>
      (ownership === 'all' || resource.isOwner === (ownership === 'mine')) &&
      resource.name.toLowerCase().includes(needle),
  );
}
