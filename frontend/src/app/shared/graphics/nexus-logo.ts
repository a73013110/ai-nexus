export interface Point {
  x: number;
  y: number;
}

// One continuous outline serves both the wordmark and its sign-in drawing. It is point-symmetric:
// the stem tips at index 5 and 13 mirror each other through the centre.
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
  points.map(({ x, y }, i) => `${i ? 'L' : 'M'}${x} ${y}`).join(' ');
export const nexusPath = pathOf(nexusOutline) + ' Z';

const TIPS = [5, 13] as const;
/** The outline split at the two stem tips: each half runs from one tip to the other. */
export const nexusHalves: readonly (readonly Point[])[] = TIPS.map((start, i) => {
  const end = TIPS[1 - i];
  const count = (end - start + nexusOutline.length) % nexusOutline.length;
  return Array.from(
    { length: count + 1 },
    (_, k) => nexusOutline[(start + k) % nexusOutline.length],
  );
});
export const nexusHalfPaths = nexusHalves.map(pathOf);
export const nexusTips = TIPS.map((i) => nexusOutline[i]);

/** The point `t` (0–1) of the way along an open polyline, by arc length. */
export function pointAlong(points: readonly Point[], t: number): Point {
  const lengths = points.slice(1).map((p, i) => Math.hypot(p.x - points[i].x, p.y - points[i].y));
  let distance = Math.min(1, Math.max(0, t)) * lengths.reduce((sum, n) => sum + n, 0);
  for (let i = 0; i < lengths.length; i++) {
    if (distance <= lengths[i] || i === lengths.length - 1) {
      const k = lengths[i] ? Math.min(1, distance / lengths[i]) : 0;
      const a = points[i],
        b = points[i + 1];
      return { x: a.x + (b.x - a.x) * k, y: a.y + (b.y - a.y) * k };
    }
    distance -= lengths[i];
  }
  return points[0];
}
