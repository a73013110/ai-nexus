import type { MermaidConfig } from 'mermaid';
import { LruCache } from './lru-cache';
import { measureDiagramBounds, sanitizeDiagramSvg } from './mermaid-svg';
import type { DiagramSemantics } from './mermaid-theme';

export type Diagram = ReturnType<typeof sanitizeDiagramSvg>;

/** One mount's hold on a shared render. Release it when the mount is replaced or destroyed. */
export interface DiagramView {
  readonly diagram: Diagram;
  readonly url: string;
  release(): void;
}

interface RenderedDiagram {
  readonly diagram: Diagram;
  url: string;
  users: number;
  idle?: ReturnType<typeof setTimeout>;
}

let library: Promise<{ default: typeof import('mermaid').default }> | undefined;
let queue: Promise<unknown> = Promise.resolve();
let sequence = 0;
// An unused Blob URL lingers so the stream → final swap and quick remounts show the same, already
// decoded image instead of a blank frame; it is then revoked (the SVG text stays cached).
const linger = 2000;
// Rendering dominates the cost; SVGs run from a few to a few hundred KB, so the size bound
// (~8 MB of UTF-16) usually applies before the count.
const rendered = new LruCache<string, RenderedDiagram>({
  maxEntries: 48,
  maxWeight: 4_000_000,
  weigh: (entry) => entry.diagram.svg.length,
  evicted: (entry) => {
    if (!entry.users) revoke(entry);
  },
});

// The theme is computed from the host's tokens, so light/dark and token changes produce new keys.
const diagramKey = (source: string, config: MermaidConfig, semantics?: DiagramSemantics) =>
  JSON.stringify([source, config, semantics ?? null]);

/** A finished render of this source and theme, synchronously, or null. Failures are not cached. */
export function cachedDiagram(
  source: string,
  config: MermaidConfig,
  semantics?: DiagramSemantics,
): DiagramView | null {
  const entry = rendered.get(diagramKey(source, config, semantics));
  return entry ? acquire(entry) : null;
}

function acquire(entry: RenderedDiagram): DiagramView {
  clearTimeout(entry.idle);
  entry.users++;
  // Each mount displays the SVG in its own image document, so identical IDs never collide.
  entry.url ||= URL.createObjectURL(new Blob([entry.diagram.svg], { type: 'image/svg+xml' }));
  let held = true;
  return {
    diagram: entry.diagram,
    url: entry.url,
    release() {
      if (!held) return;
      held = false;
      if (--entry.users === 0) entry.idle = setTimeout(() => revoke(entry), linger);
    },
  };
}

function revoke(entry: RenderedDiagram) {
  clearTimeout(entry.idle);
  if (entry.url) URL.revokeObjectURL(entry.url);
  entry.url = '';
}

/**
 * Mermaid owns global configuration; serialize rendering to keep themes and IDs isolated.
 * Requests queued behind a render of the same source and theme reuse its result.
 */
export function renderDiagram(
  source: string,
  config: MermaidConfig,
  active: () => boolean,
  semantics?: DiagramSemantics,
): Promise<DiagramView | null> {
  const key = diagramKey(source, config, semantics);
  const render = queue
    .catch(() => undefined)
    .then(async () => {
      const cached = rendered.get(key);
      if (cached) return active() ? acquire(cached) : null;
      if (!active()) return null;
      if (source.length > 50000) throw new Error('Diagram exceeds the supported size');
      // Official browser ESM distribution keeps upstream CommonJS internals prebundled,
      // with every diagram type available and no bundler-specific dependency exceptions.
      library ||= import('mermaid/dist/mermaid.esm.min.mjs').catch((error) => {
        library = undefined;
        throw error;
      });
      const { default: mermaid } = await library;
      await document.fonts.ready;
      if (!active()) return null;
      mermaid.initialize({
        ...config,
        startOnLoad: false,
        securityLevel: 'strict',
        htmlLabels: false,
        forceLegacyMathML: false,
        suppressErrorRendering: true,
        maxTextSize: 50000,
        maxEdges: 500,
        secure: [
          ...Object.keys(mermaid.mermaidAPI.defaultConfig),
          ...Object.keys(config),
          'startOnLoad',
          'securityLevel',
          'htmlLabels',
          'suppressErrorRendering',
          'maxTextSize',
          'maxEdges',
          'secure',
          'dompurifyConfig',
          'themeCSS',
        ],
      });
      // Mermaid's image nodes fetch assets while measuring, before SVG sanitization.
      // Inspect parsed flowchart nodes first to preserve Markdown's no-image-request contract.
      const parsed = await mermaid.mermaidAPI.getDiagramFromText(source);
      if (parsed.type.startsWith('flowchart')) {
        const data = (parsed.db as { getData(): { nodes: { img?: string }[] } }).getData();
        if (data.nodes.some((node) => node.img)) throw new Error('Diagram images are disabled');
      }
      if (!active()) return null;
      const container = document.createElement('div');
      // Mermaid measures text in a laid-out but invisible container outside any component.
      Object.assign(container.style, {
        position: 'fixed',
        inset: '0 auto auto 0',
        width: '1024px',
        visibility: 'hidden',
        pointerEvents: 'none',
      });
      container.setAttribute('aria-hidden', 'true');
      document.body.append(container);
      try {
        const result = await mermaid.render(`nx-mermaid-${++sequence}`, source, container);
        // Kept even when the requester has gone: the settled answer remounts the same diagram.
        const entry: RenderedDiagram = {
          diagram: measureDiagramBounds(
            sanitizeDiagramSvg(
              result.svg,
              config.themeVariables?.primaryTextColor || '#000000',
              config.themeVariables?.background || '#ffffff',
              parsed.type.startsWith('flowchart') ? semantics : undefined,
            ),
            container,
          ),
          url: '',
          users: 0,
        };
        rendered.set(key, entry);
        return active() ? acquire(entry) : null;
      } finally {
        container.remove();
      }
    });
  queue = render;
  return render;
}
