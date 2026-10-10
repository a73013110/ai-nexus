# 系統日誌驗收與實測紀錄

驗證日期：2026-10-07（Asia/Taipei）。本次修改涵蓋共用後端、管理頁、安全錯誤邊界、migration、契約、外部設定、封裝與維運文件。沒有操作正式 IIS、正式資料庫、SQL Server 升級或部署；Git index 未變更。

## 執行環境

Windows 10.0.26200 x64；AMD Ryzen 7 PRO 5850U（16 個 logical processors），實體記憶體約 27.83 GiB。.NET SDK 10.0.401／runtime 10.0.12、Angular 22.2.1、TypeScript 6.0.2、Node 26.5.0、npm 11.6.1；Microsoft Edge 154.0.4258.62。後端採現有 xUnit／WebApplicationFactory；前端採現有 Vitest／Playwright。測試用 SQLite、身分及模型替代僅在 test assembly；產品沒有偽造使用者 header 或驗收用故障 endpoint。

## 驗證結果

| 項目 | 結果／證據 |
| --- | --- |
| Release 前後端 build／publish | 通過；`artifacts/verification`，Angular initial 490.60 kB／transfer 118.76 kB；系統日誌 lazy chunk 18.99 kB／5.47 kB，符合現有 bundle budget |
| 後端完整回歸（不含 Browser／Performance） | **317 通過、4 略過、0 失敗**，共 321；`artifacts/test-results/backend.trx`，5 分 35 秒 |
| 日誌、隱私及故障專項 | **26 通過**；包含在上述回歸，另有 `artifacts/test-results/diagnostics.trx`，不能重複相加當成更多測試 |
| 前端單元測試 | **51 通過**，20 個檔案；固定提示、原始例外隱私、本機代碼、複製、取消及受控回報去重均涵蓋 |
| 編譯 Angular＋Kestrel 真實查證流程 | **1 通過**，Release；`artifacts/test-results/diagnostic-browser.trx`，14 秒；下節列出實際代碼與畫面證據 |
| Playwright UI 完整回歸 | **128 通過、0 失敗**，4.5 分鐘；`artifacts/browser-results.json`；包含既有 127 項及新增全域提示／回報／手機版面檢查 |
| Release 效能專項 | **1 通過**；`artifacts/test-results/diagnostic-performance.trx`、`artifacts/diagnostics-performance.json` |
| 設定／腳本檢查 | 通過：v3 遷移、秘密分離、原 ACL、冪等、擴充欄位、範本順序及全部 PowerShell 語法 |
| EF model／契約 | 沒有 pending model changes；idempotent SQL／描述腳本、OpenAPI 及前端型別已更新；測試核對診斷契約及 252 個固定公開提示一致 |
| Repository 檢查 | `Test-Repository.ps1 -WorkingTree` 通過：追蹤及未忽略新檔、不改 index；範本無非空秘密，未偵測 live key／本機密碼；仍需正常 code review |

4 個真實 SQL Server 2025 測試因未設定 `AINEXUS_SQLSERVER_TEST` 而略過，分別為中文全文、30053 fallback、native vector、診斷 SqlBulkCopy／去重／獨立交易／保留清理。測試會建立並刪除自己的隨機資料庫，需要**另行提供且明確授權的測試 SQL instance**與建庫權限；本次沒有改用 `.local` 或正式連線。SQLite 回歸、DDL／snapshot／SQL 腳本檢查不能證明 SQL Server 2025 的實機 migration、索引執行計畫或容量。

## 模組盤點與處理

所有模組共用 ILogger provider 和 HTTP 安全邊界；任意例外 Message 不作一般回應。以下列出各流程的關聯及落點。

