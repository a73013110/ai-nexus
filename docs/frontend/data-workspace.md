# 資料工作區

結論：固定欄位的資料頁（稽核、日誌、管理列表）照以下規則；共用元件與通用版面見 [共用介面模式](ui-patterns.md)。

- 表格用 `DataTable` 系列（獨立捲動、固定表頭、欄位偏好 `preferenceKey`、整列點選）；排序、分頁的資料與查詢由業務頁負責。
- 列狀態只有一套（`frontend/src/styles/data-workspace.scss`），頁面不另寫 hover、選取或 focus：
  - 可點的列用 `nxTableRow`，列內一個 `data-row-action`（名稱連結或開啟按鈕）負責鍵盤開啟；其他控制項（列選單）保留自己的焦點。
  - hover 淡底；開啟中的列（對話框、詳情）加 `is-selected`，用 `--accent-soft` 底色，不加側邊色條；狀態不改版面。
  - 只有鍵盤焦點（`:focus-visible`）畫框，而且只畫在整列一圈，不在名稱外再套一圈；滑鼠點選與對話框關閉後還原焦點都不留框。
- 高度：表格框只包住資料列，不撐出空白；資料頁（`layoutMode="data"`）最多用到視窗剩餘高度（至少 18rem），超過時表格內捲動、表頭固定。頁面與表格之間的容器（分頁、頁籤）加 `ui-table-fill` 傳遞高度，不寫固定高度；資料頁以外（面板、對話框）需要上限時才設 `--table-max-height`。`stacked` 卡片在 `narrow` 以下改由頁面捲動。
- 長文字放詳情，列高一致；原文由 title 與鍵盤可開的詳情取得。數字欄的表頭與儲存格加 `ui-table-numeric`（靠右、等寬數字；卡片版靠左）。
- 查詢快照與未送出的篩選欄位分開：換頁、匯出、排序、每頁筆數沿用已送出的快照並重設游標。游標分頁不推測總筆數，翻頁重設列表位置並關閉舊詳情。
- 詳情與關聯資料分別載入、分別顯示失敗；關閉、改查詢或切換時取消舊請求並擋下遲到回應。完整資料以文字呈現，不當 HTML；權限不足時不顯示詳情與匯出。
- 沒有逐筆詳情的資料不加空抽屜。稽核與日誌的分頁、匯出見 [活動稽核](../features/activity-audit.md) 與 [系統日誌](../features/system-logs.md)。
- 參考 [Carbon Data Table](https://www.carbondesignsystem.com/building-blocks/core/components/data-table/guidelines)；日期選擇依 [WAI APG](https://www.w3.org/WAI/ARIA/apg/patterns/dialog-modal/examples/datepicker-dialog/) 但用非 modal popover，保留 Tab 順序。
