export interface Point {
  x: number;
  y: number;
}
export interface Harmonic {
  frequency: number;
  amplitude: number;
  phase: number;
}

/** Uniform arc-length samples prevent long edges from moving faster than short ones. */
export function sampleOutline(points: readonly Point[], count: number): Point[] {
  const edges = points.map((a, i) => ({
    a,
    b: points[(i + 1) % points.length],
    length: Math.hypot(
      points[(i + 1) % points.length].x - a.x,
      points[(i + 1) % points.length].y - a.y,
    ),
  }));
  const total = edges.reduce((n, e) => n + e.length, 0);
  return Array.from({ length: count }, (_, i) => {
    let distance = (total * i) / count,
      edge = edges[0];
    for (const candidate of edges) {
      edge = candidate;
      if (distance <= edge.length) break;
      distance -= edge.length;
    }
    const t = edge.length ? distance / edge.length : 0;
    return { x: edge.a.x + (edge.b.x - edge.a.x) * t, y: edge.a.y + (edge.b.y - edge.a.y) * t };
  });
}

/** Complex DFT, performed once for this small vector asset, never during animation frames. */
export function harmonics(points: readonly Point[]): Harmonic[] {
  return points
    .map((_, k) => {
      let real = 0,
        imaginary = 0;
      points.forEach((p, n) => {
        const angle = (2 * Math.PI * k * n) / points.length,
          c = Math.cos(angle),
          s = Math.sin(angle);
        real += p.x * c + p.y * s;
        imaginary += p.y * c - p.x * s;
      });
      real /= points.length;
      imaginary /= points.length;
      return {
        frequency: k > points.length / 2 ? k - points.length : k,
        amplitude: Math.hypot(real, imaginary),
        phase: Math.atan2(imaginary, real),
      };
    })
    .sort((a, b) => b.amplitude - a.amplitude);
}
export function reconstruct(values: readonly Harmonic[], phase: number): Point {
  return values.reduce(
    (p, h) => ({
      x: p.x + h.amplitude * Math.cos(h.frequency * phase + h.phase),
      y: p.y + h.amplitude * Math.sin(h.frequency * phase + h.phase),
    }),
    { x: 0, y: 0 },
  );
}
