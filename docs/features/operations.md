# 平台營運

入口 `/admin/operations`，功能識別 `operations`。看全平台用量、管理知識檢索索引、設定模型與工具價格；帳號與授權在 [存取管理](administration.md)。

## 授權

- 與 `admin` 分開：持有 `admin` 不隱含 `operations`，反之亦然。可以只給維運或會計人員平台營運而不給帳號管理權。
- 初始只授予 `administrators` 群組；其他群組由管理員在「功能群組與模型」勾選。
- 以下 API 由 `operations` 把關，每個 request 重新驗證：`/api/v1/admin/usage`、`/api/v1/admin/knowledge/*`、`/api/v1/admin/billing/*` 與總覽的 `scope=platform`。
- 測試身分下只能讀取：GET 以外的請求一律拒絕，包括新增價格、索引操作、服務探測與檢索測試台，見 [測試身分](test-identity.md)。
- 檢索索引的重建、啟用與清除仍走管理稽核（`admin.embedding_*`），交易內再確認操作者仍持有 `operations`。

## 分頁

| 分頁 | `?tab=` | 內容 |
| --- | --- | --- |
| 平台用量 | `usage` | 最近 30 天的活躍使用者、完成／失敗／取消、各模型輸入輸出、缺失用量與原檔容量；模型供應商與網路搜尋的設定狀態 |
| 知識檢索 | `retrieval` | 向量 profile 覆蓋率、重建／啟用／清除舊向量、服務能力與檢索測試台 |
| 模型與工具價格 | `prices` | 新增價格版本與價格歷史，欄位見 [費用](billing.md#設定價格) |

- 區間費用、使用者排行與 CSV 在總覽切換「整個平台」，同樣需要 `operations`，見 [總覽](dashboard.md)。平台讀取與匯出都留下稽核。
