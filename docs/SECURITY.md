# 網站與原檔安全

錯誤回應、SSE、工作與通知禁止回傳 Exception.Message／SQL／provider 原始 body；共用安全契約提供固定提示、公開分類與伺服器查證代碼。管理日誌也只能看到遮罩後的型別、代碼與無路徑堆疊。日誌 query／detail／export 分開授權、特權讀取先保存稽核，CSV 防公式注入。完整白名單、前端不可信回報、保存與防竄改能力限制見 [DIAGNOSTICS](DIAGNOSTICS.md)。

安全政策集中於 `AiNexus.Platform/Security/WebSecurity.cs`。預設本機與正式網站使用 HTTPS；HTTPS 回應的 Session／Antiforgery Cookie 是 `Secure`、`HttpOnly`、`SameSite=Strict`。正式環境使用 HSTS；IIS 憑證、HTTPS binding、反向代理信任與 AD TLS 必須依 [部署文件](../deploy/iis/README.md) 驗收。

`Start-Local`／`Start-Dev` 只有明確 `-Http` 才開啟本機例外，且僅在 Development、Host 與來源 IP 同時是 loopback 時允許。Testing 的 HTTP Cookie 政策只供獨立測試 host；Production 不接受停用 HTTPS 的舊參數。

## 回應與瀏覽器邊界

- API 在生成 antiforgery token 前及回應開始前使用一致的 `Cache-Control: no-cache, no-store`、`Pragma: no-cache`、`Expires: 0`，涵蓋登入、錯誤、JSON、檔案與 SSE。Antiforgery 不需再覆寫不完整的快取政策；保留原本 CSRF 檢查及警告層級。原始碼政策見 [ASP.NET Core Antiforgery](https://source.dot.net/Microsoft.AspNetCore.Antiforgery/Internal/DefaultAntiforgery.cs.html)。
- CSP 限制 script、connect、font、worker 到同源；image 限同源與本機建立的 `blob:`（Mermaid 清理後的 SVG 圖片），禁用 object、跨來源表單與任何 framing。Angular 動態樣式需要 `style-src 'unsafe-inline'`；script 沒有允許 inline 或 eval。另有 nosniff、DENY、no-referrer、COOP／CORP same-origin、停用不使用的 camera／microphone／geolocation／payment／USB，以及移除 Kestrel Server 標頭。
- 原檔必須經 owner 或知識／專案／分享 ACL。只有 PDF／PNG／JPEG／WebP 可 inline 顯示；文字、XML、Markdown、Word 等作為附件下載。原檔回應使用獨立 `default-src 'none'; sandbox` CSP，防止上傳內容取得網站的執行能力。檔名不成為檔案系統路徑，binary 不在 wwwroot。
- 上傳沿用格式 allowlist、標記驗證、大小／頁數／擷取上限、owner quota 及寫入鎖；拒絕 SVG 與巨集文件。讀檔、文件頁面、背景任務及檔案庫關聯均重新驗證權限。
- Markdown 包含串流尾段，先關閉 raw HTML，再通過同一個 DOMPurify allowlist。外部連結僅 HTTP(S)，帶 noopener／noreferrer；圖片語法只顯示說明文字，不發起任意外部請求。
- Mermaid 只接受完成的程式碼區塊。渲染使用 strict 模式與站點固定的設定；圖表圖片節點在量測前拒絕。輸出的 SVG 移除 active HTML／事件／圖片／連結及外部 CSS resource；清理完成後在隱藏量測容器確認完整圖形邊界，容器於 finally 移除。閱讀畫面只透過惰性 Blob 圖片呈現，不啟用圖表的 click handler；下載沿用同一份清理與邊界修正後的 SVG。
- 成本會被單一使用者放大的端點有每人每分鐘上限：送出（含重新生成、編輯與網路搜尋）30、上傳 30、前端錯誤回報 10、日誌查詢 60、匯出 2、presence 120；AD 登入另依來源 IP 每分鐘 10 次。超過回 429 `rate_limited` 與 `Retry-After`。分組依登入 session 的使用者或 Windows SID，未登入才依來源 IP（`Identity/UserRateLimits.cs`）。網路搜尋另有每日配額，生成另有每人一個進行中與佇列上限。
- 登入／登出活動稽核保存語意結果，失敗帳號保持未驗證，不歸到既有 cookie 身分；密碼、token 與完整請求不進入稽核。帳號、登入方式及來源 IP 使用核準的欄位與長度；來源只取 server connection，不信任任意 forwarded header。稽核與對外查證代碼、HTTP trace 可互查，但兩個查詢入口各自重新驗證原有權限。見 [管理者查證流程](DIAGNOSTICS.md)。

以上是應用程式基線，依 [Microsoft HTTPS](https://learn.microsoft.com/en-us/aspnet/core/security/enforcing-ssl?view=aspnetcore-10.0)、[OWASP 安全標頭](https://cheatsheetseries.owasp.org/cheatsheets/HTTP_Headers_Cheat_Sheet.html) 與 [檔案上傳](https://cheatsheetseries.owasp.org/cheatsheets/File_Upload_Cheat_Sheet.html) 建議維護。正式憑證、網路入口、SQL／AD 權限、持續更新及備份還原仍屬部署與維運驗收；單次程式調整不構成全面安全認證。
