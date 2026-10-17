import DOMPurify from 'dompurify';
import hljs from 'highlight.js/lib/core';
import typescript from 'highlight.js/lib/languages/typescript';
import javascript from 'highlight.js/lib/languages/javascript';
import csharp from 'highlight.js/lib/languages/csharp';
import sql from 'highlight.js/lib/languages/sql';
import json from 'highlight.js/lib/languages/json';
import bash from 'highlight.js/lib/languages/bash';
import xml from 'highlight.js/lib/languages/xml';
import css from 'highlight.js/lib/languages/css';
import scss from 'highlight.js/lib/languages/scss';
import yaml from 'highlight.js/lib/languages/yaml';
import python from 'highlight.js/lib/languages/python';
import java from 'highlight.js/lib/languages/java';
import powershell from 'highlight.js/lib/languages/powershell';
import ini from 'highlight.js/lib/languages/ini';
import diff from 'highlight.js/lib/languages/diff';

// 文法也會登記它們的別名（ts、cs、sh、html、yml、py、ps1、toml、patch）。
for (const [name, language] of Object.entries({
  typescript,
  javascript,
  csharp,
  sql,
  json,
  bash,
  xml,
  css,
  scss,
  yaml,
  python,
  java,
  powershell,
  ini,
  diff,
}))
  hljs.registerLanguage(name, language);

/** 按需載入；沒有登記文法的語言回傳 null。 */
export function highlightCode(language: string, source: string): string | null {
  if (!hljs.getLanguage(language)) return null;
  const html = hljs.highlight(source, { language, ignoreIllegals: true }).value;
  // highlight.js 會跳脫原始碼；白名單只保留 token 的 span，其他一律移除。
  return DOMPurify.sanitize(html, {
    ALLOWED_TAGS: ['span'],
    ALLOWED_ATTR: ['class'],
    RETURN_TRUSTED_TYPE: false,
  });
}
