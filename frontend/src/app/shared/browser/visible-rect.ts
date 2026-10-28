/** 視口座標的矩形，與 `DOMRect` 的 left／top／right／bottom 相同。 */
export interface Bounds {
  left: number;
  top: number;
  right: number;
  bottom: number;
}

function intersect(first: Bounds, second: Bounds): Bounds {
  return {
    left: Math.max(first.left, second.left),
    top: Math.max(first.top, second.top),
    right: Math.min(first.right, second.right),
    bottom: Math.min(first.bottom, second.bottom),
  };
}

function empty(bounds: Bounds) {
  return bounds.right <= bounds.left || bounds.bottom <= bounds.top;
}

/**
 * 元素能被看見的範圍：視口與每個會裁切的祖先（`overflow` 不是 visible，例如對話框的捲動內容）的內距框取交集。
 * 只計會裁切的那一軸；元素本身不算。
 */
export function clipBounds(element: Element): Bounds {
  let bounds: Bounds = { left: 0, top: 0, right: innerWidth, bottom: innerHeight };
  for (let node = element.parentElement; node; node = node.parentElement) {
    const style = getComputedStyle(node);
    const clipsX = style.overflowX !== 'visible',
      clipsY = style.overflowY !== 'visible';
    if (!clipsX && !clipsY) continue;
    const box = node.getBoundingClientRect();
    const left = box.left + node.clientLeft,
      top = box.top + node.clientTop;
    bounds = intersect(bounds, {
      left: clipsX ? left : -Infinity,
      top: clipsY ? top : -Infinity,
      right: clipsX ? left + node.clientWidth : Infinity,
      bottom: clipsY ? top + node.clientHeight : Infinity,
    });
  }
  return bounds;
}

/**
 * 元素實際看得到的部分；比可捲動容器高的目標（整段表單）只取露出的那一截，標示框才不會蓋到容器外的頁籤或頁尾。
 * `inset` 往外留白，但仍不超出裁切範圍。完全看不到時為 null。
 */
export function visibleRect(element: Element, inset = 0): Bounds | null {
  const clip = clipBounds(element),
    rect = element.getBoundingClientRect();
  if (empty(intersect(clip, rect))) return null;
  const padded = intersect(clip, {
    left: rect.left - inset,
    top: rect.top - inset,
    right: rect.right + inset,
    bottom: rect.bottom + inset,
  });
  return empty(padded) ? null : padded;
}
