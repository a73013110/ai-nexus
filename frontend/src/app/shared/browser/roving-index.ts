/**
 * 群組控制（選單、頁籤、切換鈕、表格列動作）的方向鍵移動：Home／End 到頭尾，`orientation`
 * 方向的前後鍵移一格。`wrap` 時頭尾相接，否則停在邊界。不是移動鍵時回傳 null。
 * `current` 為 -1（焦點不在群組內）時，往後移到第一項；停用項目由呼叫端先排除。
 */
export function rovingIndex(
  key: string,
  current: number,
  length: number,
  {
    orientation = 'vertical',
    wrap = true,
  }: { orientation?: 'vertical' | 'horizontal'; wrap?: boolean } = {},
): number | null {
  if (key === 'Home') return 0;
  if (key === 'End') return length - 1;
  const [back, forward] =
    orientation === 'vertical' ? ['ArrowUp', 'ArrowDown'] : ['ArrowLeft', 'ArrowRight'];
  const step = key === forward ? 1 : key === back ? -1 : 0;
  if (!step) return null;
  return wrap
    ? (current + step + length) % length
    : Math.max(0, Math.min(length - 1, current + step));
}
