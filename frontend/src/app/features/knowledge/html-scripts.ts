/** Matches `<script`, inline `on…=` handlers and `javascript:` URLs in an HTML document. */
const SCRIPT_PATTERNS = [
  /<script[\s>/]/i,
  /<[a-z][^>]*\son[a-z]+\s*=/i,
  /(?:href|src|action|formaction|xlink:href)\s*=\s*["']?\s*javascript:/i,
];

export function hasScripts(html: string): boolean {
  return SCRIPT_PATTERNS.some((pattern) => pattern.test(html));
}

export type ViewerKind = 'markdown' | 'html' | 'other';

export function richKind(fileName: string, contentType: string): ViewerKind {
  const type = contentType.toLowerCase(),
    name = fileName.toLowerCase();
  if (type.startsWith('text/markdown') || /\.(md|markdown)$/.test(name)) return 'markdown';
  if (type.startsWith('text/html') || /\.html?$/.test(name)) return 'html';
  return 'other';
}
