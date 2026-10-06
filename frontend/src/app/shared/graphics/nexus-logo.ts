import type { Point } from './fourier';

// One continuous outline serves both the wordmark and its Fourier drawing.
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
export const nexusPath =
  nexusOutline.map(({ x, y }, i) => `${i ? 'L' : 'M'}${x} ${y}`).join(' ') + ' Z';
