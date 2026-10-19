import type { NavigationKind } from '@core/layout/navigation-tree';

/** 選單節點的種類，依新增選單的順序；編輯表的「新增」與對話框的種類切換共用名稱與圖示。 */
export const NAVIGATION_KINDS: readonly { kind: NavigationKind; label: string; icon: string }[] = [
  { kind: 'group', label: '分群', icon: 'apps' },
  { kind: 'page', label: '頁面', icon: 'document' },
  { kind: 'link', label: '外部連結', icon: 'external' },
];

/** 種類的顯示名稱；伺服器回傳未知的種類時原樣顯示。 */
export function navigationKindLabel(kind: string) {
  return NAVIGATION_KINDS.find((item) => item.kind === kind)?.label ?? kind;
}
