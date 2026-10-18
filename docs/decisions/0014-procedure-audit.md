# 0014 procedure 的稽核

- **狀態**：採用（2026-10-18）
- **決定**：procedure 寫的稽核與程式寫的必須無法區分：同一張 `audit.AuditEvents`、同樣的情境來源、同樣的動作名稱與明細規則。情境由程式在呼叫 procedure 的同一個批次帶入；procedure 只用 `audit` schema 的共用物件寫入。

## 情境由程式帶入

- `ISqlDatabase<T>` 呼叫 procedure 時，在同一個批次先以 `sp_set_session_context` 設定下列鍵，再 `EXEC` procedure，只有一次往返：

  | 鍵 | 來源（與 `AuditEventInterceptor` 相同） |
  |---|---|
  | `audit.owner_id` | `ICurrentUser`；背景工作為作業擁有者 |
  | `audit.actor_id` | 已驗證請求的 ActorId（測試身分時的實際管理者） |
  | `audit.trace_id` | `Activity.Current` 的 TraceId |
  | `audit.operation_id` | `Activity.Current` 的 `operation.id` 標籤 |
  | `audit.at` | `TimeProvider`，不用資料庫時鐘 |

- 每次呼叫都設定全部鍵，沒有值就設 NULL：連線池重用連線時，上一次的值不會殘留到這次。
- procedure 不為稽核宣告 `@ActorId`、`@TraceId` 之類的參數；業務過濾需要的 `@UserId` 照常傳。

## 共用物件

- `audit.udf_AuditContext()`：inline 資料表值函式，回傳上述情境一列。inline 函式會展開進查詢計畫，沒有逐列呼叫的成本。
- `audit.usp_WriteAuditEvent @Action, @ResourceId, @Result, @DetailsJson`：寫一筆；`ISJSON` 不成立或超過 40000 字就 `THROW`。
- 一次異動多列時，不逐列 `EXEC`：用 `OUTPUT` 收集異動列，再一次寫入。

  ```sql
  UPDATE identity.Users SET Enabled = 0
  OUTPUT inserted.Id INTO @changed (Id)
  WHERE ...;

  INSERT audit.AuditEvents (OwnerId, ActorId, TraceId, OperationId, Action, ResourceId, Result, DetailsJson, At)
  SELECT c.OwnerId, c.ActorId, c.TraceId, c.OperationId, N'admin.user', d.Id, N'saved',
         (SELECT d.Id AS id, CAST(0 AS bit) AS enabled FOR JSON PATH, WITHOUT_ARRAY_WRAPPER), c.At
  FROM @changed d CROSS APPLY audit.udf_AuditContext() c;
  ```

## 明細與程式一致

- `DetailsJson` 用 `FOR JSON PATH, WITHOUT_ARRAY_WRAPPER` 從明確欄位組出，鍵名用 camelCase，且只能用 `AuditRedactor` 白名單內的鍵；不得 `SELECT *`，不放密碼、hash、token 與對話內容。
- `Action` 必須能被 `AuditCategories` 分類，否則稽核頁查不到；新動作先加進程式的清單。
- 成功結果由 procedure 寫，和異動同一個交易。**失敗結果由程式在回復後寫**（與 `AdministrativeAudit` 相同）：procedure 內寫的失敗稽核會跟著交易一起回復。
- 共用的測試 helper 檢查 procedure 寫出的每一筆稽核：`OwnerId`、`TraceId`、`At` 與呼叫端一致；`AuditRedactor.Sanitize` 後內容不變；`Action` 可被分類。每支會寫稽核的 procedure 的整合測試都要呼叫它。

## 失敗即拒絕

沒有情境時（例如在 SSMS 直接執行），`OwnerId` 為 NULL，寫入因欄位 NOT NULL 失敗，整支 procedure 回復，不會留下沒有身分的異動。DBA 手動執行時，先自己 `sp_set_session_context`。

## 效能

- 以前「每筆稽核由程式另外呼叫一次函式」的成本在網路往返，不在函式本身。現在稽核寫在 procedure 內，與業務異動同一個批次、同一個交易，沒有額外往返；巢狀 `EXEC` 在伺服器內執行，成本可忽略。
- 真正要避免的是逐列處理：cursor、`WHILE` 迴圈逐列 `EXEC`、在 `SELECT` 裡呼叫 scalar 函式。

## 為什麼不用其他做法

- **觸發程序（`tr_`）自動寫稽核**：拿不到動作名稱與業務明細，只能記欄位差異；EF 寫入也會再記一次，造成重複。
- **SQL Server Audit／Temporal table**：記錄的是資料庫帳號與資料版本，不是應用程式使用者與業務動作；可以作為 DBA 的補充，不取代業務稽核。
- **每支 procedure 都加稽核參數**：參數表變長，且每支都要自己檢查有沒有傳；漏傳時沒有任何地方會發現。
