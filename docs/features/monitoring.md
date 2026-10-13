# 即時營運監控

入口 `/admin/monitoring`，功能識別 `monitoring`。先回答「系統是否正常、哪裡有問題」，再查看在線人員、工作階段、操作動態、API、SQL／HTTP 周邊服務、程序資源與各模組子系統。

## 操作

- 主視覺一句話回答健康（正常／N 項需要注意／N 項嚴重問題）並列出問題；與周邊服務或子系統有關的問題可點開詳情。暫停、斷線或停用採集時改說明資料時間點，狀態穩定兩份快照才播報。
- 關鍵指標固定為最近 1 分鐘；走勢、周邊服務、熱門 API 與事件依 1／5／15 分鐘範圍。
- 周邊服務只連線有觀測的依賴，尚無觀測的收在下方；子系統卡片來自各模組，見 [監控子系統](../architecture/monitoring-sources.md)。點選列、卡片或工作階段看詳情。
- 在線人員可依功能、狀態篩選，搜尋名稱／帳號／IP／裝置，排序及分頁。趨勢可選請求速率、P95、5xx 或 HTTP 回應量，附鍵盤逐點檢視與數據表。
- 擁有 logs.query 可依 trace ID 從操作動態前往系統日誌。
- 支援暫停／恢復、重連、JSON 快照匯出、深淺色、375px（執行環境、無異常子系統與熱門 API 收合）、鍵盤及減少動態。背景分頁停止訂閱；退避重連保留有時間標記的舊快照，授權失效後清空。

## 身分與觀測定義

人由伺服器驗證的 UserId／AD 帳號識別。每個分頁生成隨機 UUID，與 UserId 組成工作階段 key。同一人兩個分頁算一位使用者、兩個工作階段，不能推算兩台電腦。每 25 秒報送白名單功能與 active／idle／background；兩分鐘未互動為 idle，預設 90 秒沒有心跳即離線。背景節流／睡眠可能暫時離線。登出移除此分頁，其他中斷依租期處理。

IP 只取 HttpContext.Connection.RemoteIpAddress，不自行相信 X-Forwarded-For；可能呈現代理位址。OS／瀏覽器由 User-Agent 推定，只作提示。沒有反向 DNS、AD 電腦查詢或端點掃描。

| 數據 | 定義 |
| --- | --- |
| API／錯誤 | /api/v1 已完成要求；4xx／5xx 為錯誤，使用者中止另計 |
| 處理中／串流 | 未結束的一般要求與伺服器實際回應 SSE 的長連線，排除監控訂閱 |
| 延遲／P95 | 非 SSE API 耗時；P95 是固定直方圖區間上界，無樣本或超過 600 秒顯示 — |
| HTTP 位元組 | stream／BodyWriter 實際讀寫的 body bytes，包含未結束串流的寫入；不保存 body |
| SQL | SqlClient CommandBefore／After／Error，涵蓋 EF、Dapper 與直接指令的次數、耗時、失敗與執行中數量，不讀 SQL／參數／結果 |
| HTTP 依賴 | 所有 IHttpClientFactory client 共用 handler，自發送至回應標頭，不含後續讀取／模型生成時間 |
| 程序資源 | 每 3 秒取樣：本程序占所有核心的 CPU、工作集、managed heap、GC 可用記憶體上限、threads、執行緒集區排隊、GC 次數與暫停比例、鎖競爭／秒 |
| 資源趨勢 | 與 API 趨勢對齊的 10 秒區間：CPU、GC 暫停取平均，記憶體取最後值，排隊取最大；無樣本為 null |

趨勢與速率使用已完成的 10 秒區間，每點附 5xx 次數與 P95；第一個區間完成前等待資料。窗口影響 API、依賴與事件，`recentTraffic` 固定為最近 1 分鐘；在線清單維持現況，工作階段數據從首次心跳累計。依賴狀態依實際呼叫判定，「尚無觀測」不等於正常或故障，沒有主動探測。連線開啟失敗及 SqlBulkCopy 不產生 Command 指標。

應用層觀測不包含網卡／TLS 封包、整台電腦流量、SQL Server 全站連線或傳輸 bytes、使用者螢幕、鍵盤內容、聊天／文件內容、查詢字串。基礎設施層指標需另設 exporter／collector，經受控 adapter 擴充。

## 健康判定

`health.status`：有嚴重問題為 `critical`，只有警告為 `attention`，沒有問題為 `healthy`；停用監控或尚無任何觀測為 `unobserved`。門檻是 [`MonitoringHealth`](../../backend/src/AiNexus.Features/Monitoring/MonitoringHealth.cs) 的常數：