| 範圍 | 已整合的行為 |
| --- | --- |
| Identity／AccessControl／登入 | 固定驗證提示、401／403／CSRF／限流安全契約，伺服器產生關聯與查證代碼；原管理／安全 audit 寫入及讀取遮罩 |
| API／框架 binding／授權 | route template、HTTP status、耗時、RequestId／OperationId；不信任外部 traceparent；拒絕事件 Information，取消不當 Error |
| SQL／Dapper／EF | 最外層記 typed exception／SQL number；SQL 日誌 sink 獨立交易、有限逾時、批次；diagnostic query／cleanup suppression 防遞迴 |
| Conversations／Inference／模型目錄 | Run／Message／RunEvent 的 IssueCode；queue 持久 parent；獨立 Consumer Activity；模型探測失敗的安全 notice 與 Warning |
| SSE／生成中途失敗 | 安全 error／status／snapshot，無原始 provider 回應；已開始的非 SSE 回應中止連線；前端保留永久 4xx 不重試與重連行為 |
| Attachments／OCR／Files／Library | 同一 API／job 邊界與 JobId；檔名／容量固定驗證提示；不記原檔、OCR 文字、完整 body 或 query |
| Knowledge／全文／向量／rewrite／rerank | 30010／30046／30053 hybrid 降為 vector 的可查 Warning／typed reason／原實際模式／SQL number／NX／Trace；重試與其他降級同樣留事件 |
| Operations／背景工作 | JobId／Attempt／Trace／Operation 持久保存；重試維持流程、每次問題獨立 NX；清除 request scope，安全 job 詳情與終止通知 |
| Integrations／WebSearch／Repositories | 共用錯誤分類、失敗 job／模型生成關聯；catch 後降級與重試留 Warning；不記連線秘密、原始錯誤 body 或查詢／文件文字 |
| Notifications／Sharing | 新失敗通知保存 NX；API 與前端都遮住既有自由 ErrorMessage；分享／複製失敗訊息保留安全狀態與代碼 |
| Artifacts／匯出／Projects／Quality／Billing | 共用 API／job／run 邊界；衝突與格式固定提示保留，草稿與重試流程回歸；管理匯出限制、CSV 公式防護、audit |
| 啟動／關機／options | DI 前啟動失敗嘗試 Critical 站外保存；服務 started／stopping；有限 shutdown flush、未完成計數；主機入口之前的故障需查原生日誌 |
| Angular 未處理例外／Promise rejection | 有界、限流、粗粒度去重，只送 kind／fingerprint；回應有效 NX 才顯示已回報代碼；離線明確 LOCAL；取消與固定本機驗證不送系統故障 |

## 故障與資安證據

日誌測試實際操作暫存目錄、輪替、checksum、cursor 與 SQLite，並模擬 store outage／程序重新開啟 journal；驗證 commit 後 checkpoint 遺失重播不重複匯入、未補送檔不清理、損壞及封存截斷可見計數、兩個 live journal 共用容量，以及已 Dispose 的 journal 不重開鎖。無寫入權限／磁碟 IO 失敗由注入 journal 實作拋出，並另外驗證實際不合法／不可作目錄的路徑與容量不足；沒有對正式 NTFS ACL 改權限，也沒有實際填滿主機磁碟或強制殺 IIS。

SQL 30053 測試注入 SqlClient typed exception 到 `ISqlDatabase<NexusDbContext>`，走產品的 SqlServerRetrievalStore／RetrievalPipeline／HTTP／journal／query store：HTTP 200、實際 mode vector，管理持久表仍有 Warning、30053、hybrid／vector、NX、RequestId 與 TraceId。這證明應用 fallback 分支；沒有重現正式 SQL 引擎故障或證明 CU 修復效果。

權限測試涵蓋未登入、一般使用者、只有 query，以及撤銷 detail／export 的獨立授權；cursor 綁 actor／filter／固定時間且無重複。高權限讀取先保存 audit，格式不合法的稽核 fail closed。例外／巢狀例外、API、SSE、通知、既有 audit 與管理細節皆驗證不含測試秘密；任意物件不會被序列化或執行 ToString。管理表格的 HTML payload 被呈現為文字，匯出前導公式加單引號。

選配 OTLP 使用真正 loopback Kestrel receiver 驗證 HTTP protobuf 原始 TraceId／SpanId／UTC timestamp 與遮罩內容；不可達 exporter 測試確認失敗計數及檔案主保存持續。此項不代表 collector 的 TLS、權限、持久保存或 trace／metric buffer 在正式環境已驗收。

## 一次真實端到端驗收

使用已編譯 Angular、loopback Kestrel、隔離 SQLite、test assembly 的登入身分及故障模型：

1. 在 `/chat` 送出合成驗收文字，模型故障經生成邊界記錄；前端只顯示安全提示。
2. 點「複製問題查證代碼」，實際讀剪貼簿核對：`NX-6798EE6931E8204A436D895CC681CA4A`。
3. 以有 query／detail grant 的測試管理員到 `/admin/logs`，輸入代碼；查到 LogId `8cfc3a97-72ad-403c-80c8-06f558c87b80`。
4. 詳情有 `System.Net.Http.HttpRequestException`、`provider_connection_lost`、省略的原始 Message、無檔案路徑堆疊；TraceId `3a3bfac8dd894596c31354d2acf3f9d8`、RunId `436b83a6-fa7e-4713-ad64-ce13cd0fe546` 與相關事件相符。
5. 驗證 light／dark／375px、載入／空資料／安全查詢失敗／降級狀態、XSS 與實際 CSV 下載；沒有 page error。詳情讀取 audit 已保存。

