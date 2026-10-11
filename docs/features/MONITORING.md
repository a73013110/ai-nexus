# 即時營運監控

入口 `/admin/monitoring`，功能識別 `monitoring`。查看此應用程式的在線人員、工作階段、操作動態、API、SQL／HTTP 周邊服務與程序資源。設計參考 [Grafana Node graph](https://grafana.com/docs/grafana/latest/visualizations/panels-visualizations/visualizations/node-graph/) 與 [Datadog Service Map](https://docs.datadoghq.com/tracing/services/services_map/)，沿用現有主題、tokens、SVG 圖示、趨勢圖、選單、詳情抽屜和 SSE parser。

## 操作

- 切換最近 1／5／15 分鐘，查看速率、錯誤、平均與 P95 回應時間。
- 點選拓樸的人員／服務節點查看詳情。拓樸最多顯示六位人員與六項依賴，可展開全部周邊服務清單；工作階段清單在下方。
- 清單可搜尋名稱／帳號／IP／裝置，篩選功能與狀態、排序及分頁；功能分布可直接篩選清單。
- 趨勢可選 API 速率、平均延遲、HTTP 回應 KB／秒或錯誤比例，附鍵盤逐點檢視與數據表。
- 異動、錯誤與中止事件呈現在時間軸；擁有 logs.query 可依伺服器 trace ID 前往系統日誌。
- 支援暫停／恢復、重連、JSON 快照匯出、深淺色、375px、鍵盤及減少動畫。背景分頁停止監控訂閱；退避重連保留有時間標記的舊快照，授權失效後清空資料。

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
| 程序資源 | 本程序占所有核心的 CPU 比例、工作集、managed heap、threads 與運作時間 |

趨勢與速率使用已完成的 10 秒區間；第一個區間完成前等待資料。窗口影響 API、依賴與事件；在線清單維持現況，工作階段數據從首次心跳累計。依賴狀態依實際呼叫判定，「尚無觀測」不等於正常或故障，沒有主動探測。連線開啟失敗及 SqlBulkCopy 不產生 Command 指標。

應用層觀測不包含網卡／TLS 封包、整台電腦流量、SQL Server 全站連線或傳輸 bytes、使用者螢幕、鍵盤內容、聊天／文件內容、查詢字串。基礎設施層指標需另設 exporter／collector，經受控 adapter 擴充。

## 共用架構與上限

`AiNexus.Features/Monitoring` 分離契約、固定詞彙、聚合、採集與 endpoints。RuntimeTrafficMiddleware 包住既有診斷／安全層，沿用身分、錯誤與稽核。TrafficCountingStream 量測實際 IO；TrafficHttpHandler 用 factory defaults 覆蓋所有模組；RuntimeSampler 在 SqlClient 共用邊界觀測。DependencyCatalog 依核准 host／port 或 SQL server／database 分類，只輸出服務名稱。

保留 15 分鐘，最多 91 個 10 秒 buckets、每 bucket 128 個 API aggregates、32 項依賴、120 筆事件，預設 2000 個工作階段。快照回傳最近 200 個並揭露略過數量；滿額拒收新工作階段，既有記錄繼續更新。SQL 執行中追蹤最多 1024 筆，十分鐘清理缺少 terminal event 的記錄。Heartbeat／採集不寫資料庫；特權快照、訂閱與匯出留下持久稽核。

目前符合單一 IIS worker 部署，不聚合叢集，重啟重新採集。多執行個體可沿用契約與採集邊界，將 presence lease／聚合移至分散式 adapter。AiNexus.Runtime Meter 已加入既有 OpenTelemetry，啟用 Diagnostics.OtlpEnabled 可經既有 collector 匯出 API 次數、body bytes、duration histogram、in-flight 與工作階段 gauges；tags 不含人員／IP。

## 權限與設定

`monitoring` 功能預設只授予 administrators 群組，沒有遙測資料表。一般成員只能報送自己的 presence；監控讀取需明確 grant，admin 不隱含 monitoring。保留 CSRF／身分版本驗證。SSE 每十五秒以新 scope 重驗帳號與有效 grant，測試身分也驗來源管理員、期限與撤銷；每人最多三條，單條十分鐘後重連重新驗 cookie。SQL 不可用時暫停傳送並重連，安全錯誤不含原始例外。AsyncLocal suppression 排除觀測者自己的 HTTP／SQL／外部呼叫。

| 設定 | 預設 | 範圍 |
| --- | --- | --- |
| Monitoring.Enabled | true | false 停用採集／心跳記錄 |
| Monitoring.MaxSessions | 2000 | 100–10000 |
| Monitoring.SessionTimeoutSeconds | 90 | 60–300 |
| Monitoring.RefreshSeconds | 3 | 2–15 |

一般設定可覆寫，修改後重啟，不需新增秘密或 SQL server-wide monitoring 權限。Presence 每人每分鐘 120 次，監控讀取每分鐘 60 次，匯出每分鐘兩次。

SSE 的不緩衝與 heartbeat 沿用回答串流的做法，見 [SSE](../architecture/SSE.md)。

## 驗證

單元與整合測試的 Monitoring 資料夾以隔離 SQLite 驗證權限／CSRF、撤權中止訂閱、生命週期／容量、人數與分頁、實際 payload 與進行中串流計數、直方圖與 SSE 延遲排除、共用 HTTP handler 的失敗／中止及觀測者排除、SQL metadata 遮罩、匯出稽核和停用模式。MonitoringBrowserTests 透過真實 Kestrel、前端及 Edge 驗證瀏覽器心跳、對話操作、即時快照、AD 身分提示、詳情與稽核。`frontend/e2e/monitoring.spec.ts` 以本機 SSE fixture 驗持續更新、篩選、全部服務清單、抽屜、range、匯出、暫停／重連、深淺色、375px、鍵盤與減少動畫。畫面為測試資料；SQL／IIS／AD 與代理 IP 需依部署環境實機驗收。
