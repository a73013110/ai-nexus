# 簽核

各種申請共用的審核：申請人送出後依流程逐關審核，每關任一位審核人核准即往下，任一關退回即結束。目前的申請種類是[儲存庫權限](repository-access.md)。設計與規則見 [ADR 0036](../decisions/0036-approval-engine.md)。

## 簽核頁（`flow.approvals`，全員）

- **待我審核**：輪到自己的申請。點開看申請內容與整條流程，填意見後核准或退回；退回時建議寫原因，申請人會收到。
- **我的申請**：自己送出的申請與進度；還沒審完前可以撤回。
- **我審過的**：自己核准或退回過的申請。
- **全部**：只有持有 `flow.approvals.manage` 的人看得到。這些人也能審任何一關（記為代審），並在規則算不出審核人時接手。
- 通知與郵件的連結帶 `?instance=`，開啟就是那份申請。

畫面上的關卡會註記：條件不符而略過、前一關已由同一人核准而自動通過、沒有審核人而改由簽核管理員。

## 流程設定（`flow.definitions`）

每種申請一條流程，關卡依序進行（可拖曳排序，最多 20 關）。每關設定：

- **審核人**：申請人的主管（往上幾層，沒有主管的單位跳過）、指定人員、持有某功能的人，或種類自己的規則（例如儲存庫負責人）。
- **何時需要**：每次都要，或申請內容符合某個值時（例如權限是「寫入」時才需要主管）。

修改只影響之後送出的申請；審核中的申請照送出時的流程走完。同時有人修改時，後儲存的人會被擋下並重新讀取。

## 期限與提醒

區段 `Flow`（[`FlowOptions`](../../backend/src/AiNexus.Features/Office/Flow/FlowOptions.cs)）：

- `ExpireAfterDays`（預設 30）：送出後超過天數仍未結束就逾期，申請人要重新送出。由排程 `flow.maintenance`（每 15 分鐘）處理，它也依目前的組織與授權重算每一關的審核人。
- `RemindAfterDays`（預設 2）：關卡等待超過天數時，`flow.reminders` 在工作日早上提醒審核人。

## 新增申請種類

業務模組實作 `IFlowKind` 並以 `AddFlowKind` 註冊，送出時呼叫 `FlowService.SubmitAsync`；預設流程寫在 `database/App/Seed/flow.sql`（`FlowSeedTests` 檢查每個種類都有）。核准後要做的事放在 `CompleteAsync`，與決定同一個交易。範例：`backend/src/AiNexus.Features/Work/Repositories/Access/RepositoryAccessFlow.cs`。
