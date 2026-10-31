# 0023 稽核規範

- **狀態**：採用（2026-10-27），取代 [0014](0014-procedure-audit.md)
- **決定**：每個 `/api/v1` 端點都要宣告稽核動作，或寫明不稽核的理由。宣告了稽核的端點，每個請求都寫一筆 `audit.AuditEvents`，查詢、失敗與授權拒絕也不例外。寫入只經過 `AuditTrail` 這一個入口。

## 背景

銀行稽核要回答「誰、在哪個功能、查了或改了哪些資料、用什麼條件、結果如何」，查詢也算在內。原本的做法有三個問題：

- 只記異動與少數敏感讀取。
- 動作名稱由各模組自取（`admin.user`、`identity.login`），分類靠名稱前綴推測。
- 結果值不一致（`saved`、`read`、`failed`…）。

## 端點宣告

- 每個端點寫 `.Audited(XxxAudit.Field)` 或 `.NotAudited("理由")`。`RequestAuditTests` 會檢查每個 `/api/v1` 端點都有宣告，而且宣告稽核的端點在 `WithAudit()` 群組底下。
- 只有不含業務資料、或已由其他紀錄留證的端點可以不稽核，例如：
  - 固定目錄與下拉選項；
  - 每頁都會呼叫的自身資料與登入狀態；
  - 心跳與進度輪詢；
  - 生成串流（送出訊息時已經留證）；
  - 前端錯誤回報。

## 動作名稱

- 格式是 `<模組>.<對象>.<動詞>`：
  - 模組用功能目錄的模組 ID；不在目錄裡的平台模組（`account`、`remote-files`、`web-search`）用自己的 kebab-case 名稱。
  - 對象用 snake_case 單數。
  - 動詞只能取固定清單 [`AuditAction`](../../backend/src/AiNexus.Platform/Auditing/AuditAction.cs) 裡的值。
- 分類在宣告時決定，存在 `Category` 欄位，共四種：`authentication`、`administration`、`access`、`activity`。`access` 只能用 `read`、`searched`、`exported`、`downloaded` 這四個動詞，其他分類則不能用這四個。
- 宣告寫在模組根目錄的 `<Module>Audit` 靜態類別，附中文標籤。稽核頁的動作清單、標籤與分類都由 [`AuditActions`](../../backend/src/AiNexus.Features/Admin/Audit/AuditActions.cs) 從宣告收集，前端不寫死。
- 名稱一旦寫進資料庫就是歷史，不跟著類別或端點改名。真的要改時，同一次修改要附上更新既有資料列的資料庫腳本。

## 一筆紀錄

| 欄位 | 內容 |
|---|---|
| `OwnerId`／`ActorId` | 紀錄的擁有者。用測試身分代為操作時，`ActorId` 是實際操作的管理者 |
| `FeatureId` | 這次通過授權的功能，由功能 policy 解析 |
| `SubjectUserId` | 資料主體，也就是被查閱或被修改的使用者 |
| `ResourceId` | 被操作的資料列 |
| `Result` | `completed` 或錯誤代碼：problem 的 code、`validation_failed`、`feature_forbidden`、`unexpected_error` 等 |
| `DetailsJson` | 查詢條件與前後快照，鍵名必須在 `AuditRedactor` 白名單內 |
| `ClientAddress`、`TraceId`、`OperationId`、`IssueCode`、`At` | 來源與查證關聯 |

- 預設值：
  - 路由 `{id}` 是 GUID 時，當成 `ResourceId`；
  - `{userId}`、`users/{id}` 當成 `SubjectUserId`。
- 路由參數與查詢字串會自動記成條件。只保留識別碼、日期、數字、列舉這類短值，其他值一律記成 `[text]`，所以不會存到搜尋本文。
- handler 有兩個方法可用：
  - `Describe(resource, subject, details)` 補上對象、資料主體與明細；
  - `Use(action)` 讓同一個端點依輸入改用另一個宣告（例如新增與修改共用端點）。

## 寫入時機與失敗

- **異動**：請求開始時，稽核列就加進請求的 DbContext。handler SaveChanges 時，它與業務異動在同一個交易裡一起提交或一起回滾。
- **查詢與失敗**：handler 沒有 SaveChanges 時，由 `CompleteAsync` 用新的 DbContext 補寫。
  - 查詢的稽核在回應送出前寫入，寫不進去就不送出資料（失敗即拒絕）。所以 `completed` 表示資料已開始送出，送出途中中斷（例如取消下載）不改紀錄。
  - handler 已經提交、之後才失敗時（例如跨資料庫的提交失敗），會把同一筆紀錄改記成錯誤代碼。
- **例外**：記 `unexpected_error`，或外部服務自己的代碼。如果這筆也寫不進去，就記日誌 1003 `audit.failure_unrecorded`，原本的例外照常拋出。
- **授權拒絕**：`AuditingAuthorizationResultHandler` 在寫出 403 前，用端點宣告的動作寫一筆，結果與回應的錯誤代碼相同（`feature_forbidden`），`FeatureId` 是被要求的功能。放在授權結果處理而非 requirement handler，每個請求只記一筆，評估授權也不帶寫入。這筆寫不進去時記日誌 1003，仍回 403：拒絕沒有送出資料。
- **請求以外**：背景工作，或一個請求影響多筆資料時，用 `AuditTrail.Record`，由呼叫端在自己的交易裡提交。
- `AuditEventInterceptor` 會拒絕沒有分類的紀錄。

## 重複查詢

同一個人、同一個動作、相同條件的成功查詢，10 分鐘內只記第一次（`RepeatedReads`）。

- 這樣輪詢與重新整理不會淹沒稽核；資料與條件在第一筆就已經留證。
- 是否重複只看伺服器端的身分、動作與條件，前端無法藉此略過稽核。
- 失敗的查詢不合併。
- 紀錄只存在執行個體的記憶體，重啟後最多多記一筆。

## procedure

procedure 不再寫稽核。呼叫 procedure 的端點跟其他端點一樣由請求稽核記錄：procedure `THROW` 就是失敗。

已移除：

- `audit.usp_WriteAuditEvent`
- `audit.udf_AuditContext`
- 以 session context 傳遞稽核情境的做法

## 功能使用量

使用量從 `FeatureId` 統計（[功能使用量](../features/feature-usage.md)），不另建使用量表，也不在前端埋點。

## 為什麼

- **每個請求都記，不挑「敏感」的記**：哪些查詢算敏感，會隨法規與資料改變。漏記無從發現，多記的可以用篩選處理；宣告加上測試，新端點就不可能忘記稽核。
- **只有一個寫入入口**：先前程式、procedure、登入各有一套寫法（0014 的 session context、`AdministrativeReadAudit`、`AuditOutcomes`），結果值與情境補值都不一致。
- **分類存成欄位**：從前綴推測會把 `admin.` 底下的讀取歸錯，動作一改名分類也跟著變。
- **結果只分完成與錯誤代碼**：錯誤代碼與對外 problem 相同，可以對照公開說明；`saved`、`read` 這類值沒有提供資訊。
- **讀取失敗即拒絕**：稽核是送出資料的前提，寫不進去時寧可讓使用者重試。
- **不用觸發程序、SQL Server Audit 或 Temporal table**：它們記的是資料庫帳號與欄位，不是應用程式使用者、功能與條件；可以給 DBA 當補充。
- **代價**：每個查詢都有一筆，稽核表成長較快。對策是合併重複查詢，並為 `Category`、`FeatureId`、`SubjectUserId` 建索引；保存期由部署單位制定。
