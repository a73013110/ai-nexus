# 錯誤回應、查證代碼與遮罩

- 對外錯誤只用固定的公開提示、分類與伺服器查證代碼；例外訊息、SQL、供應商原始 body 不進回應、SSE、工作或通知。
- 日誌只保存核准的 metadata；所有例外的自由 Message 一律省略，特權診斷也只看得到型別、代碼與無路徑堆疊。
- 寫法（`Result<T>`、`<Module>Errors`、`ExternalServiceException`）見 [後端撰寫慣例](BACKEND_CONVENTIONS.md#錯誤)；日誌管線見 [診斷日誌架構](DIAGNOSTICS.md)。

## 錯誤回應

一般 API 回 `application/problem+json`：

```json
{
  "type": "urn:ai-nexus:problem:service_unavailable",
  "title": "操作未完成，請聯絡管理員。查證代碼：NX-0123456789ABCDEF0123456789ABCDEF",
  "status": 503,
  "code": "service_unavailable",
  "issueCode": "NX-0123456789ABCDEF0123456789ABCDEF"
}
```

- 查證代碼由伺服器 CSPRNG 產生（`NX-`＋32 個十六進位字元），不是權限憑證；一般使用者不能用它查管理 API。
- 4xx 的提示、狀態碼與重試分類來自固定的 `PublicErrorCatalog`；前後端 catalog 一致由 `DiagnosticTests` 檢查。
- SSE 已開始時改送安全的 `event: error`（`code`、`message`、`issueCode`），durable run 的 `status`／`snapshot` 事件同樣帶 issueCode，格式見 [SSE](SSE.md)。已開始的非 SSE 二進位回應失敗時中止連線，狀態碼無法再改寫。

## 前端

- Angular `ApiError` 不信任伺服器的任意 title，依公開 catalog 與有效 IssueCode 顯示提示並提供複製；頁面、toast、附件／工作詳情、通知與聊天共用同一政策。
- 本機格式／容量檢查用 `ClientValidationError` 的固定提示；預期的 AbortError 不當成故障。
- 沒有有效伺服器代碼時顯示 `LOCAL-`＋16 個十六進位字元，明確標示「尚未記入伺服器」。
- 未處理的 exception 與 Promise rejection 經 `/api/v1/client-issues` 回報，只送 `kind` 與粗粒度 SHA-256 fingerprint，不送 message、stack、URL、DOM。伺服器端 body ≤2 KiB、每人每分鐘 10 次、去重快取；前端每分鐘最多 5 次。事件標記 `UntrustedClient=true`；`accepted=true` 只代表佇列接受。

## 流程關聯

- HTTP middleware 為每個 request 建立自己的 W3C server Activity、RequestId 與 OperationId，不繼承外部 `traceparent`、request ID 或前端宣稱的 UserId。UserId 只來自伺服器驗證的使用者；route tag 只用路由範本。
- Job／Run 入列時保存 TraceId、ParentSpanId、OperationId。worker 建立新的 Consumer Activity（JobId、RunId、Attempt），清掉 request scope。重試與重啟維持同一 TraceId／JobId，每次失敗有不同 IssueCode。
- 同一例外跨層拋出時，`Issues.Report` 在例外 Data 記下已分配的代碼，只記一次；呼叫端不要先記 Error 再重新拋出。

## 遮罩與注入防護

- 不記：密碼、Cookie、Authorization、API key／token、連線字串、完整 body／query、聊天與文件內容、OCR、模型輸入輸出。完整 URL、email、控制字元與可辨識的 credential assignment 會遮罩。
- 例外只保留型別、受控錯誤代碼、`SqlException` Number／State／Class、無檔案路徑的 method frames（最多 6 層、每層 30 frames）。可診斷性要靠 typed code，而不是例外文字。
- 允許的屬性名在 `DiagnosticRedactor.Fields`；只接受 ID、有限數值、布林、受控 enum 與核准字串，其他物件換成 `[OBJECT OMITTED]`，不呼叫 `ToString()`。截斷先於 regex，補送時再套一次白名單與上限。
- 稽核另有核准的 JSON schema（`AuditRedactor`）：可存授權相關的 before／after，不存密碼 hash、token、模型或文件內容；無法安全解析的稽核讓操作失敗，歷史內容讀取時再遮罩。
- 檔案用 JSON encoder 並移除 CR／LF 與控制字元；管理頁以 Angular 插值與 `<pre>` 呈現。CSV 只匯出摘要欄位，前導 `= + - @` 加單引號，不含 ExceptionDetail、PropertiesJson、UserId。
- 秘密偵測不保證涵蓋所有未知格式：模組不得用動態內容組模板，新增白名單欄位或例外分類要做隱私審查。
