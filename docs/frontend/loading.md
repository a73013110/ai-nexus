# 載入與送出中

結論：全站只有五種「正在進行」的樣子，依資料狀態選一種，不自訂動畫、不自訂淡化。使用者觸發的事一定有回饋，背景輪詢一律安靜；還在讀的時候不顯示「沒有資料」。

| 狀態 | 什麼時候 | 樣子 | 用什麼 |
|---|---|---|---|
| 首次載入 | 還沒有任何資料 | 依版面排的骨架 | `.skeleton`、元件的 `loading` |
| 重新讀取 | 已有資料，重讀、換篩選、搜尋、翻頁、寫入後重讀 | 舊內容保留並淡化，列表上緣掃過細線 | 元件的 `busy`，其他區塊 `.is-refreshing` |
| 背景輪詢 | `apiResource` 的 `poll` | 不顯示，狀態變化交給 `StatusBadge` | — |
| 送出中 | 按鈕觸發的寫入、匯出、測試、重試 | 按鈕圖示換成轉圈並改字，擋重複送出 | `button[nxPending]`、`button[nxRefresh]` |
| 長工作 | 有真實進度的工作 | 確定進度條，或不定進度加階段文字 | `JobProgress`、動態島 |

生成與串流（`GenerationIndicator`、`InferenceSignal`、動態島）是另一套語彙，見 [動效](motion.md#生成與登入)。

## 讀取狀態從哪來

- `apiResource()`：`loading()` 只在第一次或請求改變時為真；`refreshing()` 是使用者能感覺到的重讀（`reload()`、請求改變），**不含輪詢**。
- 首次載入綁 `loading()`；重新讀取綁 `refreshing()`。綁錯（重讀綁 `loading()`）手動重讀與重試會沒有回饋。
- 搜尋框有防抖時，等待期間也算重新讀取：`busy` 綁 `typing() || refreshing()`（例：`admin-users-store.ts`）。

## 首次載入

- 形狀已知（表格、清單、卡片、數字、圖表）用 `.skeleton` 排出版面；容器 `role="status"` 並附 sr-only 的「正在載入X…」。
- 共用元件自己處理：`nx-table-state`（`loading` 綁 `loading()`）、`nx-tree-view`（`busy` 且沒有節點）；`nx-resource-gallery`、`nx-stat-tile`、`nx-month-calendar`、圖表（`nx-trend-chart`、`nx-donut-chart`、`nx-sparkline`、`nx-heatmap-calendar`）與 `nx-select` 的 `loading`。頁面不用一段文字代替骨架。
- `nx-resource-gallery` 與圖表在 `busy` 但還沒有資料時也畫骨架，不先顯示空狀態。
- `nx-stat-tile` 的 `value` 為 null 是「未知」（顯示「—」），載入中要傳 `loading`。
- 形狀未知的小區塊（popover 內的規則、單行說明）才用 `<nx-icon name="loading">` 加一句「正在載入X…」。
- 讀取中不顯示空狀態、「沒有符合的選項」、計數 0 或「找不到」；`nx-empty-state` 只用於確定沒有資料。讀取失敗也不能留著「正在載入…」。
- `@defer` 的 `@placeholder`／`@loading` 用同一種骨架或一行狀態文字。

## 重新讀取

- 保留舊內容，不換回骨架、不清空，也不重設捲動或選取。
- `nx-data-table`、`nx-tree-view`、`nx-resource-gallery`、`nx-month-calendar`、`nx-stat-grid` 與圖表傳 `busy`：已有內容時淡化並設 `aria-busy`，列表類（表格、樹、卡片格線）在上緣掃過 `.refresh-line`。`nx-data-table` 在沒有列（`nx-table-state` 顯示中）時不淡化，骨架就是回饋。
- 其他區塊（卡片、屬性清單、詳情面板）在容器加 `.is-refreshing` 與 `aria-busy`；不自訂 `.stale`、`.is-loading` 之類的淡化。
- 讀取中翻頁、排序與列操作用 `aria-disabled`／`readonly` 擋，不用 `disabled`，焦點才不會掉。
- 分頁與檢視切換（`nx-tabs`、`nx-view-switch`）不轉圈：切換後讀取的回饋在內容區的 `loading`／`busy`。

## 送出中

- 寫入按鈕用 `<button nxPending [pending]="saving()">`：前導圖示換成轉圈、`aria-busy`、`aria-disabled`（不用 `disabled`，焦點留在原處）、送出中忽略點擊與 Enter。
- 重讀按鈕用 `button[nxRefresh]`（只有圖示，至少轉一圈）；讀取失敗的「重試」用 `nxPending`，`pending` 綁該讀取的 `refreshing()`。`nx-table-state` 的重試鈕已內建：`retrying` 綁 `refreshing()`，放在 `nx-data-table` 裡也跟著表格的 `busy` 轉。
- 對話框送出中擋取消與 Esc，欄位唯讀；完成或失敗才解除（失敗訊息留在對話框內）。Esc 由 `nxCompactDialog` 的 `busy` 擋（同時設 `aria-busy`），「取消」鈕用 `aria-disabled`；共用的 `nx-dialog-save-bar`、`nx-name-dialog` 已照做。
- 選單項目的動作（匯出、複製、刪除）送出中由觸發鈕顯示轉圈：`nx-action-menu` 的 `busy`；面板仍開著時用 `MenuAction.busy`。同一列表中只擋正在處理的那一列。
- 立即生效的開關、勾選與單選：可還原的用樂觀更新、失敗還原並提示；不可還原或很慢的用元件的 `pending`（`nx-setting-row` 的開關、`nx-checkbox`、`nx-choice-groups`：控制項旁轉圈、`aria-busy`、忽略再次切換）。
- 下載與匯出也是送出中；上傳顯示在動態島或 toast（`duration: 0`），不只停用按鈕。

## 下拉與搜尋

- 選項來自 API 的 `nx-select` 傳 `loading`：觸發鈕顯示轉圈與「正在載入…」，已選的值不退回 placeholder，選單裡是「正在載入選項…」而不是「沒有符合的選項」。
- 讀取中選項不先藏起來再冒出；欄位保留位置，用 `loading`。
- 打字即搜尋的 `nx-search-field` 傳 `busy`：放大鏡換成轉圈，欄位仍可輸入；結果區照「重新讀取」處理。`nx-resource-gallery` 的搜尋欄跟著它的 `busy`。
- 找人（`directorySearch`）的 `loading()` 涵蓋防抖等待與請求：結果為空且 `loading()` 時顯示「正在搜尋…」，不是「沒有符合的帳號」。
- 有「查詢」鈕的用 `QueryBar` 的 `busy`。

## 上千列的清單

- 一頁上千列（以人分組、每人展開整段區間）時，一次建立所有列會讓畫面卡住，之後每次版面計算也要涵蓋整頁。每段用 `nearViewport`（`frontend/src/app/shared/browser/near-viewport.ts`）在捲到附近才建立，之前放一列估計高度的佔位；每段是 OnPush 元件，後面的段顯示時不重檢查前面的。例：出勤紀錄的 `RecordsPersonGroup`。
- 沒有內容的儲存格不建立元件（例：沒有表單的日子不放清單元件），列數多時差很多。

## 換頁

- 延遲載入的路由與 guard 期間，頂端顯示細的進度線（`RouteProgress`，掛在 `app.html`，看 `Router.currentNavigation()`），不遮內容；不到 200ms 的換頁不顯示。頁面不必自己處理。

## 文案

- 按鈕：「動詞＋中…」，例如「儲存中…」「匯出中…」「建立中…」；字數接近原文，按鈕寬度才不跳。
- 狀態文字（`role="status"`、sr-only、placeholder）：「正在＋動詞＋對象…」，例如「正在載入使用者…」「正在搜尋…」。
- 一律用全形「…」，不用 `...`；不用「讀取中」「載入中」兩字狀態句。

## 外觀

- 轉圈只有一種：`<nx-icon name="loading">`，它自己會轉，呼叫端不加動畫。
- `[aria-busy='true']` 的按鈕保持原本的不透明度、游標是 `progress`；只有真的不能用的才是 `not-allowed` 與淡化。
- 減少動態時轉圈與細線停下，淡化與文字保留（[減少動態](motion.md#減少動態)）。
