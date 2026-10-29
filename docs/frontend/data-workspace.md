# 資料工作區

結論：固定欄位的資料頁（稽核、日誌、管理列表）照以下規則；共用元件與通用版面見 [共用介面模式](ui-patterns.md)。

- 表格用 `DataTable` 系列（獨立捲動、固定表頭、欄位偏好 `preferenceKey`、整列點選）。排序、欄位篩選與分頁交給表格模型（[ADR 0026](../decisions/0026-shared-table-queries.md)）：前端已有資料用 `localTable`、後端列表 `endpointTable`、大表 `cursorTable`，以 `[table]` 傳入、表頭寫 `th[nxHeader]`，載入／空狀態與分頁列自動產生。
- 列狀態只有一套（`frontend/src/styles/data-workspace.scss`），頁面不另寫 hover、選取或 focus：
  - 可點的列用 `nxTableRow`，列內一個 `data-row-action`（名稱連結或開啟按鈕）負責鍵盤開啟；其他控制項（列選單）保留自己的焦點。
  - hover 淡底；開啟中的列（對話框、詳情）加 `is-selected`，用 `--accent-soft` 底色，不加側邊色條；狀態不改版面。
  - 只有鍵盤焦點（`:focus-visible`）畫框，而且只畫在整列一圈，不在名稱外再套一圈；滑鼠點選不留框。對話框或抽屜關閉後還原的焦點沿用開啟時的狀態：點開的列即使按 Esc 關閉也不留框（`DataTableRow` 的 `is-focus-returned`），用鍵盤開啟的才保留。
  - 最後一列貼著外框圓角時（下方沒有頁尾或狀態），列的底角用外框內圓角，底色與焦點框不被切掉。
- 高度：資料頁（`layoutMode="data"`）的表格框一律延伸到視窗底部（至少 18rem），列少時框內留白、分頁列貼底，列多時內捲、表頭固定。以一張表為主的頁都用資料版面，兩份資料用 segment `ViewSwitch` 一次一張；並列的面板表格才用 `--table-max-height`。頁面與表格之間的容器加 `ui-table-fill` 傳遞高度。對話框 body 裡的表格同樣吃剩下的高度，包著表格的元件 host 要是 flex 欄。`stacked` 卡片在 `narrow` 以下改由頁面捲動。
- 放大：內容超出捲動區時「欄位」旁出現「放大表格」，從原位過渡到貼齊視窗，Esc 還原；對話框裡的表格用對話框的放大。
- 捲軸：表格不預留捲軸空間，表頭與列線延伸到外框；有內距的捲動區見 [design-system.md](design-system.md)。
- 長文字放詳情，列高一致；原文由 title 與鍵盤可開的詳情取得。數字欄的表頭與儲存格加 `ui-table-numeric`（靠右、等寬數字；卡片版靠左）。
- 詳情與關聯資料分別載入、分別顯示失敗；關閉、改查詢或切換時取消舊請求並擋下遲到回應。完整資料以文字呈現，不當 HTML；權限不足時不顯示詳情與匯出。
- 沒有逐筆詳情的資料不加空抽屜。稽核與日誌的分頁、匯出見 [活動稽核](../features/activity-audit.md) 與 [系統日誌](../features/system-logs.md)。
- 日期一律用共用日曆（非 modal popover，保留 Tab 順序）。

## 查詢

- 條件用 `QueryBar`＋`queryState()`：欄位改的是草稿，按「查詢」（或欄位內 Enter）才套用；有未套用的變更就提示。已套用的條件成為可逐一移除的 chip（欄位標 `nxQueryField` 時依欄位順序），並寫入 URL：重新整理與分享連結都以它為準。分類 `ViewSwitch` 與移除 chip 立即套用（`commit`），不帶走其他未套用的草稿。連結的條件驗證不過時不查詢：結果維持預設、條件留在草稿並顯示原因。
- 連動條件（單位→人員）：上層放前面，以 `dependents` 宣告；上層換值清下層，清回預設保留。下層選項依草稿的上層值向後端重讀。
- 分類 `ViewSwitch` 只篩同一份資料時放 `QueryBar` 的 `[query-lead]`；切換成另一份資料（例：價格表的「目前價格／價格歷史」）時，segment `ViewSwitch` 放在 `.page-toolbar`，該頁的一句說明以 `.form-note` 放同一列，放不下自動換列。間距由容器提供，頁面不替 `ViewSwitch` 或說明補 margin。
- `.page-toolbar` 只分左右兩端：左邊搜尋或說明，右邊的多顆動作包進 `.page-actions`；手機上動作鈕只留圖示。
- 欄位篩選只在查出的資料裡縮小範圍；同頁有 `QueryBar` 時傳 `[table]`，chip 併成一列。換頁、排序、欄位篩選與每頁筆數不寫進 URL；預設區間在套用當下換算成起訖。匯出亦同，但預設區間在按下匯出時重算。
- 常用條件放在列上，少用的收進「＋條件」popover（按鈕顯示已設定數）。多種 ID 合併成一個搜尋框依格式判斷（例：[系統日誌](../features/system-logs.md)）。
- 時間範圍用 `TimeRangePicker`：預設區間與自訂起訖在同一個 popover，時區只在裡面寫一次。多選用 `Select` 的 `multiple`。

