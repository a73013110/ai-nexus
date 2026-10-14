# 資料工作區

結論：固定欄位的資料頁（稽核、日誌、管理列表）照以下規則；共用元件與通用版面見 [共用介面模式](ui-patterns.md)。

- 表格用 `DataTable` 系列（獨立捲動、固定表頭、欄位偏好 `preferenceKey`、整列點選）；排序、分頁的資料與查詢由業務頁負責。
- 長文字放詳情，列高一致；原文由 title 與鍵盤可開的詳情取得。
- 查詢快照與未送出的篩選欄位分開：換頁、匯出、排序、每頁筆數沿用已送出的快照並重設游標。游標分頁不推測總筆數，翻頁重設列表位置並關閉舊詳情。
- 詳情與關聯資料分別載入、分別顯示失敗；關閉、改查詢或切換時取消舊請求並擋下遲到回應。完整資料以文字呈現，不當 HTML；權限不足時不顯示詳情與匯出。
- 沒有逐筆詳情的資料不加空抽屜。稽核與日誌的分頁、匯出見 [活動稽核](../features/activity-audit.md) 與 [系統日誌](../features/system-logs.md)。
- 參考 [Carbon Data Table](https://www.carbondesignsystem.com/building-blocks/core/components/data-table/guidelines)；日期選擇依 [WAI APG](https://www.w3.org/WAI/ARIA/apg/patterns/dialog-modal/examples/datepicker-dialog/) 但用非 modal popover，保留 Tab 順序。
