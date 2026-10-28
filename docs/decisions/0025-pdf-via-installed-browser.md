# 0025 PDF 以主機上的瀏覽器產生

- **狀態**：採用（2026-10-28）
- **實作**：Platform 的 `PdfRenderer`（PuppeteerSharp）與 `PrintPage`；寫法見 [匯出檔案](../architecture/exports.md)。
- **決定**：
  - PDF 由 HTML 列印頁產生，交給主機已安裝的 Edge／Chrome 無頭模式渲染；經 PuppeteerSharp 直接走 DevTools 協定。
  - 正式程式碼不引用 Microsoft.Playwright；它只留在後端瀏覽器測試專案。
  - 所有 PDF（文件、日後的表格報表）共用同一個列印頁與渲染器。

## 為什麼不用 Playwright

- Playwright 的 .NET 版透過 Node 驅動控制瀏覽器，套件會把約 100 MB 的 `node.exe` 與驅動程式複製進發布輸出；即使用的是主機的 Edge，也少不了它。
- PuppeteerSharp 是純 .NET 的 DevTools 用戶端，發布只多幾 MB；關閉 JavaScript、攔截網路、A4 與頁碼都能對應。

## 為什麼不用程式碼排版的 PDF 函式庫

- QuestPDF 等用 C# 排版，不需要瀏覽器，但版面要另寫一套，與網頁、Word 的樣式分開維護；QuestPDF 對營收超過門檻的組織要付費授權。
- 部署主機本來就以 Edge 作為使用者瀏覽器與後端瀏覽器測試的對象，不增加新的安裝條件。

## 何時重新評估

- 需要大量、格式固定的報表（對帳單、簽核單），或瀏覽器程序在 IIS 下的穩定性、資源成為問題時，評估改用程式碼排版的函式庫；`PdfRenderer` 的呼叫端只傳列印頁，替換時集中在 Platform。
