export interface Point {
  x: number;
  y: number;
}

// 一條連續的外框同時用於字標與登入的繪製。它是點對稱的：
// 主幹尖端位於索引 5 與 13，兩者透過中心互相對應。
export const nexusOutline: readonly Point[] = [
  { x: -0.72, y: 0.72 },
  { x: -0.72, y: -0.55 },
  { x: -0.96, y: -0.55 },
  { x: -0.96, y: -0.72 },
  { x: -0.72, y: -0.72 },
  { x: -0.72, y: -0.9 },
  { x: 0.55, y: 0.55 },
  { x: 0.55, y: -0.72 },
  { x: 0.72, y: -0.72 },
  { x: 0.72, y: 0.55 },
  { x: 0.96, y: 0.55 },
  { x: 0.96, y: 0.72 },
  { x: 0.72, y: 0.72 },
  { x: 0.72, y: 0.9 },
  { x: -0.55, y: -0.55 },
  { x: -0.55, y: 0.72 },
];
const pathOf = (points: readonly Point[]) =>
  points
    .map(({ x: pointX, y: pointY }, index) => `${index ? 'L' : 'M'}${pointX} ${pointY}`)
    .join(' ');
export const nexusPath = pathOf(nexusOutline) + ' Z';

const TIPS = [5, 13] as const;
/** 外框在兩個主幹尖端處切開：每一半都從一個尖端走到另一個。 */
export const nexusHalves: readonly (readonly Point[])[] = TIPS.map((start, index2) => {
  const end = TIPS[1 - index2];
  const count = (end - start + nexusOutline.length) % nexusOutline.length;
  return Array.from(
    { length: count + 1 },
    (_, index3) => nexusOutline[(start + index3) % nexusOutline.length],
  );
});
export const nexusHalfPaths = nexusHalves.map(pathOf);
export const nexusTips = TIPS.map((index4) => nexusOutline[index4]);

/** 開放折線上依弧長走到第 `t` 處（0–1）的點。 */
export function pointAlong(points: readonly Point[], time: number): Point {
  const lengths = points
    .slice(1)
    .map((part, index5) => Math.hypot(part.x - points[index5].x, part.y - points[index5].y));
  let distance = Math.min(1, Math.max(0, time)) * lengths.reduce((sum, count2) => sum + count2, 0);
  for (let index6 = 0; index6 < lengths.length; index6++) {
    if (distance <= lengths[index6] || index6 === lengths.length - 1) {
      const index7 = lengths[index6] ? Math.min(1, distance / lengths[index6]) : 0;
      const first = points[index6],
        second = points[index6 + 1];
      return {
        x: first.x + (second.x - first.x) * index7,
        y: first.y + (second.y - first.y) * index7,
      };
    }
    distance -= lengths[index6];
  }
  return points[0];
}
