# 0034 Google 日曆同步：宣告式比對、冪等推送、自建 Google 客戶端

- **狀態**：採用（2026-11-07）
- **決定**：
  - 日曆同步是獨立模組 `Office/CalendarSync`，Google API 是共用的 `Platform/Google`（服務帳戶、權杖快取、各 API 的客戶端）。之後用到其他 Google API（Drive、Gmail）時在 Google 模組加客戶端，不另做一套驗證。
  - 每輪以 GD 的期望集合比對資料庫的列（宣告式），不靠「上一步成功」：漏掉的一輪、失敗的推送、手動刪掉的事件，下一輪都回到 GD 的狀態。比對是純函式（`CalendarSyncPlanner`），推送是另一步。
  - 推送冪等：事件 ID 由本地列 ID 決定（`Guid.ToString("N")` 符合 Google 的 base32hex 規則），建立遇 409 接手、更新遇 404 重建、刪除遇 404 算完成。
  - 不用 Google 官方 .NET SDK。只用到 events 的五個呼叫，自己寫 JWT（RS256）換權杖與 REST 呼叫，回應以 source-generated `JsonSerializerContext` 解析（ADR 0030）。
  - 對外 HTTP 共用 `AddControlledHttpClient`：`SocketsHttpHandler` 以 `PooledConnectionLifetime` 管理連線池，處理常式不再每兩分鐘輪替。權杖每個（範圍、委派帳號）快取一份，到期前 5 分鐘由單一請求換新。
- **原因**：
  - 舊系統逐筆指令式同步，任何一步失敗就留下重複或殘留的事件（以 UUID 查重、`NULL NOT IN` 讓刪除失效、手動刪掉的事件永遠卡住），且把退回結案當成有效。
  - SDK 帶進大量相依與反射式序列化，而我們只需要很小的一部分；權杖與錯誤分類自己做，才能把配額、憑證與權限問題分開處理（整輪停下或逐筆退避）。
  - 輪替處理常式會丟掉整個連線池、重新握手 TLS；高頻呼叫的服務（Google、模型供應商）共用長連線，DNS 變更靠連線存活上限生效。
- **取捨**：
  - 期間窗外的事件不再比對，舊單在 GD 被改動不會反映到日曆；換來每輪只讀最近一段資料。
  - 大量移除需要人工確認（安全閥），GD 資料異常時寧可晚刪也不清空日曆。
  - 開發與正式共用資料庫，只有一個站台能啟用；以 applock 保證同時只有一輪。
- **延伸**：[Google 日曆同步](../features/calendar-sync.md)、[外部資料庫](../architecture/external-databases.md)。
