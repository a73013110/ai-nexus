import { Injectable, inject } from '@angular/core';
import { ApiClient } from '@core/api/api-client';

/**
 * 其他系統的檔案（後端 RemoteFiles）：瀏覽器只拿得到憑證，內容由後端代為取回。
 * 縮圖、下載連結與閱讀器連結的網址用 `remoteFileContent`；這裡是需要讀回內容的呼叫（例如 PDF 交給 pdf.js）。
 */
@Injectable({ providedIn: 'root' })
export class RemoteFilesApi {
  private readonly api = inject(ApiClient);
  content = (ticket: string, signal?: AbortSignal) =>
    this.api.open('/api/v1/remote-files/content', { query: { ticket }, signal });
}
