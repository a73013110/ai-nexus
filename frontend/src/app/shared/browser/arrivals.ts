/**
 * 判斷清單裡哪些項目是「後來才出現」的，讓只有新來的播進場動效（紀錄條的新一根、倒數跳動的那一位數）。
 * 第一次就在、而且一直都在的項目不算新來的；消失後再出現的算。不能改用「顯示後才加上動效 class」：
 * 對已存在的元素加上 animation 一樣會播，第一次顯示的整排會在下一幀一起動。
 *
 * 回傳的函式在每次清單改變時呼叫（通常在 `computed` 裡），得到這一版的判斷函式；同一個元素的結果不會由否變是。
 */
export function arrivals() {
  let initial: Set<string> | undefined;
  return (keys: Iterable<string>) => {
    const current = new Set(keys);
    if (!initial) initial = current;
    else for (const key of initial) if (!current.has(key)) initial.delete(key);
    const settled = initial;
    return (key: string) => !settled.has(key);
  };
}
