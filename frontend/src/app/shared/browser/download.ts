export function downloadFile(content: string, title: string, extension: 'md' | 'json') {
  const url = URL.createObjectURL(
    new Blob([content], {
      type: extension === 'md' ? 'text/markdown;charset=utf-8' : 'application/json;charset=utf-8',
    }),
  );
  const link = document.createElement('a');
  link.href = url;
  link.download =
    (title.replace(/[\\/:*?"<>|\u0000-\u001f]/g, '_').slice(0, 80) || 'AI-Nexus') + '.' + extension;
  link.click();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}
