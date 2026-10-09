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

/** Loaded on demand; returns null for languages without a registered grammar. */
export function highlightCode(language: string, source: string): string | null {
  if (!hljs.getLanguage(language)) return null;
  const html = hljs.highlight(source, { language, ignoreIllegals: true }).value;
  // highlight.js escapes the source; the allowlist keeps the token spans and nothing else.
  return DOMPurify.sanitize(html, {
    ALLOWED_TAGS: ['span'],
    ALLOWED_ATTR: ['class'],
    RETURN_TRUSTED_TYPE: false,
  });
}
