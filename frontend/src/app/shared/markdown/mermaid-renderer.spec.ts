import type { MermaidConfig } from 'mermaid';
import { afterAll, beforeAll, describe, expect, it, vi } from 'vitest';

const mermaid = vi.hoisted(() => ({
  initialize: vi.fn(),
  render: vi.fn(async (id: string, source: string) => ({
    svg: `<svg id="${id}" viewBox="0 0 10 10"><text>${source.length}</text></svg>`,
  })),
  mermaidAPI: {
    defaultConfig: {},
    getDiagramFromText: vi.fn(async () => ({ type: 'sequence', db: {} })),
  },
}));
vi.mock('mermaid/dist/mermaid.esm.min.mjs', () => ({ default: mermaid }));

import { cachedDiagram, renderDiagram } from './mermaid-renderer';

const light: MermaidConfig = { theme: 'base', themeVariables: { darkMode: false } };
const dark: MermaidConfig = { theme: 'base', themeVariables: { darkMode: true } };

describe('Mermaid render cache', () => {
  const urls = { created: 0, revoked: [] as string[] };
  const { createObjectURL, revokeObjectURL } = URL;
  beforeAll(() => {
    // jsdom has no font loading, Blob URLs, SVG geometry or canvas.
    Object.defineProperty(document, 'fonts', {
      configurable: true,
      value: { ready: Promise.resolve() },
    });
    Object.defineProperties(SVGElement.prototype, {
      getBBox: { configurable: true, value: () => ({ x: 0, y: 0, width: 10, height: 10 }) },
      viewBox: {
        configurable: true,
        get: () => ({ baseVal: { x: 0, y: 0, width: 10, height: 10 } }),
      },
    });
    vi.spyOn(HTMLCanvasElement.prototype, 'getContext').mockReturnValue(null);
    Object.assign(URL, {
      createObjectURL: () => `blob:diagram-${++urls.created}`,
      revokeObjectURL: (url: string) => urls.revoked.push(url),
    });
  });
  afterAll(() => {
    Object.assign(URL, { createObjectURL, revokeObjectURL });
    delete (document as { fonts?: unknown }).fonts;
    delete (SVGElement.prototype as { getBBox?: unknown }).getBBox;
    delete (SVGElement.prototype as { viewBox?: unknown }).viewBox;
    vi.restoreAllMocks();
  });

  it('renders a source and theme once; later mounts share the same SVG and image URL', async () => {
    const source = 'sequenceDiagram\nA->>B: 一次';
    const [first, second] = await Promise.all([
      renderDiagram(source, light, () => true),
      renderDiagram(source, light, () => true),
    ]);
    expect(mermaid.render).toHaveBeenCalledTimes(1);
    expect(second!.diagram).toBe(first!.diagram);
    expect(second!.url).toBe(first!.url);
    const remount = cachedDiagram(source, light);
    expect(remount?.url).toBe(first!.url);
    expect(mermaid.render).toHaveBeenCalledTimes(1);
    for (const view of [first, second, remount]) view!.release();
  });

  it('keys by theme, so a theme change renders again and never reuses a stale SVG', async () => {
    const source = 'sequenceDiagram\nA->>B: 主題';
    const calls = mermaid.render.mock.calls.length;
    const shown = await renderDiagram(source, light, () => true);
    expect(cachedDiagram(source, dark)).toBeNull();
    const switched = await renderDiagram(source, dark, () => true);
    expect(mermaid.render.mock.calls.length).toBe(calls + 2);
    expect(switched!.url).not.toBe(shown!.url);
    shown!.release();
    switched!.release();
  });

  it('keeps a finished render for later mounts even when its requester is gone', async () => {
    const source = 'sequenceDiagram\nA->>B: 交接';
    let mounted = true;
    // The streaming view is replaced by the final answer while Mermaid is still drawing.
    mermaid.render.mockImplementationOnce(async (id: string) => {
      mounted = false;
      return { svg: `<svg id="${id}" viewBox="0 0 10 10"></svg>` };
    });
    expect(await renderDiagram(source, light, () => mounted)).toBeNull();
    const calls = mermaid.render.mock.calls.length;
    const settled = cachedDiagram(source, light);
    expect(settled).not.toBeNull();
    expect(mermaid.render.mock.calls.length).toBe(calls);
    settled!.release();
  });

  it('does not cache failures', async () => {
    const source = 'not a diagram';
    mermaid.render.mockRejectedValueOnce(new Error('Parse error'));
    await expect(renderDiagram(source, light, () => true)).rejects.toThrow('Parse error');
    expect(cachedDiagram(source, light)).toBeNull();
    const retried = await renderDiagram(source, light, () => true);
    expect(retried).not.toBeNull();
    retried!.release();
  });

  it('revokes an unused image URL after a short linger, then recreates it on demand', async () => {
    vi.useFakeTimers();
    try {
      const source = 'sequenceDiagram\nA->>B: 釋放';
      const view = await renderDiagram(source, light, () => true);
      view!.release();
      view!.release();
      const again = cachedDiagram(source, light)!;
      expect(again.url).toBe(view!.url);
      again.release();
      vi.runAllTimers();
      expect(urls.revoked).toContain(view!.url);
      const later = cachedDiagram(source, light)!;
      expect(later.url).not.toBe(view!.url);
      later.release();
    } finally {
      vi.useRealTimers();
    }
  });
});