| 代碼 | 等級 | 條件 |
| --- | --- | --- |
| `error_rate_high` | 警告／嚴重 | 最近 1 分鐘至少 20 次要求，錯誤率 > 5%（> 25% 嚴重） |
| `server_errors` | 警告 | 最近 1 分鐘有 5xx |
| `latency_high` | 警告 | 最近 1 分鐘至少 20 次要求，P95 > 3 秒 |
| `cpu_high`、`gc_pause_high`、`thread_pool_starvation` | 警告 | 最近 3 個資源區間都超過 CPU 85%、GC 暫停 10%、排隊 100 項 |
| `memory_high` | 警告 | 工作集 > GC 可用記憶體 90% |
| `dependency_failing`、`dependency_degraded` | 嚴重或警告、警告 | 依所選窗口；全部失敗且至少 3 次呼叫才算嚴重 |
| `subsystem_failing`、`subsystem_degraded`、`subsystem_unreadable` | 嚴重、警告、警告 | 子系統回報，或讀取失敗／逾時 |
| `sessions_dropped` | 警告 | 工作階段達上限而拒收 |

## 共用架構與上限

`AiNexus.Features/Monitoring` 分離契約（`MonitoringSnapshot` version 2）、固定詞彙、聚合、健康判定、採集與 endpoints。讀取、匯出與 SSE 都經 `MonitoringSnapshots`，同一窗口一秒內共用同一份快照；建構快照不查資料庫，子系統讀取在背景進行。RuntimeTrafficMiddleware 包住既有診斷／安全層，沿用身分、錯誤與稽核。TrafficCountingStream 量測實際 IO；TrafficHttpHandler 用 factory defaults 覆蓋所有模組；RuntimeSampler 在 SqlClient 共用邊界觀測。DependencyCatalog 依核准 host／port 或 SQL server／database 分類，只輸出服務名稱。

保留 15 分鐘，最多 91 個 10 秒 buckets、每 bucket 128 個 API aggregates、32 項依賴、120 筆事件，預設 2000 個工作階段。快照回傳最近 200 個並揭露略過數量；滿額拒收新工作階段，既有記錄繼續更新。SQL 執行中追蹤最多 1024 筆，十分鐘清理缺少 terminal event 的記錄。Heartbeat／採集不寫資料庫；特權快照、訂閱與匯出留下持久稽核。

目前只觀測單一 IIS worker，重啟重新採集；多執行個體需將 presence lease／聚合移至分散式 adapter。AiNexus.Runtime Meter 已加入既有 OpenTelemetry，啟用 Diagnostics.OtlpEnabled 可經既有 collector 匯出 API 次數、body bytes、duration histogram、in-flight 與工作階段 gauges；tags 不含人員／IP。

## 權限與設定

`monitoring` 功能預設只授予 administrators 群組，沒有遙測資料表。一般成員只能報送自己的 presence；監控讀取需明確 grant，admin 不隱含 monitoring。保留 CSRF／身分版本驗證。SSE 每十五秒以新 scope 重驗帳號與有效 grant，測試身分也驗來源管理員、期限與撤銷；每人最多三條，新連線取代最舊一條（`stream_superseded`）而非被拒，因為重新整理後舊連線要等伺服器或代理察覺斷線才釋放；被取代的視窗暫停、不重連。單條十分鐘後結束並立即重連以重驗 cookie；429 依 `Retry-After` 退避。SQL 不可用時暫停傳送並重連，安全錯誤不含原始例外。AsyncLocal suppression 排除觀測者自己的 HTTP／SQL／外部呼叫。

| 設定 | 預設 | 範圍 |
| --- | --- | --- |
| Monitoring.Enabled | true | false 停用採集／心跳記錄 |
| Monitoring.MaxSessions | 2000 | 100–10000 |
| Monitoring.SessionTimeoutSeconds | 90 | 60–300 |
| Monitoring.RefreshSeconds | 3 | 2–15 |

一般設定可覆寫，修改後重啟，不需秘密或 SQL server-wide monitoring 權限。Presence 每人每分鐘 120 次，監控讀取每分鐘 60 次，匯出每分鐘兩次。

SSE 的不緩衝與 heartbeat 沿用回答串流的做法，見 [SSE](../architecture/sse.md)。

## 驗證

單元與整合測試在各自的 Monitoring 資料夾。MonitoringBrowserTests 以真實 Kestrel、前端及 Edge 驗證心跳到即時快照與稽核；`frontend/e2e/monitoring.spec.ts` 以本機 SSE fixture 驗頁面互動、深淺色、375px、鍵盤與減少動畫。SQL／IIS／AD 與代理 IP 需依部署環境實機驗收。
