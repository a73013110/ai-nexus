# 監控子系統

結論：要讓一個子系統出現在即時監控，就在自己的模組實作 `IMonitoringSource` 並以 `services.AddMonitoringSource<XxxMonitoring>()` 註冊。Monitoring 只認得 Platform 的介面，不依賴上層模組；前端以通用卡片顯示未知的子系統。頁面行為與健康判定見 [即時營運監控](../features/monitoring.md)。

## 寫一個來源

1. 在模組根目錄新增 `<Module>Monitoring.cs`（`internal sealed class`），實作 [`IMonitoringSource`](../../backend/src/AiNexus.Platform/Monitoring/IMonitoringSource.cs)：`Id`（穩定、kebab-case）、`Name`（繁體中文）、`Order`、`Refresh` 與 `ReadAsync`。
2. 回傳 [`SubsystemReading`](../../backend/src/AiNexus.Platform/Monitoring/SubsystemReading.cs)：狀態用 `SubsystemStates`，數字用 `SubsystemFact.Count`／`Bytes`／`Percent`／`Seconds`／`Tokens`，文字用 `Words`；未知值傳 `null`。
3. 狀態語意：`failing` 功能無法運作、`degraded` 部分受影響、`healthy` 正常運作中、`idle` 未使用或未啟用、`unobserved` 只表示讀不到。不要用 `unobserved` 表示「未啟用」，健康摘要會把讀取後的 `unobserved` 當成讀取失敗。
4. 範例：[`JobsMonitoring`](../../backend/src/AiNexus.Features/Jobs/JobsMonitoring.cs)。在 `MonitoringSnapshotTests` 確認來源讀得到。

## 讀取規則

- 只在有人讀快照時讀取（lazy）。`SubsystemReadings` 回傳快取，過期的來源另起一次背景讀取（single-flight），快照與 SSE 從不等待資料庫。
- 每次讀取有自己的 DI scope，不帶觀看者的 trace 與身分，並在 `MonitoringSuppression` 內執行，來源自己的 SQL 不計入依賴指標。
- 逾時 5 秒；例外或逾時顯示為 `unobserved`、摘要「無法讀取」，記一次 `monitoring.source_unavailable`（3006）。
- 應用程式停止時不再開始讀取，並等待進行中的讀取結束。
- 來源要便宜：記憶體狀態、設定或走索引的彙總查詢。系統範圍沒有 `ICurrentUser`；`Document` 沒有 query filter，要自行排除已刪除列。
- 來源觸發的共用背景工作若本來就該計入指標（例：`ModelCatalog` 的探測），以 `ExecutionContext.SuppressFlow()` 啟動，避免 suppression 隨 AsyncLocal 流入。

## 目前的來源

| Id | 模組 | 更新 | 資料 | 非 healthy 的條件 |
| --- | --- | --- | --- | --- |
| `generation` | Inference | 3 秒 | `GenerationScheduler` 記憶體計數 | 工作者未就緒 failing；佇列滿或排隊數 > 工作者 degraded |
| `model-providers` | Inference | 15 秒 | `ModelCatalog` 既有快取（10 秒） | 全部不可用 failing；部分不可用 degraded |
| `jobs` | Jobs | 30 秒 | `(Status, LeaseUntil, CreatedAt)` 索引 | 執行中租約過期或 1 小時內有失敗 degraded |
| `log-pipeline` | Diagnostics | 5 秒 | `DiagnosticHealth` 記憶體計數，日誌管線的完整指標（日誌頁只取 `Degraded`） | 有寫入失敗、遺失、損毀或待補寫 degraded |
| `knowledge-index` | Knowledge | 30 秒 | 文件狀態索引，只數未完成與失敗 | 有失敗文件 degraded |
| `usage-today` | Billing | 60 秒 | `UsageReports`，伺服器本地日，走 `CreatedAt` 覆蓋索引 | 失敗 ≥ 5 次且 > 20% degraded |
| `storage` | Attachments | 5 分鐘 | 檔案數與大小、附件磁碟可用空間 | 可用 < 10% degraded、< 3% failing |
| `integrations` | Integrations | 60 秒 | 只讀設定，不連線 | 已啟用但設定不完整 degraded |

## 成本

- 快照只組合快取資料：`MonitoringSnapshots` 讓同一窗口一秒內的讀取共用同一份快照，觀看人數不會放大成本。
- `RuntimeSampler` 每 3 秒取樣，彙整成 10 秒區間的固定環狀緩衝（最多 91 個）。
