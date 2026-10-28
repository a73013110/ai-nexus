# 匯出檔案

匯出一律用 Platform 的共用元件（`backend/src/AiNexus.Platform/Export/`），模組只決定資料、檔名與提供哪些格式；排版、安全防護與 Content-Type 寫在一處。

| 內容 | 格式 | 元件 |
|---|---|---|
| 表格 | CSV | `Csv.Write(標題, 列)`：UTF-8 BOM、公式注入防護 |
| 表格 | Excel | `Spreadsheet.Write(SpreadsheetSheet…)`：凍結標題、篩選、列印設定 |
| Markdown 文件 | Word | `MarkdownDocument.Docx(標題, 內文)` |
| Markdown 文件 | PDF | `MarkdownDocument.Html(…)` → `PdfRenderer.RenderAsync(html)` |

## 端點寫法

- handler 回傳 `Result<ExportFile>`，端點寫 `.ToHttpResultAsync(file => file.ToHttpResult())`。
- 不要自己設 `Cache-Control`：`/api` 回應在送出前一律改寫為不快取（[ADR 0006](../decisions/0006-api-no-store.md)）。
- 匯出是把資料帶離系統：加 `.Audited(...)`、`.RequireRateLimiting(...)`，筆數超過上限時拒絕而不是截斷（見稽核、日誌匯出）。
- 要在檔案送出前再檢查權限的（例如成果的 PDF 渲染較慢），在 handler 渲染後重新讀取一次。

## 新增格式或版面

- 表格要出 PDF 時，把表格轉成 HTML 交給 `PrintPage.Html(標題, 小字, 內文)`，再給 `PdfRenderer`；不要另寫列印樣式，PDF 版面才會一致。
- 格式固定、版面與舊系統一模一樣的檔案（例如出勤報表）可以自己寫，但字元清理仍用共用元件（`Spreadsheet.Clean`）。
- 不引入另一套 PDF 引擎，原因見 [ADR 0025](../decisions/0025-pdf-via-installed-browser.md)。

## PDF 的部署條件

- 主機要安裝 `Export.Pdf.Browser` 指定的 Edge（`msedge`，預設）或 Chrome（`chrome`），從標準安裝位置尋找；裝在別處時用 `Export.Pdf.BrowserExecutablePath` 指定絕對路徑。另需涵蓋繁體中文的字型（Windows 為 Microsoft JhengHei）。
- 找不到瀏覽器或啟動失敗時回 `pdf_renderer_unavailable`，其他格式不受影響；網站照常啟動。
- 同時最多兩份 PDF，超過立即回 `pdf_export_busy`；單份上限 `Export.Pdf.TimeoutSeconds`。
- 頁面關閉 JavaScript、攔截全部網路請求，列印頁另有 CSP；Markdown 的原始 HTML 一律視為文字。
