/**
 * 以程式開啟外部網址（Ctrl+K 等沒有 `<a>` 可點的地方），行為與選單裡的外部連結相同：
 * 一律 `rel="noopener noreferrer"`，目的網站拿不到 `window.opener`，也收不到本站網址當 Referer；
 * `newTab` 決定是否 `target="_blank"`。用暫時的 `<a>` 而不是 `window.open`，
 * 是因為同分頁開啟時 `location.assign` 無法不送 Referer。
 */
export function openExternal(url: string, newTab: boolean) {
  const link = document.createElement('a');
  link.href = url;
  link.rel = 'noopener noreferrer';
  if (newTab) link.target = '_blank';
  link.click();
}
