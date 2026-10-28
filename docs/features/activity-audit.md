# 活動稽核

「活動稽核」頁回答「誰在何時、從哪個功能登入、查閱、操作或變更了什麼，結果如何」。

- 位置：「系統」導覽下的獨立工作區 `/admin/audit`，功能 ID `audit`。
- 記錄規則（什麼要記、名稱、欄位、失敗處理）見 [ADR 0023](../decisions/0023-audit-standard.md)。
- 相關頁面：
  - 錯誤、耗時與技術流程在[系統日誌](system-logs.md)；
  - 各功能用了多少在[功能使用量](feature-usage.md)；
  - 帳號與授權的管理在[存取管理](administration.md)。

## 權限

- 使用 `audit` 功能授權，初始授予 administrators 群組（範圍「全部」）。
  - 有 `admin` 不代表有 `audit`，所以可以只給稽核人員 `audit`，不給帳號管理權。
  - 每個請求都重新檢查授權。
- 授權帶[資料範圍](../architecture/data-scopes.md)：

  | 範圍 | 看得到的紀錄 |
  |---|---|
  | `own` | 自己是操作者、擁有者或資料主體的紀錄 |
  | `unit`／`unit-tree` | 所屬單位（及下層）目前成員的紀錄 |
  | `all` | 全部 |

- 沒有對應使用者的紀錄（系統動作、未驗證的登入失敗）只有 `all` 看得到。
- 列表、匯出與功能使用量都套用同一個範圍。
- 稽核紀錄沒有存當時的單位：人調單位後，他的紀錄跟著到新單位。
- `GET /api/v1/admin/audit/catalog` 只提供解讀紀錄所需的資料：
  - 功能、模型名稱與功能在選單的位置。功能異動依選單分組，與[存取管理](administration.md#授權工作流程)相同。
  - 分類、模組與動作清單。
  - 不回傳角色、權限群組或帳號清單。

## 畫面

- 分類切換：登入與身分、管理異動、查閱與匯出、功能操作。值存在紀錄的 `Category` 欄位，篩選直接比對這個欄位。
- 動作篩選：
  - 每個模組先列「模組（全部）」，選它就是以模組前綴篩選；底下再列該模組在目前分類裡的動作。
  - 名稱都來自 catalog，新增動作不需要改前端。
- 結果篩選：「完成」（`completed`）或「失敗或拒絕」（其他所有錯誤代碼）。
- 其他條件：
  - 搜尋帳號、資料主體、動作、Trace ID、查證代碼或詳情文字；
  - 輸入 GUID 時，改比對資源、操作者、擁有者或資料主體的 ID；
  - 依台北日期選期間。
- 從功能使用量頁連過來時會帶入 `feature`，顯示成一個可以清除的條件。
- 列表欄位依序是：時間、操作者（代為操作時另外顯示測試身分）、動作與分類、功能、資料主體、結果。「資源」欄預設隱藏。
- 分頁：
  - 每頁 25／50／100 筆，預設 50。
  - 以 ID 游標分頁，查詢期間有新紀錄寫入也不會重複；不計算總數。
- 詳情抽屜：
  - 欄位的前後差異；
  - 失敗原因（錯誤代碼的公開說明加上代碼本身）；
  - 功能、資料主體、來源 IP；
  - TraceId、OperationId、查證代碼，以及原始紀錄。
- 「查證相關日誌」會帶入同一個 trace（沒有 trace 時改用查證代碼），時間取前後五分鐘。
- 前端在 `features/admin/audit`，依路由延後載入；頁面與抽屜都有操作導覽。

## 匯出

`GET /api/v1/admin/audit/export` 匯出整個查詢結果。

- **範圍**：與列表同一組篩選，但不分頁。
- **格式**：由新到舊、UTF-8 BOM，正確處理引號並加上公式前綴防護。時間為 UTC，結果是原始代碼，異動資訊是遮罩後的 metadata JSON。
- **上限**：筆數超過 `Audit.MaxExportRows`（預設 10000）就整個拒絕（`audit_export_limit`），不截斷。每人每分鐘可匯出 2 次。
- **稽核**：匯出本身記為 `audit.event.exported`。被拒絕或逾時（`Audit.ExportTimeoutSeconds`）的嘗試也會留下錯誤代碼。

## 保存

- 稽核沒有更新或刪除的 API。
- 資料庫管理帳號直接改表的權限，依公司政策限制。
- 保存期由部署單位制定。
- SQL 無法寫入時，查詢會失敗而不回傳資料（失敗即拒絕）。

## 驗證

- `RequestAuditTests`：端點宣告、名稱唯一、查詢合併、失敗與拒絕的寫法。
- `ListActivityAuditTests`、`AuditDataScopeTests`：篩選、資料範圍與匯出。
- `LoginAuditTests`：登入紀錄。
- 前端：`audit-query.spec.ts`、`audit-presentation.spec.ts`、`audit-tour.spec.ts`。

設計參考 [OWASP Logging Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Logging_Cheat_Sheet.html)，以及 Microsoft Entra 把[登入紀錄](https://learn.microsoft.com/en-us/entra/identity/monitoring-health/concept-sign-ins)與[稽核紀錄](https://learn.microsoft.com/en-us/entra/identity/monitoring-health/concept-audit-logs)分開查詢的做法。
