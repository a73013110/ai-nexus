const languages: Readonly<Record<string, string>> = {
  ts: 'typescript',
  tsx: 'typescript',
  mts: 'typescript',
  js: 'javascript',
  jsx: 'javascript',
  mjs: 'javascript',
  cjs: 'javascript',
  cs: 'csharp',
  csx: 'csharp',
  sql: 'sql',
  json: 'json',
  jsonc: 'json',
  sh: 'bash',
  bash: 'bash',
  html: 'xml',
  htm: 'xml',
  xml: 'xml',
  svg: 'xml',
  csproj: 'xml',
  props: 'xml',
  targets: 'xml',
  slnx: 'xml',
  resx: 'xml',
  config: 'xml',
  css: 'css',
  scss: 'scss',
  yml: 'yaml',
  yaml: 'yaml',
  py: 'python',
  java: 'java',
  ps1: 'powershell',
  psm1: 'powershell',
  psd1: 'powershell',
  ini: 'ini',
  toml: 'ini',
  editorconfig: 'ini',
  diff: 'diff',
  patch: 'diff',
};

/** 路徑對應的高亮文法；純文字則為空字串。 */
export function codeLanguage(path: string) {
  const name = path.slice(path.lastIndexOf('/') + 1).toLowerCase();
  const dot = name.lastIndexOf('.');
  return languages[dot >= 0 ? name.slice(dot + 1) : name] ?? '';
}

export const isMarkdown = (path: string) => /\.(?:md|markdown)$/i.test(path);
export const baseName = (path: string) => path.slice(path.lastIndexOf('/') + 1);
