import type { MermaidConfig } from 'mermaid';
import { LruCache } from './lru-cache';
import { measureDiagramBounds, sanitizeDiagramSvg } from './mermaid-svg';
import type { DiagramSemantics } from './mermaid-theme';

export type Diagram = ReturnType<typeof sanitizeDiagramSvg>;

/** 單一掛載點對共用渲染結果的持有。掛載點被替換或銷毀時釋放。 */
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
// 未使用的 Blob URL 會暫留，讓串流轉最終結果，以及快速重新掛載時，
// 顯示同一張已解碼的圖片而非空白畫面；之後才撤銷（SVG 文字仍快取）。
const linger = 2000;
// 渲染才是主要成本；SVG 大小從數 KB 到數百 KB，因此大小上限（約 8 MB UTF-16）
// 通常比數量上限先生效。
const rendered = new LruCache<string, RenderedDiagram>({
  maxEntries: 48,
  maxWeight: 4_000_000,
  weigh: (entry) => entry.diagram.svg.length,
  evicted: (entry) => {
    if (!entry.users) revoke(entry);
  },
});

// 主題由宿主的 token 計算，所以淺色／深色與 token 改變時會產生新的 key。
const diagramKey = (source: string, config: MermaidConfig, semantics?: DiagramSemantics) =>
  JSON.stringify([source, config, semantics ?? null]);

/** 此來源與主題已完成的渲染結果（同步取得），沒有則為 null。失敗不快取。 */
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
  // 每個掛載點在自己的影像文件中顯示 SVG，所以相同的 ID 不會衝突。
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
 * Mermaid 擁有全域設定；渲染需要序列化，才能讓主題與 ID 互不干擾。
 * 排在同一來源與主題的渲染之後的請求，會重用其結果。
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
      // 官方的瀏覽器 ESM 發行版已預先打包上游的 CommonJS 內部模組，
      // 所有圖表類型都可用，也不需要任何 bundler 專屬的例外處理。
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
      // Mermaid 的影像節點會在量測時、SVG 消毒之前抓取資源。
      // 因此先檢查解析後的流程圖節點，以維持 Markdown「不發出影像請求」的約定。
      const parsed = await mermaid.mermaidAPI.getDiagramFromText(source);
      if (parsed.type.startsWith('flowchart')) {
        const data = (parsed.db as { getData(): { nodes: { img?: string }[] } }).getData();
        if (data.nodes.some((node) => node.img)) throw new Error('Diagram images are disabled');
      }
      if (!active()) return null;
      const container = document.createElement('div');
      // Mermaid 在一個已排版但不可見、位於任何元件之外的容器中量測文字。
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
        // 即使請求者已離開也保留：已完成的結果會讓同一張圖重新掛載。
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
