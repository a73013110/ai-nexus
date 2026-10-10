import type { MermaidConfig } from 'mermaid';
import { measureDiagramBounds, sanitizeDiagramSvg } from './mermaid-svg';
import type { DiagramSemantics } from './mermaid-theme';

let library: Promise<{ default: typeof import('mermaid').default }> | undefined;
let queue: Promise<unknown> = Promise.resolve();
let sequence = 0;

/** Mermaid owns global configuration; serialize rendering to keep themes and IDs isolated. */
export function renderDiagram(
  source: string,
  config: MermaidConfig,
  active: () => boolean,
  semantics?: DiagramSemantics,
) {
  const render = queue
    .catch(() => undefined)
    .then(async () => {
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
      container.className = 'mermaid-measure';
      container.setAttribute('aria-hidden', 'true');
      document.body.append(container);
      try {
        const result = await mermaid.render(`nx-mermaid-${++sequence}`, source, container);
        return active()
          ? measureDiagramBounds(
              sanitizeDiagramSvg(
                result.svg,
                config.themeVariables?.primaryTextColor || '#000000',
                config.themeVariables?.background || '#ffffff',
                parsed.type.startsWith('flowchart') ? semantics : undefined,
              ),
              container,
            )
          : null;
      } finally {
        container.remove();
      }
    });
  queue = render;
  return render;
}
