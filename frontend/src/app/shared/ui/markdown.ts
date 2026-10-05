import MarkdownIt from 'markdown-it';
import DOMPurify from 'dompurify';
import hljs from 'highlight.js/lib/core';
import typescript from 'highlight.js/lib/languages/typescript';
import javascript from 'highlight.js/lib/languages/javascript';
import csharp from 'highlight.js/lib/languages/csharp';
import sql from 'highlight.js/lib/languages/sql';
import json from 'highlight.js/lib/languages/json';
import bash from 'highlight.js/lib/languages/bash';

for (const [name, language] of Object.entries({ typescript, javascript, csharp, sql, json, bash }))
  hljs.registerLanguage(name, language);
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
  const label = /^[\w#+-]{1,24}$/.test(language) ? language : 'text';
  const code =
    !env?.['streaming'] && hljs.getLanguage(language)
      ? hljs.highlight(token.content, { language, ignoreIllegals: true }).value
      : escape(token.content);
  return `<div class="code-block"><div class="code-toolbar"><span>${escape(label)}</span><button type="button" class="code-copy" title="複製程式碼" aria-label="複製程式碼">複製程式碼</button></div><pre><code>${code}</code></pre></div>`;
};
// This is the sole trusted-HTML boundary. Raw HTML is disabled at parsing, then the
// generated markup is reduced to a small HTML allowlist. No arbitrary HTML input is trusted.
export const markdownBlockStarts = (content: string): number[] =>
  markdown
    .parse(content, {})
    .filter((token) => token.level === 0 && token.map)
    .map((token) => token.map![0]);

export const renderMarkdown = (content: string, streaming = false): string =>
  DOMPurify.sanitize(markdown.render(content, { streaming }), {
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
    ],
    ALLOW_DATA_ATTR: false,
    ADD_URI_SAFE_ATTR: ['target', 'rel', 'type', 'start', 'colspan', 'rowspan'],
    ALLOWED_URI_REGEXP: /^(?:https?:\/\/|#[\w-]*$)/i,
    RETURN_TRUSTED_TYPE: false,
  });
