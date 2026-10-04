import { describe, expect, it } from 'vitest';
import { harmonics, reconstruct, sampleOutline } from './fourier';
describe('Fourier vector reconstruction', () => {
  it('preserves complex positive and negative frequencies and the constant offset', () => {
    const samples = Array.from({ length: 64 }, (_, i) => {
      const t = (i / 64) * Math.PI * 2;
      return {
        x: 3 + Math.cos(t) + 0.2 * Math.cos(-4 * t),
        y: -2 + Math.sin(t) + 0.2 * Math.sin(-4 * t),
      };
    });
    const spectrum = harmonics(samples);
    for (let i = 0; i < samples.length; i++) {
      const p = reconstruct(spectrum, (i / 64) * Math.PI * 2);
      expect(p.x).toBeCloseTo(samples[i].x, 9);
      expect(p.y).toBeCloseTo(samples[i].y, 9);
    }
  });
  it('samples different edge lengths at a consistent travel speed', () => {
    expect(
      sampleOutline(
        [
          { x: 0, y: 0 },
          { x: 3, y: 0 },
          { x: 3, y: 1 },
          { x: 0, y: 1 },
        ],
        8,
      ),
    ).toEqual([
      { x: 0, y: 0 },
      { x: 1, y: 0 },
      { x: 2, y: 0 },
      { x: 3, y: 0 },
      { x: 3, y: 1 },
      { x: 2, y: 1 },
      { x: 1, y: 1 },
      { x: 0, y: 1 },
    ]);
  });
});
