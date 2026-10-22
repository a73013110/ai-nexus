export function downloadFile(content: string, title: string, extension: 'md' | 'json') {
  downloadBlob(
    new Blob([content], {
      type: extension === 'md' ? 'text/markdown;charset=utf-8' : 'application/json;charset=utf-8',
    }),
    title,
    extension,
  );
}
export function downloadBlob(content: Blob, title: string, extension: string) {
  const url = URL.createObjectURL(content);
  const link = document.createElement('a');
  link.href = url;
  link.download =
    // eslint-disable-next-line no-control-regex -- control characters are not valid in file names
    (title.replace(/[\\/:*?"<>|\u0000-\u001f]/g, '_').slice(0, 80) || 'ai-nexus') + '.' + extension;
  link.click();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}

/**
 * 檔案下載回應的檔名：優先 `Content-Disposition` 的 RFC 5987 `filename*`（UTF-8，中文檔名靠它），其次 `filename`；
 * 兩者都沒有或解不開時用 `fallback`。回傳值直接交給 `downloadBlob`（它會再清掉檔名不允許的字元）。
 */
export function attachmentName(
  response: Response,
  fallback: { title: string; extension: string },
): { title: string; extension: string } {
  const header = response.headers.get('Content-Disposition') ?? '';
  let name = '';
  const encoded = /filename\*\s*=\s*UTF-8''([^;\s]+)/i.exec(header)?.[1];
  if (encoded) {
    try {
      name = decodeURIComponent(encoded);
    } catch {
      name = '';
    }
  }
  name ||= /filename\s*=\s*(?:"([^"]*)"|([^;\s]+))/i.exec(header)?.slice(1).find(Boolean) ?? '';
  const dot = name.lastIndexOf('.');
  return dot > 0 && dot < name.length - 1
    ? { title: name.slice(0, dot), extension: name.slice(dot + 1) }
    : fallback;
}