## 分頁、匯出與狀態

- 大表（日誌、稽核）用 `cursor` 分頁：不計總數也不跳頁，以時間範圍與排序定位，因為 COUNT 與深 offset 成本隨資料量成長。其餘用 `offset`，可輸入頁碼。換頁時關閉舊詳情寫在模型的 `navigated`。
- 分頁列版型固定：左側範圍與每頁筆數（inline `Select`，手機隱藏），右側一組有外框的翻頁按鈕、頁碼夾在中間；頁面不自訂分頁樣式。卡片格線、選擇器也用表格模型，以 `<nx-table-pagination [table]>` 翻頁，單位用 `unit`。
- 表格工具列（`[table-toolbar]`）只放標題＋必要計數（沒有分頁時的筆數、未定價等需要注意的數量）；表格層級的動作（匯出、驗證）放 `[table-actions]`（「欄位」旁），匯出由伺服器處理整個已套用的查詢，不只已載入的頁。
  - 下載用 `ApiClient.open` 取回應、`attachmentName`（`frontend/src/app/shared/browser/download.ts`）取伺服器的 `filename*` 中文檔名，再 `downloadBlob`；匯出鈕用 `nxPending`，送出中顯示「匯出中…」。
  - 不放說明句：操作提示交給按鈕名稱與 `title`，單位寫在表頭，頁面層級的說明放頁面工具列的 `form-note`。
  - 有 `offset` 分頁時筆數只在頁尾出現；工具列與頁尾不重複筆數、查詢區間、時區或操作提示。
  - 頁首 `page-actions` 放整頁的動作（整頁快照匯出、診斷），不放表格動作與跨功能連結。
  - 不放「重新整理」：同條件再按「查詢」就會重讀第一頁；狀態會變的列表（寄送紀錄、工作）用 `apiResource` 的 `poll` 依進行中的資料自動更新，背景更新不擋操作、不重設捲動，狀態變化交給 `StatusBadge` 的 `state` 動效。
- 首次載入、重新讀取與送出中的樣子見 [載入與送出中](loading.md)；沒用表格模型時 `busy` 綁 `refreshing()`。
- 表頭下方的載入、無資料與讀取失敗（附重試）一律用 `nx-table-state`，不在 `tbody` 放空白列、不自組空狀態。「欄位」與表格動作固定在框的右上，沒有標題時也一樣。表格沒有資料時讀取失敗只由它顯示；已有資料時重新整理失敗才用頁面的 `Notice`。匯出等動作的失敗另用 `Notice`。系統降級只在異常時以 `Notice` 提示並連到監控，資料頁不另放健康卡。

## 詳情

- 屬性多的詳情（AD 屬性、原始屬性）用 `PropertyList`：分組、名稱旁附來源名稱、可篩選並標出相符處，沒有值的預設收起；整組共用的限制寫在組的說明，不在每列重複。
- 上下筆用 `DrawerPager`（只在本頁移動；有未儲存變更時先確認），識別碼用 `CopyValue`。順序：`drawer-summary`（狀態與時間、標題、主體）→ 主要內容（差異、訊息、指標）→ 基本資訊 → 關聯（可複製的 ID、跨功能反查）→ 原始資料（`details.ui-raw-record`，預設收合、純文字）。
- 以頁籤編輯的管理對話框（使用者管理）沿用同一順序的精簡版：標題列放狀態與 `DrawerPager`，摘要下方一列放關聯，頁籤是主要內容。
- 跨功能連結（日誌 ↔ 稽核）只放在詳情的關聯區，頁首不放；側欄已提供導覽。