本次資料庫會隨 test fixture 清理，代碼是本次驗收證據，不能拿到別的環境查詢。下一次重跑會生成新的代碼；`artifacts/diagnostic-acceptance/result.json` 保存當次真實對應。

畫面：[安全聊天錯誤](../artifacts/diagnostic-acceptance/chat-safe-error.png)、[管理診斷](../artifacts/diagnostic-acceptance/admin-masked-detail.png)、[手機管理頁](../artifacts/diagnostic-acceptance/admin-mobile.png)、[安全查詢失敗與降級](../artifacts/diagnostic-acceptance/query-failure-and-degraded.png)。資料：[驗收結果](../artifacts/diagnostic-acceptance/result.json)、[CSV](../artifacts/diagnostic-acceptance/safe-export.csv)。artifacts 不加入 Git。

## 效能實測

2026-10-07 17:46（Asia/Taipei），Release／JIT optimization 開啟，當時沒有並行跑 build 或瀏覽器／回歸測試。兩輪各暖機 200 次，再發 4,000 次 `GET /health/live`，並行 8；ASP.NET TestServer in-process，查詢儲存 SQLite，採樣 1、OTLP 關、queue 8192／2048、batch 200、flush 10 ms。對照是在相同應用管線關閉 ILogger provider，仍有其他共用應用成本；量測包含 request latency，記憶體讀值在等待落盤／匯入後取得。

| 指標 | Provider 關閉對照 | 啟用日誌 |
| --- | ---: | ---: |
| Requests／秒 | 16,655.18 | 9,467.61 |
| p50／p95／p99（ms） | 0.3974／0.5357／2.1356 | 0.6564／2.9697／4.1276 |
| 全程序 GC allocated（MiB） | 152.40 | 481.28 |
| Managed memory 前／後（MiB） | 21.09／24.26 | 28.90／53.42 |
| Working set（MiB） | 170.98 | 210.21 |
| 已落盤／查詢表事件 | 0／0 | 4,203／4,203 |
| Lost | 0 | 0 |

這個小型 HTTP workload 下，日誌及持久處理有可量測成本：吞吐較對照低約 43%，p95 增加約 2.43 ms。GC allocated 是累計分配、包含背景寫入與匯入，不是常駐記憶體；same-process 兩輪 working set 受前一輪與 JIT／cache 影響，不能當作純 sink 的獨立 RSS。沒有使用真實 IIS 網路、正式 SQL Server、TLS、業務交易或長時間負載；不以這些數字宣稱正式容量、所有故障零遺失或 SLO 已達標。部署前依預期尖峰、內容大小、SQL／磁碟與實際保留期做專用環境容量測試。

原始數值與環境在 [diagnostics-performance.json](../artifacts/diagnostics-performance.json)。重跑方式：

```powershell
dotnet test --project backend/tests/AiNexus.Tests/AiNexus.Tests.csproj --no-restore -c Release `
  --filter 'Category=Performance' --report-xunit-trx --report-xunit-trx-filename diagnostic-performance.trx `
  --results-directory artifacts/test-results
./scripts/Verify.ps1 -Browser
```

## 尚需環境／政策驗收

真實 SQL Server 2025 migration／bulk／索引執行計畫，IIS app-pool NTFS ACL、Hosting Bundle／ANCM 啟動、Windows emergency source、回收／強制終止、正式尖峰及磁碟與 SQL 容量尚需授權的非正式環境驗收。主機崩潰、WAS、SQL ERRORLOG 等仍查原生日誌。

診斷 30 天、已補送檔案 14 天、稽核 365 天為可配置預設；組織的法定保存、legal hold、使用者識別、特權 grant、告警門檻、備份及外部 collector 邊界待確認。Checksum／一般 SQL 表不是不可竄改存放，也不能宣稱已符合所有資安政策。架構、故障操作與設定見 [DIAGNOSTICS](DIAGNOSTICS.md)；正式部署步驟見 [IIS](../deploy/iis/README.md)。
