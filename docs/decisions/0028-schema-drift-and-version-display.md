# 0028 正式機結構異動偵測與版本呈現

- **狀態**：提議（2026-10-29）
- **決定**：
  - 不在客戶庫裝 DDL trigger。以「預期快照」比對實際結構：有無異動看雜湊，時間取 `sys.objects` 的 `modify_date`／`create_date`。
  - 取快照與比對寫成同一組 procedure（schema `releases`），update.sql、演練庫與網站共用，雜湊算法只有一份。
  - 相容性看版號；檢查與異動偵測接上既有的監控子系統，不另做輪詢。
  - 新模組 `Admin/Releases`：版本資訊、異動清單與差異、部署歷史。
- **背景**：客戶端常直接改 procedure，回開發庫同步時會漏；客戶庫無法隨時連線，以往只能寫信詢問客戶做了什麼。

## 快照

- 每個物件一列：schema、名稱、類型、雜湊、定義本文。procedure、function、view、trigger 取 `sys.sql_modules.definition`；資料表的定義是欄位、索引、約束組成的固定格式字串。
- 預期快照取自演練庫（執行同一份 update.sql，見 [ADR 0027](0027-single-version-and-tag-based-release.md)），由 update.sql 寫入目標庫。比對的是「與這一版應有的結構不同」，更新前就存在的客戶端修改也抓得到。
- 實作時先以 PoC 確認演練庫與客戶庫的定義本文一致；不一致時改為更新完成當下在目標庫取快照（只偵測更新後的異動）。
- 新庫由 `Initialize-Database.ps1` 建立後同樣寫入快照。

## 異動判定

| 種類 | 條件 | 時間 |
| --- | --- | --- |
| 修改 | 雜湊不同 | `modify_date` |
| 新增 | 快照沒有 | `create_date` |
| 刪除 | 實際沒有 | 無，只知道不見了 |

- `modify_date` 在定義沒變時也可能更新（例如重新整理 module），只當參考；是否異動以雜湊為準。
- 排除快照相關的表與 `database/App/export-exclusions.txt` 列出的表。
- 網站的 SQL 帳號需要 `VIEW DEFINITION` 才讀得到 procedure 本文，寫進 [資料庫建置與權限](../operations/database-setup.md)。

## 相容性

- 發版時把「這版程式需要的最低資料庫版本」（本版以前最近一個含資料庫的 tag）寫進程式，目標庫記錄的版本較舊即 failing。由工具計算，沒有人要維護常數。
- 精確檢查仍是既有的啟動比對（EF 讀寫的表與欄位，[資料庫](../architecture/database.md)），它擋下啟動；版號檢查只顯示、不擋。
- 資料庫可以先發，前提是舊程式仍能運作：新欄位可為 NULL 或有預設值；procedure 新參數要有預設值、回傳欄位只加不刪；改名或刪除分兩次發。

## 效能

- 請求路徑沒有成本：版本是常數。
- 啟動：版號檢查讀一列；結構比對沿用既有的一次 metadata 查詢。
- 異動偵測實作 `IMonitoringSource`（[監控子系統](../architecture/monitoring-sources.md)）：只在有人看時讀、快取 10 分鐘，單一 procedure 在伺服器端算雜湊、只傳回有差異的列；定義本文在打開差異時才讀。
- 每日一次背景工作重算；出現新的異動時，通知有版本頁權限的人。
- 實作時量測耗時並寫入日誌。

## 畫面

依 [設計系統](../frontend/design-system.md) 與 [動效](../frontend/motion.md)。

- **所有使用者**：使用者選單底部以等寬小字顯示版本；點開「關於」對話框，有版本、發布日期與本版更新（tag 之間 `feat`／`fix` 的 commit 主旨，分組列出）。
- **新版提示**：登入後的 API 回應帶 `X-App-Version`；與前端 `APP_VERSION` 不同時，動態島出現「已更新到 v…」與重新整理動作。同一版只提示一次，不打斷操作。
- **監控**：子系統卡片「資料庫結構」，不相容 failing、有異動 degraded、一致 healthy，點選前往版本頁。
- **版本頁**（`Admin/Releases`，獨立 grant）：
  - 版本對照：程式、資料庫，以及程式需要的最低資料庫版本。
  - 異動清單：物件、種類、時間；選取後並排顯示差異（儲存庫瀏覽既有的 diff 解析移到 `@shared` 共用）。可下載異動物件目前定義的 `.sql`，套用到開發庫後匯出提交。
  - 部署歷史：程式（新版本第一次啟動時自動記錄）與資料庫（update.sql 記錄）依時間排列。

## 何時重新評估

客戶允許安裝 DDL trigger（可記錄誰改、執行了什麼語句），或正式機能從開發端直接連線比對時。
