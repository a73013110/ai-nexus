# 活動稽核

「活動稽核」是「系統」導覽中的獨立工作區，網址 `/admin/audit`，使用 `audit` 功能授權。平台管理負責帳號、角色、群組及使用政策；即時監控負責現在的工作階段與負載；活動稽核負責歷史行為與結果；系統日誌負責錯誤、耗時及技術流程。入口、功能授權及存取預覽共用功能分類與語意圖示。

新安裝預設將 `audit` 授予 administrators 群組。`IndependentActivityAudit` migration 為既有 `admin` 群組補上稽核授權，保留升級前的查閱能力；後續可單獨授權或撤銷。持有 `admin` 不再隱含 `audit`。每次 API 請求重新驗證有效 grant，稽核人員不需要帳號管理權限。

舊書籤 `/admin?tab=audit` 在建立管理頁之前轉到 `/admin/audit`，保留搜尋、分類、Trace ID、日期、動作、結果及 fragment。管理頁移除原稽核分頁，管理工具及使用者活動視窗提供前往獨立頁面的連結。原 `GET /api/v1/admin/audit` URL、DTO、SQL 查詢、游標與資料遮罩沿用；查詢由 `Audit` 模組的 `ListActivityAudit` 負責，使用明確的 `feature:audit` policy。

`GET /api/v1/admin/audit/catalog` 僅提供解讀異動所需的功能及模型名稱，不回傳角色、群組、帳號清單或管理政策。前端頁面、API 及呈現工具位於 `features/audit`，依路由延後載入，不依賴管理頁或管理目錄。

列表共用 FilterPanel、ViewSwitch、DataTable、TablePagination 及 DetailDrawer。分類、動作、結果、搜尋與台北日期可組合；每頁最多 100 筆，以 ID 游標避免新活動插入造成重複。CSV 僅匯出已載入的頁面，保留公式防護與可讀欄位。切換同一頁的網址條件會重新查詢並清除原詳情與分頁。

「查證相關日誌」帶入同一 Trace ID（缺少 trace 時使用查證代碼）及事件前後五分鐘，保留歷史查證範圍；系統日誌詳情可反查同一 trace 的稽核。兩側連結依各自授權顯示，API 重新驗證。監控中的頁面分類與 API 流量也分別辨識 audit／logs，不歸入平台管理。

事件分類、保存與關聯細節見 [系統日誌與維運](DIAGNOSTICS.md)。授權、游標、日期、匯出、關聯查證、舊書籤、同頁導航與權限不足的驗證分別由 `ActivityAuditTests`、`admin.spec.ts`、`navigation-audit.spec.ts` 及 `system-logs.spec.ts` 執行。
