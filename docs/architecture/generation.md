# 生成與背景任務

- 聊天生成在程序內排程，以 SQL 保存狀態與租約；SSE 斷線不停止生成。
- 文件索引、評測、程式碼 review 等長工作走 SQL durable jobs（租約＋fenced checkpoint）。
- **每個 IIS app 只用一個 worker**：程序內鎖與模型容量不跨程序，原因與擴展條件見 [ADR 0001](../decisions/0001-single-iis-in-process-locks.md)。

## 聊天生成

- 建立 run 前驗證核准模型、群組配額、owner、思考能力與冪等 key，再於同一交易保存訊息、run、參數與第一個事件。
- Context 只略過本次送往模型的最舊完整輪次，不刪歷史。編輯新增分支，重新生成新增 assistant 兄弟節點。
- `GenerationWorker` 每 80 ms 或 512 字元保存部分文字與 replay 事件；SSE 中斷不停止生成，恢復時以 snapshot 與序號續接，契約見 [SSE](sse.md)。
- `GenerationRuns` 保存 ExecutorId 與 LeaseExpiresAt，執行中的 worker 定期續約；其他實例只接手已到期的租約，避免本機與 IIS 共用資料庫時互相中止。取消先更新 SQL，原 executor 續約時偵測並停止。主機時鐘要同步。
- 這不是全域持久佇列，也沒有跨程序的模型容量限制。

## 模型路由

- Google／Ollama adapter 以 keyed DI 註冊；`InferenceRouter` 依核准的 profile 路由並管理每個 provider 的容量，聊天佇列按 provider 分開，模型清單探測互相隔離。
- 排隊的 run 保存 provider 與原生模型；文字與圖片走同一條路由，**不自動 fallback** 到其他 provider。
- OCR、段落工具與評測共用 `ModelTaskService` 的核准、配額與用量；預留配額以使用者 SQL row lock 序列化，遠端呼叫不持有交易。規則見 [模型政策與配額](../features/model-policy.md)。

## Durable jobs

- `BackgroundJobWorker` claim 工作後取得 60 秒租約並持續 heartbeat；`LeaseToken` 做 fencing，過期的 worker 不能提交，已完成的項目在重試時重用。
- 停止先記取消要求；離開頁面不會取消。
- 外部 RPC 不保證跨程序 exactly-once：結果尚未保存的呼叫可能在重試時重做。
- 新工作實作 `IBackgroundJobHandler`，在遠端呼叫前後都驗證授權，以 checkpoint 保存結果；Job／Attempt 的日誌關聯自動帶入。
- 評測凍結題庫、指令與模型設定指紋，設定變更會阻擋執行與重試；來源文字以不可信資料封裝。

## 程序內鎖

- 同一程序內的互斥用 `KeyedAsyncLock` 依鍵分開：生成狀態依對話、資源寫入依資源，不同對話或資源互不等待。
- 每人一個進行中的生成與配額由 `Users` 列鎖在交易內保證（filtered unique index 再擋一次）。
- Gitea 的連線、解除與匯入由本機鎖協調，固定 commit 的成功匯入重用既有文件。

## 費用

- 每次呼叫以 run／invocation ID 建立唯一的 `ModelCharge`，預約時凍結有效價格；串流用量與結束狀態都經 `BillingService` 更新。
- 價格是追加版本，歷史不重算；取消、失敗與缺少 usage 時保留「未知費用」，不填零。
- 報表依幣別與 API／內部成本分組，在資料庫以參數化日期區間彙總；隱藏模型名稱的政策同時套用在對話與費用報表。見 [費用](../features/billing.md)。

## 外部連線

- 網路搜尋在送出前才對公開提問查詢，冪等 key 與配額在 SQL 協調；來源是有限摘要，以不可信資料封裝。
- Gitea 不用共用 token：每人以 Data Protection 加密保存自己的 token，保護目的綁定登入者與伺服器 URL。
- 兩種 provider 都限制回應大小、停用重新導向與預設的 request logging，避免 credential 或 query 外洩。外部呼叫不持有 SQL 交易。
