# 資料工作區

結論：固定欄位的資料頁（稽核、日誌、管理列表）照以下規則；共用元件與通用版面見 [共用介面模式](ui-patterns.md)。

- 表格用 `DataTable` 系列（獨立捲動、固定表頭、欄位偏好 `preferenceKey`、整列點選）；排序、分頁的資料與查詢由業務頁負責。
- 列狀態只有一套（`frontend/src/styles/data-workspace.scss`），頁面不另寫 hover、選取或 focus：
  - 可點的列用 `nxTableRow`，列內一個 `data-row-action`（名稱連結或開啟按鈕）負責鍵盤開啟；其他控制項（列選單）保留自己的焦點。
  - hover 淡底；開啟中的列（對話框、詳情）加 `is-selected`，用 `--accent-soft` 底色，不加側邊色條；狀態不改版面。
  - 只有鍵盤焦點（`:focus-visible`）畫框，而且只畫在整列一圈，不在名稱外再套一圈；滑鼠點選不留框。對話框或抽屜關閉後還原的焦點沿用開啟時的狀態：點開的列即使按 Esc 關閉也不留框（瀏覽器此時會判定為鍵盤焦點，`DataTableRow` 以 `is-focus-returned` 擋下），用鍵盤開啟的才保留。
  - 最後一列貼著外框圓角時（下方沒有頁尾或狀態），列的底角用外框內圓角，底色與焦點框不被切掉。
- 高度：表格框只包住資料列，不撐出空白；資料頁（`layoutMode="data"`）最多用到視窗剩餘高度（至少 18rem），超過時表格內捲動、表頭固定。頁面與表格之間的容器（分頁、頁籤）加 `ui-table-fill` 傳遞高度，不寫固定高度；資料頁以外（面板、對話框）需要上限時才設 `--table-max-height`。`stacked` 卡片在 `narrow` 以下改由頁面捲動。
- 捲軸：表格不預留捲軸空間（`scrollbar-gutter: auto`），表頭與列線延伸到外框，資料超出時才出現捲軸；有內距的捲動區見 [design-system.md](design-system.md)。
- 長文字放詳情，列高一致；原文由 title 與鍵盤可開的詳情取得。數字欄的表頭與儲存格加 `ui-table-numeric`（靠右、等寬數字；卡片版靠左）。
- 詳情與關聯資料分別載入、分別顯示失敗；關閉、改查詢或切換時取消舊請求並擋下遲到回應。完整資料以文字呈現，不當 HTML；權限不足時不顯示詳情與匯出。
- 沒有逐筆詳情的資料不加空抽屜。稽核與日誌的分頁、匯出見 [活動稽核](../features/activity-audit.md) 與 [系統日誌](../features/system-logs.md)。
- 參考 [Carbon Data Table](https://www.carbondesignsystem.com/building-blocks/core/components/data-table/guidelines)；日期選擇依 [WAI APG](https://www.w3.org/WAI/ARIA/apg/patterns/dialog-modal/examples/datepicker-dialog/) 但用非 modal popover，保留 Tab 順序。

## 查詢

- 條件用 `QueryBar`＋`queryState()`：欄位改的是草稿，按「查詢」（或欄位內 Enter）才套用；有未套用的變更就提示。已套用的條件成為可逐一移除的 chip，並寫入 URL：重新整理、分享連結與側欄重新進入都以 URL 為準。分類 `ViewSwitch` 與移除 chip 立即套用（`commit`），不帶走其他未套用的草稿。
- 換頁、排序、每頁筆數與匯出沿用已套用的條件，不寫進 URL；預設區間（「最近 24 小時」）在套用當下換算成起訖。
- 常用條件放在列上，少用的收進「＋條件」popover（按鈕顯示已設定數）。多種 ID 合併成一個搜尋框依格式判斷，例：日誌的 `NX-…` 是查證代碼、32 碼 hex 是 TraceId、GUID 是 Job／Run／Operation（可切換），其餘是訊息文字。
- 時間範圍用 `TimeRangePicker`：預設區間與自訂起訖在同一個 popover，時區只在裡面寫一次。多選用 `Select` 的 `multiple`。

## 分頁、匯出與狀態

- 大表（日誌、稽核）用 `TablePagination` 的 `cursor`：上下頁與回到第一頁，不計總數也不跳頁，改以時間範圍與排序定位，因為大表的 COUNT 與深 offset 成本隨資料量成長。有總數的小表（使用者）用 `offset`，可輸入頁碼。翻頁重設列表位置並關閉舊詳情。
- 表格工具列只放標題與表格動作：匯出放 `[table-actions]`（「欄位」旁），由伺服器匯出整個已套用的查詢，不只已載入的頁。工具列與頁尾不重複筆數、時區或操作提示。
- 載入、無資料與讀取失敗（附重試）用 `TableState`；匯出等動作的失敗另用 `Notice`。系統降級只在異常時以 `Notice` 提示並連到監控，資料頁不另放健康卡。

## 詳情

- 上下筆用 `DrawerPager`（只在本頁移動），識別碼用 `CopyValue`。順序：`drawer-summary`（狀態與時間、標題、主體）→ 主要內容（差異、訊息）→ 基本資訊 → 關聯（可複製的 ID、跨功能反查）→ 原始紀錄（收合）。
- 跨功能連結（日誌 ↔ 稽核）只放在詳情的關聯區，頁首不放；側欄已提供導覽。
