import MarkdownIt from 'markdown-it';
import DOMPurify from 'dompurify';
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
markdown.renderer.rules['fence'] = (tokens, index, _options, env) => {
  const token = tokens[index];
  const language = token.info.trim().split(/\s+/)[0].toLowerCase();
  const diagrams = env?.['diagrams'] as string[] | undefined;
  if (language === 'mermaid' && diagrams && !env?.['streaming']) {
    const index = diagrams.push(token.content) - 1;
    return `<div class="markdown-diagram-slot" data-diagram-index="${index}"></div>`;
  }
  const label = /^[\w#+-]{1,24}$/.test(language) ? language : 'text';
  // Highlighting happens later, near the viewport (code-highlighting.ts); text is escaped here.
  const deferred =
    !env?.['streaming'] && label !== 'text' ? ` data-language="${escape(label)}"` : '';
  return `<div class="code-block"><div class="code-toolbar"><span>${escape(label)}</span><button type="button" class="code-copy" title="複製程式碼" aria-label="複製程式碼">複製程式碼</button></div><pre><code${deferred}>${escape(token.content)}</code></pre></div>`;
};
/** Start lines of the top-level blocks and, when the last block is a list, of its items. */
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

// This is the sole trusted-HTML boundary. Raw HTML is disabled at parsing, then the
// generated markup is reduced to a small HTML allowlist. No arbitrary HTML input is trusted.
export const renderMarkdown = (content: string, streaming = false): string =>
  sanitizeMarkdown(markdown.render(content, { streaming }));

/** Fences keep their original nesting; only renderer-owned slots host Angular widgets. */
export function renderMarkdownWithDiagrams(content: string, streaming = false) {
  const diagrams: string[] = [];
  const html = sanitizeMarkdown(markdown.render(content, { streaming, diagrams }));
  return { html, diagrams };
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
