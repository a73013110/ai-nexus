# 平台營運

全平台用量、知識檢索索引與模型價格分屬三個模組的管理頁，各自一個授權；帳號與授權在 [存取管理](administration.md)。

| 頁面 | 網址 | 功能 | 內容 |
| --- | --- | --- | --- |
| 平台用量 | `/admin/operations/usage` | `operations.usage` | 固定最近 30 天：帳號、生成次數、回報 tokens、原檔容量四個數字；各模型（請求、失敗率、tokens、處理時間、未完整回報）、使用來源占比；模型供應商與網路搜尋的連線狀態 |
| 檢索管理 | `/work/knowledge/retrieval` | `knowledge.retrieval` | 向量索引的狀態與覆蓋率，重建、啟用與清除退役向量都先確認；服務能力（SQL 向量、全文檢索、向量與重排模型）與驗證；檢索測試台 |
| 模型與工具價格 | `/admin/billing/prices` | `billing.prices` | 「目前價格」列出每個已設定的模型與工具適用中的版本（未定價者標示）；「價格歷史」列出版本與狀態。新增或帶入版本在對話框完成，欄位見 [費用](billing.md#設定價格) |

各頁右上角的問號可開啟操作導覽。

## 授權

- 三個功能與存取管理各自獨立，互不隱含：可以只給會計人員價格、只給維運人員用量，而不給帳號管理權。
- 初始只授予 `administrators` 群組；其他群組由管理員在「功能群組與模型」勾選。
- API 每個 request 重新驗證：`/api/v1/admin/usage`、`/api/v1/admin/billing/spend`、`/api/v1/admin/billing/export` 與總覽的 `scope=platform` 由 `operations.usage` 把關；`/api/v1/admin/billing/prices`、`/api/v1/admin/billing/targets` 由 `billing.prices`；`/api/v1/admin/knowledge/*` 由 `knowledge.retrieval`。
- 檢索索引的重建、啟用與清除仍走管理稽核（`admin.embedding_*`），交易內再確認操作者仍持有 `knowledge.retrieval`。
- 區間費用、使用者排行與 CSV 在總覽切換「整個平台」，需要 `operations.usage`，見 [總覽](dashboard.md)。平台讀取與匯出都留下稽核。
