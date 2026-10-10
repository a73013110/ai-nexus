# 活動稽核

「活動稽核」是「系統」導覽中的獨立工作區 `/admin/audit`，回答「誰在何時登入、操作、變更授權或查閱資料，結果如何」。錯誤、耗時與技術流程屬於 [系統日誌](SYSTEM_LOGS.md)；帳號與授權的管理在 [平台管理](ADMINISTRATION.md)。

## 權限

- 使用 `audit` 功能授權，初始授予 administrators 群組。持有 `admin` 不隱含 `audit`；可以只給稽核人員 `audit` 而不給帳號管理權。每個 request 重新驗證 grant。
- 查詢由 `Audit` 模組的 `ListActivityAudit` 負責（`GET /api/v1/admin/audit`）。`GET /api/v1/admin/audit/catalog` 只提供解讀異動所需的功能與模型名稱，不回傳角色、群組或帳號清單。
- 前端在 `features/audit`，依路由延後載入，不依賴管理頁。舊書籤 `/admin?tab=audit` 會轉到新頁並保留篩選條件。

## 記錄什麼

- **寫入**：各模組在自己的交易裡加入 `AuditEvent`，與業務一起提交；保存失敗就取消操作。logger 不能取代稽核。
- **授權異動**：角色分派、角色、群組／模型政策、功能異動保存 `resourceKey`、完整 before／after 快照與結果。業務拒絕（驗證失敗、自我鎖定）先回滾，再保存失敗代碼與原值。
- **敏感讀取**：管理員讀對話只記操作者、資源、分頁與數量；搜尋只記是否套用，不存搜尋本文、提問或回答。系統日誌的查詢、詳情、匯出也各有讀取稽核。
- **登入**：`identity.login`（成功／失敗）與 `identity.logout`。只存核准的帳號、登入方式、來源 IP、失敗代碼與追蹤識別，不存密碼、cookie、token 或任意 header。例行的 session 查詢與 Negotiate 401 challenge 不算登入。
- 失敗登入的帳號尚未驗證，actor 是空識別碼，介面顯示「未驗證」與嘗試的帳號，不會歸到既有的登入 cookie。登入稽核用獨立 DbContext，成功寫入後才簽發 cookie；失敗事件與對外 problem 共用同一個查證代碼。
- 不以 HTTP 200、開頁或背景輪詢推測使用者的操作，也不把 HTTP log 複製進稽核。
- 交易提交、未指定結果的事件記為 `completed`。介面顯示「登入成功／登入未完成／已登出」等語意，詳情保留原始結果與失敗代碼。

## 查詢與匯出

- 分類（登入與身分、管理異動、查閱與匯出、功能操作）由同一個後端 expression 決定，同時用於 SQL 篩選與 DTO。
- 分類、動作（前綴）、結果、帳號／資源／Trace ID／查證代碼搜尋與台北日期可組合；每頁最多 100 筆，以 ID 游標分頁，新事件不會造成重複。
- 詳情顯示欄位前後差異與原始 metadata；DTO 帶 TraceId／OperationId／IssueCode。「查證相關日誌」帶入同一 trace（缺少時用查證代碼）與前後五分鐘。
- 「匯出已載入 N 筆」只匯出目前已載入的結果：UTF-8 BOM、正確引號與公式前綴防護。
- 稽核沒有更新或刪除的 API；資料庫管理帳號直接改表的權限依公司政策限制。SQL 無法寫入時不能保證稽核仍被保存。

## 驗證

授權、游標、日期、匯出、關聯查證、舊書籤與權限不足由 `ListActivityAuditTests`、`LoginAuditTests`、`admin.spec.ts`、`navigation-audit.spec.ts`、`system-logs.spec.ts` 檢查。設計參考 [OWASP Logging Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Logging_Cheat_Sheet.html) 與 Microsoft Entra 將 [登入紀錄](https://learn.microsoft.com/en-us/entra/identity/monitoring-health/concept-sign-ins) 和 [稽核紀錄](https://learn.microsoft.com/en-us/entra/identity/monitoring-health/concept-audit-logs) 分開查詢的做法。
