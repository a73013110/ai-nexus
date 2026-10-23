# 資源集合頁

結論：檔案庫、知識庫、專案、成果文件、分享、品質評測用同一套「列表 → 詳細」結構與總覽的卡片外觀，新的資源型功能照做，不另寫側欄清單或下拉切換。

## 路由

- 列表與詳細用 `optionalId` 合成一條路由（`/work/knowledge`、`/work/knowledge/{id}`），切換時沿用元件。固定子頁（`/admin/quality/retrieval`、`/work/knowledge/reader/{id}`）宣告在 matcher 之前。
- 後端總覽的最近項目、檔案用途等連結直接指向詳細頁，不用 query 選取。

## 列表

- `ResourceGallery`：一列膠囊工具（範圍 `ViewSwitch` segment、功能自己的 `[gallery-tools]`、搜尋）＋卡片格線；載入時保留格線形狀，沒有結果顯示 `[gallery-empty]`。格線最小寬度由 `--gallery-min` 調整。
- 卡片是連結 `a[nxResourceCard]`：圖示、名稱、最多兩行說明，meta 由功能投影（權限、版本、更新時間）。
- 有操作的卡片（檔案）所有卡片共用一個 `MenuPanel`，不每張建 `ActionMenu`。
- 不能整張當連結的項目（背景任務：進度＋停止／重試）同樣放進 `ResourceGallery`，卡片自己套 `--tile-*` 外觀。
- 同一頁的兩種集合（評測集／我的回饋）用範圍 segment 切換，不另開分頁元件。
- 超過一頁的集合（檔案庫與它的挑選對話框）在格線下方用 `TablePagination` 的 `offset`，與有總數的表格同一套（[資料工作區](data-workspace.md#分頁匯出與狀態)）；只有一頁時不顯示，翻頁捲回格線開頭。

## 詳細

- `FeaturePage` 的 `back` 在標題前放返回列表，`query` 帶回列表的篩選（分享的 `sent`）；標題、說明換成該資源的名稱與說明。
- 頁首一個主要動作（開始對話、上傳文件、開始比較），其餘（編輯、權限、封存、刪除、匯出、撤銷）放進「⋯」`ActionMenu`。會自動輪詢的頁面不放「重新整理」。
- 內容是 `Panel`（小標題、圖示、數量、右側動作）組成的雙欄：左側是工作，右側是脈絡；`tablet` 以下疊成一欄。可拖放上傳的區域把 `nxFileDrop` 放在 Panel 上。
- 載入見 [載入與送出中](loading.md)；讀取失敗的 `Notice` 附「重試」。

## 共用字樣

權限用 `accessLabel`（我擁有／共用 · 可編輯／共用 · 可閱讀），文件處理狀態用 `documentStatus`，時間用 `relativeTime`；不要各頁自寫對照。
