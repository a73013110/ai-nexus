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
    (title.replace(/[\\/:*?"<>|\u0000-\u001f]/g, '_').slice(0, 80) || 'AI-Nexus') + '.' + extension;
  link.click();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}
