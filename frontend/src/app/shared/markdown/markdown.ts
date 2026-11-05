import MarkdownIt from 'markdown-it';
import DOMPurify from 'dompurify';
import { LruCache } from './lru-cache';
const markdown = new MarkdownIt({ html: false, linkify: false, typographer: false, breaks: true });
const escape = markdown.utils.escapeHtml;
markdown.renderer.rules['image'] = (tokens, index) =>
  `<span class="image-description">［圖片：${escape(tokens[index].content)}］</span>`;
markdown.renderer.rules['link_open'] = (tokens, index, options, _env, self) => {
  const token = tokens[index];
  const href = String(token.attrGet('href') ?? '');
  if (!/^(https?:\/\/|#[\w-]+$)/i.test(href)) token.attrSet('href', '#');
  token.attrSet('target', '_blank');
  token.attrSet('rel', 'noopener noreferrer');
  return self.renderToken(tokens, index, options);
};
/**
 * 圍欄資訊字串的語言標籤（小寫的第一個字）；不像語言名稱的一律當成 `text`，
 * 標籤會顯示在工具列上，也決定要不要延後上色。串流中的開啟圍欄也用同一套規則。
 */
export function fenceLanguage(info: string) {
  const language = info.trim().split(/\s+/)[0].toLowerCase();
  return /^[\w#+-]{1,24}$/.test(language) ? language : 'text';
}
markdown.renderer.rules['fence'] = (tokens, index, _options, env) => {
  const token = tokens[index];
  const label = fenceLanguage(token.info);
  const diagrams = env?.['diagrams'] as string[] | undefined;
  if (label === 'mermaid' && diagrams && !env?.['streaming']) {
    const index = diagrams.push(token.content) - 1;
    return `<div class="markdown-diagram-slot" data-diagram-index="${index}"></div>`;
  }
  // 語法高亮稍後在視窗附近進行（code-highlighting.ts）；這裡先跳脫文字。
  const deferred =
    !env?.['streaming'] && label !== 'text' ? ` data-language="${escape(label)}"` : '';
  return `<div class="embed embed-code">${codeEmbedBar(label)}<pre class="embed-body"><code${deferred}>${escape(token.content)}</code></pre></div>`;
};
// 依據編號（助理的「早退 2 天 [3]」）：只在呼叫端給了依據數量時才認，`[3](網址)` 仍是連結，
// 超出數量的編號照原文顯示，不變成指向不存在依據的按鈕。點擊由 `MarkdownView` 以 `.markdown-cite` 委派。
markdown.inline.ruler.before('link', 'citation', (state, silent) => {
  const citations = Number(state.env?.['citations'] ?? 0);
  if (!citations || state.src.charCodeAt(state.pos) !== 0x5b) return false;
  const match = /^\[(\d{1,3})\](?!\()/.exec(state.src.slice(state.pos));
  const number = Number(match?.[1]);
  if (!match || number < 1 || number > citations) return false;
  if (!silent) state.push('citation', '', 0).content = String(number);
  state.pos += match[0].length;
  return true;
});
markdown.renderer.rules['citation'] = (tokens, index) => {
  const number = tokens[index].content;
  return `<button type="button" class="markdown-cite" aria-label="依據 ${number}" title="依據 ${number}">${number}</button>`;
};
// 表格包進內嵌區塊：寬表格在區塊內橫向捲動，不撐寬回答。
markdown.renderer.rules['table_open'] = () => '<div class="embed embed-table"><table>';
markdown.renderer.rules['table_close'] = () => '</table></div>';

/**
 * 程式碼區塊的標頭（語言＋複製鈕），點擊由 `MarkdownView` 以 `.embed-copy` 委派。
 * 串流中的圍欄（`StreamingAnswer`）以 Angular 範本畫同一組 class：它不經過這個可信 HTML 邊界，
 * 改這裡時兩邊一起改，串流結束換成完整渲染才不會跳動。
 */
export function codeEmbedBar(language: string) {
  return `<div class="embed-bar"><span class="embed-label">${escape(language)}</span><span class="embed-actions"><button type="button" class="embed-copy" title="複製程式碼" aria-label="複製程式碼">複製</button></span></div>`;
}
/** 最上層區塊的起始行；若最後一個區塊是列表，則也包含其項目的起始行。 */
export function markdownBlockStructure(content: string) {
  const starts: number[] = [];
  let items: number[] = [];
  let list = false;
  for (const token of markdown.parse(content, {})) {
    if (token.level === 0 && token.map) {
      starts.push(token.map[0]);
      list = token.type === 'bullet_list_open' || token.type === 'ordered_list_open';
      items = [];
    } else if (list && token.level === 1 && token.type === 'list_item_open' && token.map)
      items.push(token.map[0]);
  }
  return { starts, items };
}

export interface RenderedMarkdown {
  readonly html: string;
  readonly diagrams: readonly string[];
}

// 渲染只取決於文字，所以重新掛載（切換對話、分享、成品）會重用它。
// 約為兩段長對話的回答，最多約 8 MB UTF-16 的原始碼與 HTML。
const rendered = new LruCache<string, RenderedMarkdown>({
  maxEntries: 256,
  maxWeight: 4_000_000,
  weigh: (value, content) => content.length + value.html.length,
});

/**
 * 這裡是唯一的可信 HTML 邊界。解析時停用原始 HTML，接著把產生的標記縮減為一個小的 HTML 白名單；
 * 任何任意的 HTML 輸入都不被信任。圍欄保留原本的巢狀結構；只有渲染器自有的插槽會掛載 Angular 元件。
 */
export function renderMarkdownWithDiagrams(
  content: string,
  streaming = false,
  citations = 0,
): RenderedMarkdown {
  // 串流的前緣每幀都會改變，且從不重複顯示，因此不快取。依據數量會改變輸出，所以算進鍵。
  const key = citations ? `${citations}\u0000${content}` : content;
  const cached = streaming ? undefined : rendered.get(key);
  if (cached) return cached;
  const diagrams: string[] = [];
  const html = sanitizeMarkdown(markdown.render(content, { streaming, diagrams, citations }));
  const result: RenderedMarkdown = Object.freeze({ html, diagrams: Object.freeze(diagrams) });
  if (!streaming) rendered.set(key, result);
  return result;
}

const sanitizeMarkdown = (html: string): string =>
  DOMPurify.sanitize(html, {
    ALLOWED_TAGS: [
      'p',
      'br',
      'strong',
      'em',
      's',
      'blockquote',
      'ul',
      'ol',
      'li',
      'hr',
      'table',
      'thead',
      'tbody',
      'tr',
      'th',
      'td',
      'pre',
      'code',
      'span',
      'div',
      'button',
      'a',
      'h1',
      'h2',
      'h3',
      'h4',
      'h5',
      'h6',
    ],
    ALLOWED_ATTR: [
      'class',
      'href',
      'target',
      'rel',
      'title',
      'aria-label',
      'type',
      'start',
      'colspan',
      'rowspan',
      'data-diagram-index',
      'data-language',
    ],
    ALLOW_DATA_ATTR: false,
    ADD_URI_SAFE_ATTR: [
      'target',
      'rel',
      'type',
      'start',
      'colspan',
      'rowspan',
      'data-diagram-index',
      'data-language',
    ],
    ALLOWED_URI_REGEXP: /^(?:https?:\/\/|#[\w-]*$)/i,
    RETURN_TRUSTED_TYPE: false,
  });
