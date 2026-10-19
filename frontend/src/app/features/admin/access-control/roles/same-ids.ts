/** 兩組 ID 是否相同（不計順序）；角色、群組的勾選與使用者的角色指派都用它判斷有沒有變更。 */
export function sameIds(items: readonly string[], items2: readonly string[]) {
  return items.length === items2.length && items.every((id) => items2.includes(id));
}
