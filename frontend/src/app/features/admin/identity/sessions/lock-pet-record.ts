/** 小鎖最遠飛了幾公尺：只存在這台瀏覽器、依帳號分開，不上傳。 */

const keyOf = (owner: string) => `ai-nexus.lock-pet.${owner}`;

/** 沒有帳號（`null`）就不讀也不寫，只記在這次鎖定。 */
export function readFarthest(owner: string | null): number {
  if (!owner) return 0;
  try {
    const meters = Number(localStorage.getItem(keyOf(owner)));
    return Number.isFinite(meters) && meters > 0 ? meters : 0;
  } catch {
    return 0;
  }
}

export function writeFarthest(owner: string | null, meters: number) {
  if (!owner) return;
  try {
    localStorage.setItem(keyOf(owner), String(meters));
  } catch {
    /* 只是好玩的紀錄；儲存可能被政策停用。 */
  }
}
